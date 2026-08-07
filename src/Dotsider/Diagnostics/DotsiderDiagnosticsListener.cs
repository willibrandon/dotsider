using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using Dotsider.Views;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Dotsider.Diagnostics;

/// <summary>
/// Listens on a Unix domain socket at ~/.dotsider/sockets/{pid}.dotsider.socket
/// and serves analysis state from the running TUI.
/// </summary>
internal sealed class DotsiderDiagnosticsListener(
    Func<DotsiderState?> getState,
    Func<JsonElement?>? assemblyInfoProvider = null,
    Func<JsonElement?>? currentViewProvider = null) : IAsyncDisposable
{
    private const int MaxConnections = 4;

    private static readonly UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _connectionSlots = new(MaxConnections, MaxConnections);
    private Socket? _listener;
    private Task? _acceptLoop;
    private string? _socketPath;
    private IPeerCredentialVerifier? _peerVerifier;

    /// <summary>
    /// When <see langword="true"/>, rejects all peer connections regardless of identity.
    /// Exposed for testing the rejection code path. Same pattern as <c>overridePid</c>.
    /// </summary>
    internal bool ForceRejectPeers { get; set; }

    /// <summary>
    /// Optional async hook invoked inside the handler after acquiring a connection slot
    /// but before reading. Exposed for testing connection-limit behavior.
    /// </summary>
    internal Func<Task>? TestDelayHook { get; set; }

    /// <summary>The path to the Unix domain socket file.</summary>
    public string? SocketPath => _socketPath;

    /// <summary>Creates the socket and starts accepting connections.</summary>
    /// <param name="overridePid">
    /// Optional PID override for the socket filename. When <see langword="null"/>,
    /// uses the current process ID. Exposed for testing scenarios where multiple
    /// listeners must coexist in the same process.
    /// </param>
    public void StartListening(int? overridePid = null)
    {
        var dir = SocketDirectoryHelper.EnsureSocketDirectory();

        var pid = overridePid ?? Environment.ProcessId;
        _socketPath = Path.Combine(dir, $"{pid}.dotsider.socket");

        // Clean up stale socket from a previous crash
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));

        if (OperatingSystem.IsWindows())
            SocketDirectoryHelper.SecureSocketFile(_socketPath);

        _listener.Listen(5);

        _peerVerifier = PeerCredentialVerifierFactory.Create();
        _acceptLoop = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptAsync(ct);
                _ = HandleConnectionAsync(client);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Log and continue accepting
            }
        }
    }

    private async Task HandleConnectionAsync(Socket client)
    {
        // 1. Connection limit (cheapest check, no I/O)
        if (!await _connectionSlots.WaitAsync(0))
        {
            try
            {
                await using var s = new NetworkStream(client, ownsSocket: true);
                await using var w = new StreamWriter(s, s_utf8NoBom, leaveOpen: true)
                {
                    AutoFlush = true
                };

                // Read and discard the client's request before responding
                // to avoid EPIPE on the client side
                using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await BoundedUtf8LineReader.ReadAsync(
                        s,
                        DotsiderProtocol.MaxRequestBytes,
                        readCts.Token);
                }
                catch (OperationCanceledException)
                {
                }

                var rejection = DotsiderResponse.Fail(
                    $"Connection rejected: too many concurrent connections (limit: {MaxConnections})");
                await w.WriteLineAsync(
                    JsonSerializer.Serialize(rejection, DotsiderJsonContext.Protocol.DotsiderResponse));
            }
            catch
            {
                // Connection-level errors are silently dropped
            }

            return;
        }

        try
        {
            // Optional test hook to hold the connection slot open
            if (TestDelayHook is not null)
                await TestDelayHook();

            await using var stream = new NetworkStream(client, ownsSocket: true);
            await using var writer = new StreamWriter(stream, s_utf8NoBom, leaveOpen: true)
            {
                AutoFlush = true
            };

            // 2. Read with timeout to prevent stalled clients from pinning slots
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            readCts.CancelAfter(TimeSpan.FromSeconds(5));

            BoundedUtf8LineReadResult readResult;
            try
            {
                readResult = await BoundedUtf8LineReader.ReadAsync(
                    stream,
                    DotsiderProtocol.MaxRequestBytes,
                    readCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // 3. Peer credential check (after read so the client's write completes
            //    before we close — avoids EPIPE on the client side)
            if (ForceRejectPeers || !_peerVerifier!.IsSameUser(client))
            {
                var rejection = DotsiderResponse.Fail(
                    "Connection rejected: peer is not the same user");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(rejection, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            if (readResult.Status == BoundedUtf8LineReadStatus.EndOfStream)
            {
                return;
            }

            if (readResult.Status == BoundedUtf8LineReadStatus.TooLarge)
            {
                var errorResponse = DotsiderResponse.Fail(
                    $"Request exceeds the {DotsiderProtocol.MaxRequestBytes}-byte limit");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            if (readResult.Status == BoundedUtf8LineReadStatus.InvalidUtf8)
            {
                var errorResponse = DotsiderResponse.Fail(
                    "Request is not valid UTF-8");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            var line = readResult.Value;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            // 4. Deserialize ([JsonRequired] catches missing "v")
            DotsiderRequest? request;
            try
            {
                request = JsonSerializer.Deserialize(line, DotsiderJsonContext.Protocol.DotsiderRequest);
            }
            catch (JsonException ex)
            {
                var errorResponse = DotsiderResponse.Fail($"Invalid JSON: {ex.Message}");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            if (request is null)
            {
                var errorResponse = DotsiderResponse.Fail("Empty request");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            // 5. Protocol version check (catches wrong-but-present "v")
            if (request.V != DotsiderProtocol.Version)
            {
                var errorResponse = DotsiderResponse.Fail(
                    $"Protocol version mismatch: expected {DotsiderProtocol.Version}, got {request.V}");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonContext.Protocol.DotsiderResponse));
                return;
            }

            // 6. Route to handler
            var response = HandleRequest(request);
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, DotsiderJsonContext.Protocol.DotsiderResponse));
        }
        catch
        {
            // Connection-level errors are silently dropped
        }
        finally
        {
            _connectionSlots.Release();
        }
    }

    private DotsiderResponse HandleRequest(DotsiderRequest request)
    {
        try
        {
            return request.Method.ToLowerInvariant() switch
            {
                // Assembly
                "assembly-info" => HandleAssemblyInfo(),
                "list-types" => HandleListTypes(request),
                "list-methods" => HandleListMethods(request),
                "find-members" => HandleFindMembers(request),
                "get-native-aot-info" => HandleGetNativeAotInfo(),
                "list-native-aot-sections" => HandleListNativeAotSections(),

                // IL
                "disassemble" => HandleDisassemble(request),
                "get-method-debug-info" => HandleGetMethodDebugInfo(request),
                "get-source-link" => HandleGetSourceLink(),
                "search-il-opcodes" => HandleSearchIlOpcodes(request),

                // Metadata
                "get-pe-headers" => HandleGetPeHeaders(),
                "get-clr-header" => HandleGetClrHeader(),
                "get-sections" => HandleGetSections(),
                "get-custom-attributes" => HandleGetCustomAttributes(),
                "get-resources" => HandleGetResources(),
                "resolve-token" => HandleResolveToken(request),
                "read-bytes" => HandleReadBytes(request),

                // Strings
                "get-strings" => HandleGetStrings(request),

                // Dependencies
                "get-assembly-refs" => HandleGetAssemblyRefs(),
                "get-dependency-graph" => HandleGetDependencyGraph(),
                "get-type-refs" => HandleGetTypeRefs(),

                // Size
                "get-size-tree" => HandleGetSizeTree(),
                "get-largest-methods" => HandleGetLargestMethods(request),
                "get-native-aot-size-contributors" => HandleGetNativeAotSizeContributors(request),
                "explain-native-aot-size" => HandleExplainNativeAotSize(request),

                // Symbols
                "get-native-symbols" => HandleGetNativeSymbols(),
                "disassemble-native" => HandleDisassembleNative(request),

                // WebAssembly
                "list-wasm-sections" => HandleListWasmSections(),
                "list-wasm-functions" => HandleListWasmFunctions(),

                // Pre-ILC correlation
                "correlate-method" => HandleCorrelateMethod(request),

                // ReadyToRun correlation
                "r2r-correlate" => HandleR2rCorrelateMethod(request),

                // Diff
                "diff" => HandleDiff(request),
                "diff-size" => HandleDiffSize(request),
                "check-size-budgets" => HandleCheckSizeBudgets(request),

                // NuGet
                "analyze-nupkg" => HandleAnalyzeNupkg(request),

                // Trace (live session)
                "get-trace-events" => HandleGetTraceEvents(request),
                "get-trace-counters" => HandleGetTraceCounters(),
                "get-process-output" => HandleGetProcessOutput(),
                "get-trace-summary" => HandleGetTraceSummary(),
                "start-trace" => HandleStartTrace(request),
                "stop-trace" => HandleStopTrace(),

                // Fields
                "list-fields" => HandleListFields(request),

                // Bundle
                "is-bundle" => HandleIsBundle(request),
                "get-bundle-manifest" => HandleGetBundleManifest(request),

                // Assembly resolution
                "resolve-assembly" => HandleResolveAssembly(request),

                // Navigation (live session)
                "get-current-view" => HandleGetCurrentView(),
                "navigate" => HandleNavigate(request),
                "navigate-to-il-definition" => HandleNavigateToIlDefinition(request),
                "navigate-back" => HandleNavigateBack(),
                "push-assembly" => HandlePushAssembly(request),
                "search" => HandleSearch(request),

                _ => DotsiderResponse.Fail($"Unknown method: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    // --- Helpers ---

    private DotsiderState RequireState() =>
        getState() ?? throw new InvalidOperationException("No assembly is loaded");

    private AssemblyAnalyzer RequireAnalyzer() => RequireState().Analyzer;

    // --- Assembly Handlers ---

    private DotsiderResponse HandleAssemblyInfo()
    {
        if (assemblyInfoProvider is not null)
        {
            var info = assemblyInfoProvider();
            return info is not null
                ? DotsiderResponse.Ok(info)
                : DotsiderResponse.Fail("No assembly is loaded");
        }

        var a = RequireAnalyzer();
        return DotsiderResponse.Ok(AssemblyInfoPayloadBuilder.Build(a, "standard"));
    }

    private DotsiderResponse HandleListTypes(DotsiderRequest request)
    {
        var analyzer = RequireAnalyzer();

        // A Native AOT binary has no metadata TypeDefs; fall back to recovered types.
        if (!analyzer.HasMetadata && analyzer.RecoveredTypes.Count > 0)
        {
            var recovered = analyzer.RecoveredTypes.AsEnumerable();
            if (!string.IsNullOrEmpty(request.Query))
                recovered = recovered.Where(t =>
                    t.FullName.Contains(request.Query, StringComparison.OrdinalIgnoreCase));
            if (request.MaxResults is > 0)
                recovered = recovered.Take(request.MaxResults.Value);
            return DotsiderResponse.Ok(recovered.ToList());
        }

        var types = analyzer.TypeDefs;
        if (!string.IsNullOrEmpty(request.Query))
        {
            types = [.. types.Where(t =>
                t.FullName.Contains(request.Query, StringComparison.OrdinalIgnoreCase))];
        }

        if (request.MaxResults is > 0)
            types = [.. types.Take(request.MaxResults.Value)];

        return DotsiderResponse.Ok(types);
    }

    private DotsiderResponse HandleListMethods(DotsiderRequest request)
    {
        return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildMethodInventory(
            RequireAnalyzer(), request.TypeName, request.Query, request.MaxResults));
    }

    private DotsiderResponse HandleFindMembers(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return DotsiderResponse.Fail("Query is required for find-members");

        return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildMemberSearch(
            RequireAnalyzer(), request.Query, request.MaxResults, request.IncludeCompilerGenerated));
    }

    private DotsiderResponse HandleGetNativeAotInfo()
    {
        try
        {
            return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildInfo(RequireAnalyzer()));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    private DotsiderResponse HandleListNativeAotSections()
    {
        try
        {
            return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildSections(RequireAnalyzer()));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    // --- IL Handlers ---

    private DotsiderResponse HandleDisassemble(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.TypeName) || string.IsNullOrEmpty(request.MethodName))
            return DotsiderResponse.Fail("TypeName and MethodName are required for disassemble");

        var state = RequireState();
        var method = state.Analyzer.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType.EndsWith(request.TypeName, StringComparison.OrdinalIgnoreCase)
            && m.Name.Equals(request.MethodName, StringComparison.OrdinalIgnoreCase));

        if (method is null)
            return DotsiderResponse.Fail($"Method not found: {request.TypeName}.{request.MethodName}");

        var instructions = state.IlDisassembler!.Disassemble(method);
        return DotsiderResponse.Ok(new IlDisassemblyPayload(
            method,
            state.Analyzer.PdbProvenance,
            state.Analyzer.SourceLink,
            request.IncludeDebugInfo ? state.Analyzer.GetMethodDebugInfo(method) : null,
            instructions));
    }

    private DotsiderResponse HandleGetMethodDebugInfo(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.TypeName) || string.IsNullOrEmpty(request.MethodName))
            return DotsiderResponse.Fail("TypeName and MethodName are required for get-method-debug-info");

        var state = RequireState();
        var method = state.Analyzer.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType.EndsWith(request.TypeName, StringComparison.OrdinalIgnoreCase)
            && m.Name.Equals(request.MethodName, StringComparison.OrdinalIgnoreCase));

        return method is null
            ? DotsiderResponse.Fail($"Method not found: {request.TypeName}.{request.MethodName}")
            : DotsiderResponse.Ok(state.Analyzer.GetMethodDebugInfo(method));
    }

    private DotsiderResponse HandleGetSourceLink() =>
        DotsiderResponse.Ok(RequireAnalyzer().SourceLink);

    private DotsiderResponse HandleSearchIlOpcodes(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return DotsiderResponse.Fail("Query is required for search-il-opcodes");

        var state = RequireState();
        var query = request.Query;
        var max = request.MaxResults ?? 50;
        var results = new List<IlSearchResultPayload>();

        foreach (var method in state.Analyzer.MethodDefs)
        {
            if (results.Count >= max) break;

            IReadOnlyList<IlInstruction> instructions;
            try
            {
                instructions = state.IlDisassembler!.Disassemble(method);
            }
            catch
            {
                continue;
            }

            var matches = instructions
                .Where(i => i.OpCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 0)
            {
                results.Add(new IlSearchResultPayload(
                    $"{method.DeclaringType}.{method.Name}", matches));
            }
        }

        return DotsiderResponse.Ok(results);
    }

    // --- Metadata Handlers ---

    private DotsiderResponse HandleGetPeHeaders() =>
        DotsiderResponse.Ok(RequireAnalyzer().PeHeaders);

    private DotsiderResponse HandleGetClrHeader() =>
        DotsiderResponse.Ok(RequireAnalyzer().ClrHeader);

    private DotsiderResponse HandleGetSections() =>
        DotsiderResponse.Ok(RequireAnalyzer().Sections);

    private DotsiderResponse HandleGetCustomAttributes() =>
        DotsiderResponse.Ok(RequireAnalyzer().CustomAttributes);

    private DotsiderResponse HandleGetResources() =>
        DotsiderResponse.Ok(RequireAnalyzer().Resources);

    private DotsiderResponse HandleResolveToken(DotsiderRequest request)
    {
        if (request.Token is null)
            return DotsiderResponse.Fail("Token is required for resolve-token");

        var resolved = RequireAnalyzer().ResolveToken(request.Token.Value);
        return DotsiderResponse.Ok(new TokenResolutionPayload(request.Token.Value, resolved));
    }

    private DotsiderResponse HandleReadBytes(DotsiderRequest request)
    {
        if (request.Offset is null || request.Length is null)
            return DotsiderResponse.Fail("Offset and Length are required for read-bytes");

        var raw = RequireAnalyzer().RawBytes;
        var offset = (int)request.Offset.Value;
        var length = Math.Min(request.Length.Value, raw.Length - offset);

        if (offset < 0 || offset >= raw.Length)
            return DotsiderResponse.Fail("Offset out of range");

        var bytes = raw.Slice(offset, length).ToArray();
        return DotsiderResponse.Ok(new ByteRangePayload(
            offset, length, Convert.ToHexString(bytes), Convert.ToBase64String(bytes)));
    }

    // --- Strings Handlers ---

    private DotsiderResponse HandleGetStrings(DotsiderRequest request)
    {
        var state = RequireState();
        var extractor = state.StringExtractor;
        var minLength = request.MinLength ?? 4;

        var user = extractor.ExtractUserStrings();
        var metadata = extractor.ExtractMetadataStrings();
        var raw = extractor.ExtractRawStrings(minLength);
        var rawUtf16 = extractor.ExtractRawUtf16Strings(minLength);
        var frozen = state.Analyzer.FrozenStrings;

        if (!string.IsNullOrEmpty(request.Query))
        {
            bool Match(StringEntry e) =>
                e.Value.Contains(request.Query, StringComparison.OrdinalIgnoreCase);

            user = [.. user.Where(Match)];
            metadata = [.. metadata.Where(Match)];
            raw = [.. raw.Where(Match)];
            rawUtf16 = [.. rawUtf16.Where(Match)];
            frozen = [.. frozen.Where(Match)];
        }

        var max = request.MaxResults ?? int.MaxValue;
        return DotsiderResponse.Ok(new StringsPayload(
            [.. user.Take(max)],
            [.. metadata.Take(max)],
            [.. raw.Take(max)],
            [.. rawUtf16.Take(max)],
            [.. frozen.Take(max)]));
    }

    // --- Dependency Handlers ---

    private DotsiderResponse HandleGetAssemblyRefs() =>
        DotsiderResponse.Ok(RequireAnalyzer().AssemblyRefs);

    private DotsiderResponse HandleGetDependencyGraph()
    {
        var analyzer = RequireAnalyzer();
        var graph = DependencyGraphBuilder.Build(analyzer);
        return DotsiderResponse.Ok(new DependencyGraphPayload(graph.Nodes, graph.Edges));
    }

    private DotsiderResponse HandleGetTypeRefs() =>
        DotsiderResponse.Ok(RequireAnalyzer().TypeRefs);

    // --- Size Handlers ---

    private DotsiderResponse HandleGetSizeTree()
    {
        var state = RequireState();
        var tree = SizeAnalyzer.BuildSizeTree(state.Analyzer);
        return DotsiderResponse.Ok(tree);
    }

    private DotsiderResponse HandleGetLargestMethods(DotsiderRequest request)
    {
        return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildLargestMethods(
            RequireAnalyzer(), request.MaxResults));
    }

    private DotsiderResponse HandleGetNativeAotSizeContributors(DotsiderRequest request)
    {
        try
        {
            var source = NativeAotPayloadBuilder.ResolveMstatSource(RequireAnalyzer());
            if (source is null)
            {
                return DotsiderResponse.Fail(
                    "Native AOT size contributors require an mstat sidecar; publish with IlcGenerateMstatFile.");
            }

            return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildSizeContributors(
                source, request.Query, request.Section, request.AssemblyName, request.NamespaceName,
                request.TopN, request.IncludeWhy, request.MaxWhyChains));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    private DotsiderResponse HandleExplainNativeAotSize(DotsiderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
            return DotsiderResponse.Fail("target is required");

        try
        {
            var source = NativeAotPayloadBuilder.ResolveMstatSource(RequireAnalyzer());
            if (source is null)
            {
                return DotsiderResponse.Fail(
                    "Native AOT size explanations require an mstat sidecar; publish with IlcGenerateMstatFile.");
            }

            return DotsiderResponse.Ok(NativeAotPayloadBuilder.BuildWhy(
                source, request.Target, request.MaxCandidates, request.MaxWhyChains));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    // --- Symbols Handler ---

    private DotsiderResponse HandleGetNativeSymbols()
    {
        var a = RequireAnalyzer();
        return a.NativeSymbols is { } info
            ? DotsiderResponse.Ok(info)
            : DotsiderResponse.Fail("Managed assembly; no native symbols to read");
    }

    private DotsiderResponse HandleDisassembleNative(DotsiderRequest request)
    {
        var a = RequireAnalyzer();
        var target = request.SymbolAddress ?? request.SymbolName;
        if (string.IsNullOrEmpty(target))
            return DotsiderResponse.Fail("symbolName or symbolAddress is required");

        // A ReadyToRun method spans several code ranges under one managed name; resolve to the method
        // and render all ranges through the shared query, never a false per-range ambiguity.
        if (a.IsReadyToRun)
            return HandleDisassembleReadyToRun(a, target);

        if (a.NativeSymbols is not { } info || info.Symbols.Count == 0)
            return DotsiderResponse.Fail("Managed assembly; no native symbols to disassemble");

        var matches = Core.Analysis.Disasm.NativeDisassembler.FindExecutableSymbols(info, target);
        if (matches.Count == 0)
            return DotsiderResponse.Fail($"No native symbol matches '{target}'");
        if (matches.Count > 1)
        {
            var candidates = string.Join(", ", matches.OrderBy(m => m.VirtualAddress)
                .Select(m => $"0x{m.VirtualAddress:x} {m.ManagedName ?? m.Name}"));
            return DotsiderResponse.Fail($"'{target}' is ambiguous ({matches.Count} matches): {candidates}");
        }

        var result = Core.Analysis.Disasm.NativeDisassembler.DisassembleSymbol(a, matches[0]);
        return result is null
            ? DotsiderResponse.Fail($"'{matches[0].ManagedName ?? matches[0].Name}' has no disassemblable bytes")
            : DotsiderResponse.Ok(new NativeDisassemblyPayload(
                matches[0].ManagedName ?? matches[0].Name,
                a.Architecture,
                result.Value.Instructions));
    }

    private DotsiderResponse HandleListWasmSections()
    {
        try
        {
            return DotsiderResponse.Ok(WasmPayloadBuilder.BuildSections(RequireAnalyzer()));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    private DotsiderResponse HandleListWasmFunctions()
    {
        try
        {
            return DotsiderResponse.Ok(WasmPayloadBuilder.BuildFunctions(RequireAnalyzer()));
        }
        catch (InvalidOperationException ex)
        {
            return DotsiderResponse.Fail(ex.Message);
        }
    }

    private static DotsiderResponse HandleDisassembleReadyToRun(Core.Analysis.AssemblyAnalyzer a, string target)
    {
        var result = Core.Analysis.ReadyToRunCorrelationQuery.Resolve(a, target, CancellationToken.None);
        switch (result.Outcome)
        {
            case Core.Analysis.Models.ReadyToRunQueryOutcome.Ambiguous:
                var candidates = string.Join(", ", result.Candidates.Select(
                    c => $"{c.DeclaringType}::{c.Name} token 0x{c.Token:X8}"));
                return DotsiderResponse.Fail($"{result.Message}: {candidates}");
            case Core.Analysis.Models.ReadyToRunQueryOutcome.NotFound:
            case Core.Analysis.Models.ReadyToRunQueryOutcome.Unavailable:
                return DotsiderResponse.Fail(result.Message ?? "not found");
        }

        var report = result.Report!;
        return report.NativeText is null
            ? DotsiderResponse.Fail($"'{report.Method}' has no precompiled native code"
                + (report.Diagnostic is { } d ? $" ({d})" : ""))
            : DotsiderResponse.Ok(new NativeDisassemblyPayload(
                report.Method, a.Architecture, report.NativeInstructions ?? []));
    }

    private DotsiderResponse HandleCorrelateMethod(DotsiderRequest request)
    {
        var a = RequireAnalyzer();
        if (a.BinaryKind != Core.Analysis.Models.BinaryKind.NativeAot)
            return DotsiderResponse.Fail("correlate-method requires a Native AOT binary");

        var target = request.MethodOrAddress;
        if (string.IsNullOrWhiteSpace(target))
            return DotsiderResponse.Fail("methodOrAddress is required");

        var result = Core.Analysis.CorrelationQuery.Resolve(a, target, CancellationToken.None);
        return result.Outcome switch
        {
            Core.Analysis.Models.CorrelationQueryOutcome.Resolved => DotsiderResponse.Ok(result.Report!),
            Core.Analysis.Models.CorrelationQueryOutcome.Ambiguous => DotsiderResponse.Fail(
                $"{result.Message}: " + string.Join(", ", result.Candidates.Select(c =>
                    $"{c.AssemblyName} {c.DeclaringType}::{c.Name} token 0x{c.Token:X8}"
                    + (c.VirtualAddress is { } va ? $" @ 0x{va:X}" : "")))),
            _ => DotsiderResponse.Fail(result.Message ?? "correlation unavailable")
        };
    }

    private DotsiderResponse HandleR2rCorrelateMethod(DotsiderRequest request)
    {
        var a = RequireAnalyzer();
        if (a.BinaryKind != BinaryKind.ReadyToRun)
            return DotsiderResponse.Fail("r2r-correlate requires a ReadyToRun image");

        var target = request.MethodOrAddress;
        if (string.IsNullOrWhiteSpace(target))
            return DotsiderResponse.Fail("methodOrAddress is required");

        var result = Core.Analysis.ReadyToRunCorrelationQuery.Resolve(a, target, CancellationToken.None);
        return result.Outcome switch
        {
            ReadyToRunQueryOutcome.Resolved => DotsiderResponse.Ok(result.Report!),
            ReadyToRunQueryOutcome.Ambiguous => DotsiderResponse.Fail(
                $"{result.Message}: " + string.Join(", ", result.Candidates.Select(c =>
                    $"{c.AssemblyName} {c.DeclaringType}::{c.Name} token 0x{c.Token:X8}"
                    + (c.VirtualAddress is { } va ? $" @ 0x{va:X}" : "")))),
            _ => DotsiderResponse.Fail(result.Message ?? "ReadyToRun correlation unavailable")
        };
    }

    // --- Diff Handler ---

    private static DotsiderResponse HandleDiff(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.LeftPath) || string.IsNullOrEmpty(request.RightPath))
            return DotsiderResponse.Fail("LeftPath and RightPath are required for diff");

        using var left = new AssemblyAnalyzer(request.LeftPath);
        using var right = new AssemblyAnalyzer(request.RightPath);
        var result = AssemblyDiffer.Compare(left, right);
        return DotsiderResponse.Ok(result);
    }

    private static DotsiderResponse HandleDiffSize(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.LeftPath) || string.IsNullOrEmpty(request.RightPath))
            return DotsiderResponse.Fail("LeftPath and RightPath are required for diff-size");

        if (MstatLocator.Resolve(request.LeftPath) is not { } left)
            return DotsiderResponse.Fail($"{request.LeftPath} is not mstat-backed");
        if (MstatLocator.Resolve(request.RightPath) is not { } right)
            return DotsiderResponse.Fail($"{request.RightPath} is not mstat-backed");

        return DotsiderResponse.Ok(SizeDiffPayloadBuilder.BuildDiffPayload(
            left, right, request.TopN, request.IncludeTree, request.MaxNodes));
    }

    private static DotsiderResponse HandleCheckSizeBudgets(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.AssemblyPath))
            return DotsiderResponse.Fail("AssemblyPath is required for check-size-budgets");

        var budgets = new List<SizeBudget>();
        try
        {
            if (!string.IsNullOrEmpty(request.BudgetFilePath))
                budgets.AddRange(SizeBudgetFile.Load(request.BudgetFilePath));
            if (!string.IsNullOrEmpty(request.BudgetsJson))
                budgets.AddRange(SizeBudgetFile.Parse(request.BudgetsJson));
            foreach (var spec in request.Budgets ?? [])
                budgets.Add(SizeBudgetParser.Parse(spec));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            return DotsiderResponse.Fail(ex.Message);
        }

        if (budgets.Count == 0)
            return DotsiderResponse.Fail("At least one budget is required for check-size-budgets");

        if (MstatLocator.Resolve(request.AssemblyPath) is not { } target)
            return DotsiderResponse.Fail($"{request.AssemblyPath} is not mstat-backed");

        MstatSource? baseline = null;
        if (!string.IsNullOrEmpty(request.BaselinePath))
        {
            baseline = MstatLocator.Resolve(request.BaselinePath);
            if (baseline is null)
                return DotsiderResponse.Fail($"{request.BaselinePath} is not mstat-backed");
        }

        if (baseline is null && budgets.Any(b => b.MaxGrowthBytes is not null || b.MaxGrowthPercent is not null))
            return DotsiderResponse.Fail("A growth budget needs BaselinePath");

        return DotsiderResponse.Ok(SizeDiffPayloadBuilder.BuildBudgetPayload(
            target, baseline, budgets, request.TopN));
    }

    // --- NuGet Handler ---

    private static DotsiderResponse HandleAnalyzeNupkg(DotsiderRequest request)
    {
        var path = request.AssemblyPath;
        if (string.IsNullOrEmpty(path))
            return DotsiderResponse.Fail("AssemblyPath is required for analyze-nupkg");

        using var package = new NuGetPackageAnalyzer(path);
        return DotsiderResponse.Ok(new NuGetPackagePayload(
            package.PackageId,
            package.PackageVersion,
            package.Authors,
            package.Description,
            package.DllFiles));
    }

    // --- Trace Handlers (live session) ---

    private DotsiderResponse HandleGetTraceEvents(DotsiderRequest request)
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        var events = tracer.GetEvents();

        if (!string.IsNullOrEmpty(request.CategoryFilter)
            && Enum.TryParse<TraceEventCategory>(request.CategoryFilter, true, out var category))
        {
            events = [.. events.Where(e => e.Category == category)];
        }

        if (request.MaxResults is > 0)
            events = [.. events.Take(request.MaxResults.Value)];

        return DotsiderResponse.Ok(events);
    }

    private DotsiderResponse HandleGetTraceCounters()
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        var counters = tracer.GetLatestCounters();
        return DotsiderResponse.Ok(counters);
    }

    private DotsiderResponse HandleGetProcessOutput()
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        return DotsiderResponse.Ok(tracer.GetOutput());
    }

    private DotsiderResponse HandleGetTraceSummary()
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        return DotsiderResponse.Ok(tracer.GetSummary());
    }

    private DotsiderResponse HandleStartTrace(DotsiderRequest request)
    {
        var state = RequireState();

        if (state.IsNetFramework)
            return DotsiderResponse.Fail("Assembly targets .NET Framework; EventPipe requires .NET Core 3.0+");

        if (!state.HasEntryPoint && !state.IsNativeBinary)
            return DotsiderResponse.Fail("Assembly has no entry point");

        if (state.Tracer?.ProcessState == TraceProcessState.Running)
            return DotsiderResponse.Fail("A trace is already running");

        state.PendingMutations.Enqueue(s =>
        {
            var args = request.Arguments ?? [];
            s.Tracer?.Dispose();
            s.CommitDynamicArguments(
                ShellFreeArgumentTokenizer.Format(args),
                args);
            s.Tracer = new RuntimeTracer(
                s.Analyzer.LaunchPath, args, () => s.App.Invalidate());
            s.Tracer.Start();
        });

        // Trigger a render frame so the mutation queue gets drained. The socket-thread
        // invalidate can race an in-flight frame and be drained by the Hex1b main loop;
        // the nudger guarantees a build actually runs.
        state.App.Invalidate();
        state.RequestExtraFrame();

        return DotsiderResponse.Ok(new MessagePayload("Trace start queued"));
    }

    private DotsiderResponse HandleStopTrace()
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        if (tracer.ProcessState != TraceProcessState.Running)
            return DotsiderResponse.Fail("Trace is not running");

        tracer.Stop();
        return DotsiderResponse.Ok(new MessagePayload("Trace stopped"));
    }

    // --- Navigation Handlers (live session) ---

    private DotsiderResponse HandleGetCurrentView()
    {
        if (currentViewProvider is not null)
        {
            var view = currentViewProvider();
            return view is not null
                ? DotsiderResponse.Ok(view)
                : DotsiderResponse.Fail("No view state available");
        }

        var state = RequireState();
        return DotsiderResponse.Ok(new CurrentViewPayload(
            state.CurrentTab + 1,
            CurrentTabLabel(state),
            state.PeSubTab,
            state.DynamicSubTab,
            state.Analyzer.FilePath,
            state.NavigationStack.Count,
            state.Tracer?.ProcessState.ToString(),
            state.HexIsDirty,
            state.HasEntryPoint,
            state.IsNativeAot,
            state.IsNetFramework));
    }

    private static string CurrentTabLabel(DotsiderState state) =>
        state.CurrentTab switch
        {
            TabId.General => "General",
            TabId.PeMetadata => "PE/Metadata",
            TabId.IlInspector => IlInspectorTabLabel.For(state),
            TabId.Strings => "Strings",
            TabId.HexDump => "Hex Dump",
            TabId.DepGraph => "Dep Graph",
            TabId.SizeMap => "Size Map",
            TabId.Dynamic => "Dynamic",
            _ => state.CurrentTab.ToString()
        };

    private DotsiderResponse HandleNavigate(DotsiderRequest request)
    {
        if (request.TabId is null)
            return DotsiderResponse.Fail("TabId is required for navigate");

        var tabId = request.TabId.Value;
        if (tabId is < 1 or > 8)
            return DotsiderResponse.Fail($"TabId must be 1-8, got {tabId}");

        var tabIndex = tabId - 1;
        var state = RequireState();
        state.PendingMutations.Enqueue(s =>
        {
            s.NavigateToTab(tabIndex);
        });

        // Trigger a render frame so the mutation queue gets drained. The socket-thread
        // invalidate can race an in-flight frame and be drained by the Hex1b main loop;
        // the nudger guarantees a build actually runs.
        state.App.Invalidate();
        state.RequestExtraFrame();

        return DotsiderResponse.Ok(new MessagePayload($"Navigation to tab {tabId} queued"));
    }

    private DotsiderResponse HandleSearch(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return DotsiderResponse.Fail("Query is required for search");

        var state = RequireState();
        var tabId = request.TabId is { } t ? t - 1 : state.CurrentTab;

        state.PendingMutations.Enqueue(s =>
        {
            var search = s.Search[tabId];
            if (!search.IsActive)
                search.ActivateOrCycle();
            search.UpdateQuery(request.Query);
            search.Confirm();
        });

        // Trigger a render frame so the mutation queue gets drained. The socket-thread
        // invalidate can race an in-flight frame and be drained by the Hex1b main loop;
        // the nudger guarantees a build actually runs.
        state.App.Invalidate();
        state.RequestExtraFrame();

        return DotsiderResponse.Ok(new MessagePayload(
            $"Search for '{request.Query}' queued on tab {tabId}"));
    }

    // --- Field Handlers ---

    private DotsiderResponse HandleListFields(DotsiderRequest request)
    {
        var fields = RequireAnalyzer().FieldDefs;
        if (!string.IsNullOrEmpty(request.TypeName))
        {
            fields = [.. fields.Where(f =>
                f.DeclaringType.Contains(request.TypeName, StringComparison.OrdinalIgnoreCase))];
        }

        if (!string.IsNullOrEmpty(request.Query))
        {
            fields = [.. fields.Where(f =>
                f.Name.Contains(request.Query, StringComparison.OrdinalIgnoreCase))];
        }

        if (request.MaxResults is > 0)
            fields = [.. fields.Take(request.MaxResults.Value)];

        return DotsiderResponse.Ok(fields);
    }

    // --- Bundle Handlers ---

    private static DotsiderResponse HandleIsBundle(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.AssemblyPath))
            return DotsiderResponse.Fail("AssemblyPath is required for is-bundle");

        var isBundle = SingleFileBundleReader.IsBundle(request.AssemblyPath, out var headerOffset);
        return DotsiderResponse.Ok(new BundleProbePayload(isBundle, headerOffset));
    }

    private static DotsiderResponse HandleGetBundleManifest(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.AssemblyPath))
            return DotsiderResponse.Fail("AssemblyPath is required for get-bundle-manifest");

        if (!SingleFileBundleReader.IsBundle(request.AssemblyPath, out var headerOffset))
            return DotsiderResponse.Fail("File is not a single-file bundle");

        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(request.AssemblyPath, headerOffset);
            return DotsiderResponse.Ok(manifest);
        }
        catch (InvalidDataException)
        {
            return DotsiderResponse.Fail("Invalid single-file bundle manifest");
        }
    }

    // --- Assembly Resolution Handlers ---

    private DotsiderResponse HandleResolveAssembly(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.AssemblyName))
            return DotsiderResponse.Fail("AssemblyName is required for resolve-assembly");

        var state = RequireState();
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            state.Analyzer.FilePath, request.AssemblyName,
            state.Analyzer.TargetFramework, state.Analyzer.PreferredRuntimePack,
            state.Analyzer.SourceBundlePath);

        if (resolved is null)
            return DotsiderResponse.Ok(null);

        var info = resolved switch
        {
            ResolvedAssembly.FromFile(var path) => new ResolvedAssemblyInfo("file", path, null, null),
            ResolvedAssembly.FromBundle(_, var name, var bundle) => new ResolvedAssemblyInfo("bundle", null, name, bundle),
            ResolvedModule module => new ResolvedAssemblyInfo("module", module.Path, null, null),
            _ => null
        };
        return DotsiderResponse.Ok(info);
    }

    // --- IL Navigation Handlers ---

    private DotsiderResponse HandleNavigateToIlDefinition(DotsiderRequest request)
    {
        var state = RequireState();
        var token = request.Token;
        if (token is null)
            return DotsiderResponse.Fail("Token is required for navigate-to-il-definition");

        state.PendingMutations.Enqueue(s =>
        {
            s.NavigateToIlDefinition(token.Value);
            s.App.Invalidate();
        });
        state.App.Invalidate();
        state.RequestExtraFrame();
        return DotsiderResponse.Ok(new OperationStatusPayload("queued"));
    }

    private DotsiderResponse HandleNavigateBack()
    {
        var state = RequireState();
        state.PendingMutations.Enqueue(s =>
        {
            // Priority 1: IL go-to-definition back
            if (s.CurrentTab == TabId.IlInspector && s.IlBackStack.Count > 0)
            {
                var entry = s.IlBackStack.Pop();
                s.RestoreFromIlBackEntry(entry);
            }
            // Priority 2: Cross-view back
            else if (s.CrossViewBackTarget is not null)
            {
                s.NavigateBack();
            }
            // Priority 3: Assembly stack pop
            else if (s.NavigationStack.Count > 0)
            {
                var backTab = s.PopAssembly();
                s.ReofferCompanionDialogsAfterPop();
                s.NavigateToTab(backTab);
                s.App.Invalidate();
            }
            // Priority 4: IL selection clear
            else if (s.CurrentTab == TabId.IlInspector
                && s.IlEditorState?.Cursor.HasSelection == true)
            {
                s.IlEditorState.Cursor.SelectionAnchor = null;
                s.App.Invalidate();
            }
        });
        state.App.Invalidate();
        state.RequestExtraFrame();
        return DotsiderResponse.Ok(new OperationStatusPayload("queued"));
    }

    private DotsiderResponse HandlePushAssembly(DotsiderRequest request)
    {
        var state = RequireState();

        if (!string.IsNullOrEmpty(request.AssemblyPath))
        {
            // Load outside the mutation to avoid blocking the render thread
            var openResult = AssemblyLoader.Open(request.AssemblyPath);
            state.PendingMutations.Enqueue(s =>
            {
                switch (openResult)
                {
                    case AssemblyOpenResult.Direct(var a):
                        s.PushAssemblyDirect(a);
                        break;
                    case AssemblyOpenResult.NativeAot(var aot):
                        s.PushAssemblyDirect(aot);
                        // The socket path has no dialog — attach eagerly when attachable,
                        // mirroring the apphost arm's silent companion redirect below.
                        if (aot.PreIlcSidecars is { HasAttachableCompanion: true })
                            s.AttachPreIlc();
                        break;
                    case AssemblyOpenResult.ApphostWithCompanion(var host, var companion):
                        host.Dispose();
                        s.PushAssembly(companion);
                        break;
                    case AssemblyOpenResult.BundleEntry(var entry, _):
                        s.PushAssemblyDirect(entry);
                        break;
                }
                s.App.Invalidate();
            });
        }
        else if (!string.IsNullOrEmpty(request.AssemblyName))
        {
            // Resolve outside the mutation to avoid blocking the render thread
            var resolved = AssemblyAnalyzer.ResolveAssembly(
                state.Analyzer.FilePath, request.AssemblyName,
                state.Analyzer.TargetFramework, state.Analyzer.PreferredRuntimePack,
                state.Analyzer.SourceBundlePath);
            if (resolved is not null)
            {
                state.PendingMutations.Enqueue(s =>
                {
                    s.PushAssembly(resolved);
                    s.App.Invalidate();
                });
            }
        }
        else
        {
            return DotsiderResponse.Fail("Either assemblyPath or assemblyName is required for push-assembly");
        }

        state.App.Invalidate();
        state.RequestExtraFrame();
        return DotsiderResponse.Ok(new OperationStatusPayload("queued"));
    }

    // --- Lifecycle ---

    /// <summary>
    /// Stops the diagnostics listener and releases its managed resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_listener is not null)
        {
            _listener.Close();
            _listener.Dispose();
        }

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; } catch { /* expected */ }
        }

        // Drain: acquire all slots. Each active handler holds one slot and will
        // release it once the CTS cancellation triggers its read timeout or
        // operation cancellation. The 5s read timeout bounds this wait.
        for (var i = 0; i < MaxConnections; i++)
            await _connectionSlots.WaitAsync();

        _connectionSlots.Dispose();

        if (_socketPath is not null && File.Exists(_socketPath))
            File.Delete(_socketPath);

        _cts.Dispose();
    }
}
