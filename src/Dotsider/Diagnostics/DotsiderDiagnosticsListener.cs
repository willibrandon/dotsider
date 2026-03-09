using System.Collections.Concurrent;
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
internal sealed class DotsiderDiagnosticsListener : IAsyncDisposable
{
    private readonly Func<DotsiderState?> _getState;
    private readonly ConcurrentQueue<Action<DotsiderState>> _pendingMutations;
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;
    private Task? _acceptLoop;
    private string? _socketPath;

    public DotsiderDiagnosticsListener(
        Func<DotsiderState?> getState,
        ConcurrentQueue<Action<DotsiderState>> pendingMutations)
    {
        _getState = getState;
        _pendingMutations = pendingMutations;
    }

    /// <summary>The path to the Unix domain socket file.</summary>
    public string? SocketPath => _socketPath;

    /// <summary>Creates the socket and starts accepting connections.</summary>
    public void StartListening()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotsider", "sockets");
        Directory.CreateDirectory(dir);

        _socketPath = Path.Combine(dir, $"{Environment.ProcessId}.dotsider.socket");

        // Clean up stale socket from a previous crash
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(5);

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
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) return;

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

            var response = HandleRequest(request);
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, DotsiderJsonOptions.Default));
        }
        catch
        {
            // Connection-level errors are silently dropped
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

                // Navigation (live session)
                "get-current-view" => HandleGetCurrentView(),
                "navigate" => HandleNavigate(request),
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
        _getState() ?? throw new InvalidOperationException("No assembly is loaded");

    private AssemblyAnalyzer RequireAnalyzer() => RequireState().Analyzer;

    // --- Assembly Handlers ---

    private DotsiderResponse HandleAssemblyInfo()
    {
        var a = RequireAnalyzer();
        return DotsiderResponse.Ok(new
        {
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
            .Select(t => new { Kind = "type", t.FullName, t.Namespace });

        var matchingMethods = analyzer.MethodDefs
            .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .Select(m => new { Kind = "method", m.Name, m.DeclaringType, m.Signature });

        var matchingMemberRefs = analyzer.MemberRefs
            .Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .Select(r => new { Kind = "memberRef", r.Name, r.DeclaringType, r.Signature });

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
            m.DeclaringType.Equals(request.TypeName, StringComparison.OrdinalIgnoreCase)
            && m.Name.Equals(request.MethodName, StringComparison.OrdinalIgnoreCase));

        if (method is null)
            return DotsiderResponse.Fail($"Method not found: {request.TypeName}.{request.MethodName}");

        var instructions = state.IlDisassembler.Disassemble(method);
        return DotsiderResponse.Ok(new { Method = method, Instructions = instructions });
    }

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
                instructions = state.IlDisassembler.Disassemble(method);
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
        var (nodes, edges) = DependencyGraphBuilder.Build(analyzer);
        return DotsiderResponse.Ok(new { Nodes = nodes, Edges = edges });
    }

    private DotsiderResponse HandleGetTypeRefs() =>
        DotsiderResponse.Ok(RequireAnalyzer().TypeRefs);

    // --- Size Handlers ---

    private DotsiderResponse HandleGetSizeTree()
    {
        var state = RequireState();
        var tree = SizeAnalyzer.BuildSizeTree(state.Analyzer, state.IlDisassembler);
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

    private DotsiderResponse HandleDiff(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.LeftPath) || string.IsNullOrEmpty(request.RightPath))
            return DotsiderResponse.Fail("LeftPath and RightPath are required for diff");

        using var left = new AssemblyAnalyzer(request.LeftPath);
        using var right = new AssemblyAnalyzer(request.RightPath);
        var result = AssemblyDiffer.Compare(left, right);
        return DotsiderResponse.Ok(result);
    }

    // --- NuGet Handler ---

    private DotsiderResponse HandleAnalyzeNupkg(DotsiderRequest request)
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
        if (!state.HasEntryPoint)
            return DotsiderResponse.Fail("Assembly has no entry point");

        if (state.Tracer?.ProcessState == TraceProcessState.Running)
            return DotsiderResponse.Fail("A trace is already running");

        _pendingMutations.Enqueue(s =>
        {
            var args = request.Arguments ?? "";
            s.Tracer?.Dispose();
            s.Tracer = new RuntimeTracer(
                s.Analyzer.FilePath, args, () => s.App.Invalidate());
            s.Tracer.Start();
        });

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
        var state = RequireState();
        return DotsiderResponse.Ok(new
        {
            Tab = state.CurrentTab,
            state.PeSubTab,
            state.DynamicSubTab,
            AssemblyPath = state.Analyzer.FilePath,
            NavigationDepth = state.NavigationStack.Count,
            TracerState = state.Tracer?.ProcessState.ToString()
        });
    }

    private DotsiderResponse HandleNavigate(DotsiderRequest request)
    {
        if (request.TabId is null)
            return DotsiderResponse.Fail("TabId is required for navigate");

        var tabId = request.TabId.Value;
        _pendingMutations.Enqueue(s =>
        {
            s.NavigateToTab(tabId);
            s.App.Invalidate();
        });

        return DotsiderResponse.Ok(new { Message = $"Navigation to tab {tabId} queued" });
    }

    private DotsiderResponse HandleSearch(DotsiderRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return DotsiderResponse.Fail("Query is required for search");

        var state = RequireState();
        var tabId = request.TabId ?? state.CurrentTab;

        _pendingMutations.Enqueue(s =>
        {
            var search = s.Search[tabId];
            if (!search.IsActive)
                search.ActivateOrCycle();
            search.UpdateQuery(request.Query);
            search.Confirm();
            s.App.Invalidate();
        });

        return DotsiderResponse.Ok(new { Message = $"Search for '{request.Query}' queued on tab {tabId}" });
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

        if (_socketPath is not null && File.Exists(_socketPath))
            File.Delete(_socketPath);

        _cts.Dispose();
    }
}
