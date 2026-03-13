using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class SessionToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    // Use PIDs that won't collide with real processes or other test classes
    private static int s_nextPid = 999_700;

    [Fact]
    public async Task DiscoverDotsiderSessions_FindsRunningInstance()
    {
        await using var socket = new TestDotsiderSocket(999_999, "/tmp/test/HelloWorld.dll");
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        // Should find our test instance in the JSON array
        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.NotNull(sessions);

        var testSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == 999_999);
        Assert.NotEqual(default, testSession);
        Assert.Equal(999_999, testSession.GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task GetSessionInfo_ReturnsAssemblyAndViewData()
    {
        await using var socket = new TestDotsiderSocket(999_998, "/tmp/test/HelloWorld.dll");

        // Add a get-current-view handler
        socket.OnMethod("get-current-view", _ => DotsiderResponse.Ok(new
        {
            Tab = 0,
            AssemblyPath = "/tmp/test/HelloWorld.dll"
        }));

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_session_info",
            new Dictionary<string, object?> { ["sessionId"] = 999_998 },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.True(doc.RootElement.TryGetProperty("assembly", out _));
        Assert.True(doc.RootElement.TryGetProperty("view", out _));
    }

    // --- Diff mode: real DotsiderDiagnosticsListener with real AssemblyAnalyzers ---

    [Fact]
    public async Task DiscoverDotsiderSessions_FindsDiffModeInstance()
    {
        var (pid, listener, analyzers) = CreateRealDiffListener();
        await using var listenerGuard = listener;
        using var analyzerGuard = analyzers;

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.NotNull(sessions);

        var diffSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == pid);
        Assert.NotEqual(default, diffSession);
        Assert.Equal("diff", diffSession.GetProperty("info").GetProperty("mode").GetString());
        Assert.True(diffSession.GetProperty("info").TryGetProperty("left", out _));
        Assert.True(diffSession.GetProperty("info").TryGetProperty("right", out _));
    }

    [Fact]
    public async Task GetSessionInfo_DiffMode_ReturnsBothAssemblyAndView()
    {
        var (pid, listener, analyzers) = CreateRealDiffListener();
        await using var listenerGuard = listener;
        using var analyzerGuard = analyzers;

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_session_info",
            new Dictionary<string, object?> { ["sessionId"] = pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);

        // Assembly info from the real listener's assemblyInfoProvider
        var assembly = doc.RootElement.GetProperty("assembly");
        Assert.Equal("diff", assembly.GetProperty("mode").GetString());
        Assert.Equal("RichLibrary", assembly.GetProperty("left").GetProperty("assemblyName").GetString());
        Assert.Equal("RichLibrary", assembly.GetProperty("right").GetProperty("assemblyName").GetString());

        // View from the real listener's currentViewProvider
        var view = doc.RootElement.GetProperty("view");
        Assert.Equal("diff", view.GetProperty("mode").GetString());
        Assert.True(view.TryGetProperty("tab", out _));
        Assert.True(view.TryGetProperty("filterMode", out _));
    }

    // --- NuGet mode: real DotsiderDiagnosticsListener with real NuGetPackageAnalyzer ---

    [Fact]
    public async Task DiscoverDotsiderSessions_FindsNugetModeInstance()
    {
        var (pid, listener, package) = CreateRealNugetListener();
        await using var listenerGuard = listener;
        using var packageGuard = package;

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.NotNull(sessions);

        var nugetSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == pid);
        Assert.NotEqual(default, nugetSession);
        Assert.Equal("nuget", nugetSession.GetProperty("info").GetProperty("mode").GetString());
        Assert.Equal("RichLibrary",
            nugetSession.GetProperty("info").GetProperty("packageId").GetString());
    }

    [Fact]
    public async Task GetSessionInfo_NugetMode_ReturnsBothAssemblyAndView()
    {
        var (pid, listener, package) = CreateRealNugetListener();
        await using var listenerGuard = listener;
        using var packageGuard = package;

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_session_info",
            new Dictionary<string, object?> { ["sessionId"] = pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);

        // Assembly info from the real listener's assemblyInfoProvider
        var assembly = doc.RootElement.GetProperty("assembly");
        Assert.Equal("nuget", assembly.GetProperty("mode").GetString());
        Assert.Equal("RichLibrary", assembly.GetProperty("packageId").GetString());
        Assert.Equal("2.5.1", assembly.GetProperty("packageVersion").GetString());
        Assert.True(assembly.GetProperty("dllCount").GetInt32() > 0);

        // View from the real listener's currentViewProvider
        var view = doc.RootElement.GetProperty("view");
        Assert.Equal("nuget", view.GetProperty("mode").GetString());
        Assert.True(view.GetProperty("isBrowsingPackage").GetBoolean());
    }

    // --- Helpers ---

    private (int pid, DotsiderDiagnosticsListener listener, AnalyzerPair analyzers)
        CreateRealDiffListener()
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
            {
                Mode = "diff",
                FileName = $"{left.FileName} \u2194 {right.FileName}",
                Left = new
                {
                    left.FilePath,
                    left.FileName,
                    left.FileSize,
                    left.AssemblyName,
                    left.AssemblyVersion,
                    left.TargetFramework,
                },
                Right = new
                {
                    right.FilePath,
                    right.FileName,
                    right.FileSize,
                    right.AssemblyName,
                    right.AssemblyVersion,
                    right.TargetFramework,
                },
            },
            currentViewProvider: () => new
            {
                Mode = "diff",
                Tab = 0,
                FilterMode = DiffFilterMode.All,
            });
        listener.StartListening(overridePid: pid);

        return (pid, listener, new AnalyzerPair(left, right));
    }

    private (int pid, DotsiderDiagnosticsListener listener, NuGetPackageAnalyzer package)
        CreateRealNugetListener()
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var package = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => new
            {
                Mode = "nuget",
                package.FilePath,
                package.FileName,
                package.PackageId,
                package.PackageVersion,
                package.Authors,
                package.Description,
                DllCount = package.DllFiles.Count,
            },
            currentViewProvider: () => new
            {
                Mode = "nuget",
                IsBrowsingPackage = true,
                Tab = (int?)null,
                SelectedDll = (string?)null,
            });
        listener.StartListening(overridePid: pid);

        return (pid, listener, package);
    }

    /// <summary>Holds two analyzers for disposal.</summary>
    private sealed class AnalyzerPair(
        AssemblyAnalyzer left, AssemblyAnalyzer right) : IDisposable
    {
        public void Dispose()
        {
            left.Dispose();
            right.Dispose();
        }
    }
}
