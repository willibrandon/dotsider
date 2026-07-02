using System.Net.Sockets;
using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;

namespace Dotsider.Diagnostics;

/// <summary>
/// Listens on a Unix domain socket at ~/.dotsider/sockets/{pid}.dotsider.socket
/// and serves analysis state from the running TUI.
/// </summary>
internal sealed class DotsiderDiagnosticsListener(
    Func<DotsiderState?> getState,
    Func<object?>? assemblyInfoProvider = null,
    Func<object?>? currentViewProvider = null) : IAsyncDisposable
{
    private const int MaxConnections = 4;

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
                using var r = new StreamReader(s, leaveOpen: true);
                await using var w = new StreamWriter(s, leaveOpen: true) { AutoFlush = true };

                // Read and discard the client's request before responding
                // to avoid EPIPE on the client side
                using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await r.ReadLineAsync(readCts.Token); } catch { /* timeout or error */ }

                var rejection = DotsiderResponse.Fail(
                    $"Connection rejected: too many concurrent connections (limit: {MaxConnections})");
                await w.WriteLineAsync(
                    JsonSerializer.Serialize(rejection, DotsiderJsonOptions.Default));
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
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            // 2. Read with timeout to prevent stalled clients from pinning slots
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            readCts.CancelAfter(TimeSpan.FromSeconds(5));

            string? line;
            try
            {
                line = await reader.ReadLineAsync(readCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(line)) return;

            // 3. Peer credential check (after read so the client's write completes
            //    before we close — avoids EPIPE on the client side)
            if (ForceRejectPeers || !_peerVerifier!.IsSameUser(client))
            {
                var rejection = DotsiderResponse.Fail(
                    "Connection rejected: peer is not the same user");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(rejection, DotsiderJsonOptions.Default));
                return;
            }

            // 4. Deserialize ([JsonRequired] catches missing "v")
            DotsiderRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<DotsiderRequest>(line, DotsiderJsonOptions.Default);
            }
            catch (JsonException ex)
            {
                var errorResponse = DotsiderResponse.Fail($"Invalid JSON: {ex.Message}");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonOptions.Default));
                return;
            }

            if (request is null)
            {
                var errorResponse = DotsiderResponse.Fail("Empty request");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonOptions.Default));
                return;
            }

            // 5. Protocol version check (catches wrong-but-present "v")
            if (request.V != DotsiderProtocol.Version)
            {
                var errorResponse = DotsiderResponse.Fail(
                    $"Protocol version mismatch: expected {DotsiderProtocol.Version}, got {request.V}");
                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(errorResponse, DotsiderJsonOptions.Default));
                return;
            }

            // 6. Route to handler
            var response = HandleRequest(request);
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, DotsiderJsonOptions.Default));
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

                // Diff
                "diff" => HandleDiff(request),

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
        return DotsiderResponse.Ok(new
        {
            Mode = "standard",
            a.FilePath,
            a.FileName,
            a.FileSize,
            a.AssemblyName,
            a.AssemblyVersion,
            a.TargetFramework,
            a.Culture,
            a.PublicKeyToken,
            a.Architecture,
            a.HasMetadata,
            a.DisplayName,
            a.SourceBundlePath,
            a.IsBundleBacked,
            a.LaunchPath,
            a.CanSaveInPlace,
            a.PreferredRuntimePack,
            a.PdbProvenance,
            a.SourceLink,
            TypeCount = a.TypeDefs.Count,
            MethodCount = a.MethodDefs.Count,
            AssemblyRefCount = a.AssemblyRefs.Count
        });
    }

    private DotsiderResponse HandleListTypes(DotsiderRequest request)
    {
        var types = RequireAnalyzer().TypeDefs;
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
        var methods = RequireAnalyzer().MethodDefs;
        if (!string.IsNullOrEmpty(request.TypeName))
        {
            methods = [.. methods.Where(m =>
                m.DeclaringType.Contains(request.TypeName, StringComparison.OrdinalIgnoreCase))];
        }

        if (!string.IsNullOrEmpty(request.Query))
        {
            methods = [.. methods.Where(m =>
                m.Name.Contains(request.Query, StringComparison.OrdinalIgnoreCase))];
        }

        if (request.MaxResults is > 0)
            methods = [.. methods.Take(request.MaxResults.Value)];

        return DotsiderResponse.Ok(methods);
    }

    private DotsiderResponse HandleFindMembers(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return DotsiderResponse.Fail("Query is required for find-members");

        var analyzer = RequireAnalyzer();
        var query = request.Query;
        var max = request.MaxResults ?? 100;

        var matchingTypes = analyzer.TypeDefs
            .Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();

        var matchingMethods = analyzer.MethodDefs
            .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();

        var matchingMemberRefs = analyzer.MemberRefs
            .Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();

        return DotsiderResponse.Ok(new
        {
            Types = matchingTypes,
            Methods = matchingMethods,
            MemberRefs = matchingMemberRefs
        });
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
        return DotsiderResponse.Ok(new
        {
            Method = method,
            Pdb = state.Analyzer.PdbProvenance,
            state.Analyzer.SourceLink,
            DebugInfo = request.IncludeDebugInfo ? state.Analyzer.GetMethodDebugInfo(method) : null,
            Instructions = instructions
        });
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
        var results = new List<object>();

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
                results.Add(new
                {
                    Method = $"{method.DeclaringType}.{method.Name}",
                    Matches = matches
                });
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
        return DotsiderResponse.Ok(new { Token = request.Token.Value, Resolved = resolved });
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
        return DotsiderResponse.Ok(new
        {
            Offset = offset,
            Length = length,
            Hex = Convert.ToHexString(bytes),
            Base64 = Convert.ToBase64String(bytes)
        });
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

        if (!string.IsNullOrEmpty(request.Query))
        {
            bool Match(StringEntry e) =>
                e.Value.Contains(request.Query, StringComparison.OrdinalIgnoreCase);

            user = [.. user.Where(Match)];
            metadata = [.. metadata.Where(Match)];
            raw = [.. raw.Where(Match)];
        }

        var max = request.MaxResults ?? int.MaxValue;
        return DotsiderResponse.Ok(new
        {
            UserStrings = user.Take(max),
            MetadataStrings = metadata.Take(max),
            RawStrings = raw.Take(max)
        });
    }

    // --- Dependency Handlers ---

    private DotsiderResponse HandleGetAssemblyRefs() =>
        DotsiderResponse.Ok(RequireAnalyzer().AssemblyRefs);

    private DotsiderResponse HandleGetDependencyGraph()
    {
        var analyzer = RequireAnalyzer();
        var graph = DependencyGraphBuilder.Build(analyzer);
        return DotsiderResponse.Ok(new { graph.Nodes, graph.Edges });
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
        var state = RequireState();
        var max = request.MaxResults ?? 20;
        var methods = state.Analyzer.MethodDefs
            .Select(m =>
            {
                try
                {
                    var body = state.Analyzer.GetMethodBody(m);
                    return new { Method = m, Size = body?.GetILBytes()?.Length ?? 0 };
                }
                catch
                {
                    return new { Method = m, Size = 0 };
                }
            })
            .OrderByDescending(x => x.Size)
            .Take(max);

        return DotsiderResponse.Ok(methods);
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

    // --- NuGet Handler ---

    private static DotsiderResponse HandleAnalyzeNupkg(DotsiderRequest request)
    {
        var path = request.AssemblyPath;
        if (string.IsNullOrEmpty(path))
            return DotsiderResponse.Fail("AssemblyPath is required for analyze-nupkg");

        using var package = new NuGetPackageAnalyzer(path);
        return DotsiderResponse.Ok(new
        {
            package.PackageId,
            package.PackageVersion,
            package.Authors,
            package.Description,
            package.DllFiles
        });
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
            var args = request.Arguments ?? "";
            s.Tracer?.Dispose();
            s.Tracer = new RuntimeTracer(
                s.Analyzer.LaunchPath, args, () => s.App.Invalidate());
            s.Tracer.Start();
        });

        // Trigger a render frame so the mutation queue gets drained
        state.App.Invalidate();

        return DotsiderResponse.Ok(new { Message = "Trace start queued" });
    }

    private DotsiderResponse HandleStopTrace()
    {
        var tracer = RequireState().Tracer;
        if (tracer is null)
            return DotsiderResponse.Fail("No trace session is active");

        if (tracer.ProcessState != TraceProcessState.Running)
            return DotsiderResponse.Fail("Trace is not running");

        tracer.Stop();
        return DotsiderResponse.Ok(new { Message = "Trace stopped" });
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
        return DotsiderResponse.Ok(new
        {
            Tab = state.CurrentTab + 1,
            state.PeSubTab,
            state.DynamicSubTab,
            AssemblyPath = state.Analyzer.FilePath,
            NavigationDepth = state.NavigationStack.Count,
            TracerState = state.Tracer?.ProcessState.ToString(),
            state.HexIsDirty,
            state.HasEntryPoint,
            state.IsNativeAot,
            state.IsNetFramework
        });
    }

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

        // Trigger a render frame so the mutation queue gets drained
        state.App.Invalidate();

        return DotsiderResponse.Ok(new { Message = $"Navigation to tab {tabId} queued" });
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

        // Trigger a render frame so the mutation queue gets drained
        state.App.Invalidate();

        return DotsiderResponse.Ok(new { Message = $"Search for '{request.Query}' queued on tab {tabId}" });
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
        return DotsiderResponse.Ok(new { IsBundle = isBundle, HeaderOffset = headerOffset });
    }

    private static DotsiderResponse HandleGetBundleManifest(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.AssemblyPath))
            return DotsiderResponse.Fail("AssemblyPath is required for get-bundle-manifest");

        if (!SingleFileBundleReader.IsBundle(request.AssemblyPath, out var headerOffset))
            return DotsiderResponse.Fail("File is not a single-file bundle");

        var manifest = SingleFileBundleReader.ReadManifest(request.AssemblyPath, headerOffset);
        return DotsiderResponse.Ok(manifest);
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
        return DotsiderResponse.Ok(new { Status = "queued" });
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
                if (s.ApphostCompanionDllPath is not null && !s.Analyzer.HasMetadata)
                    s.ApphostDialogOpen = true;
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
        return DotsiderResponse.Ok(new { Status = "queued" });
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
        return DotsiderResponse.Ok(new { Status = "queued" });
    }

    // --- Lifecycle ---

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
