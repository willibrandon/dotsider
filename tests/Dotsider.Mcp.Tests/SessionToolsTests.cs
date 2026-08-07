using Dotsider.Core.Analysis;
using Dotsider.Diagnostics;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for session discovery and introspection MCP tools backed by diagnostics sockets.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class SessionToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    // Use PIDs that won't collide with real processes or other test classes
    private static int s_nextPid = 999_700;

    /// <summary>
    /// discover_dotsider_sessions picks up a simulated listening instance by PID.
    /// </summary>
    [TestMethod]
    public async Task DiscoverDotsiderSessions_FindsRunningInstance()
    {
        await using var socket = new TestDotsiderSocket(999_999, "/tmp/test/HelloWorld.dll");
        socket.Start();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);

        // Should find our test instance in the JSON array
        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.IsNotNull(sessions);

        var testSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == 999_999);
        Assert.AreNotEqual(default, testSession);
        Assert.AreEqual(999_999, testSession.GetProperty("pid").GetInt32());
    }

    /// <summary>
    /// get_session_info combines the remote assembly-info and current-view payloads into one response.
    /// </summary>
    [TestMethod]
    public async Task GetSessionInfo_ReturnsAssemblyAndViewData()
    {
        await using var socket = new TestDotsiderSocket(999_998, "/tmp/test/HelloWorld.dll");

        // Add a get-current-view handler
        socket.OnMethod("get-current-view", _ => TestJsonResponse.Ok(new
        {
            Tab = 0,
            AssemblyPath = "/tmp/test/HelloWorld.dll"
        }));
        socket.Start();

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_session_info",
            new Dictionary<string, object?> { ["sessionId"] = 999_998 },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.IsTrue(doc.RootElement.TryGetProperty("assembly", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("view", out _));
    }

    // --- Diff mode: real DotsiderDiagnosticsListener with real AssemblyAnalyzers ---

    /// <summary>
    /// Discovery surfaces a real diff-mode listener and exposes both left/right assembly metadata.
    /// </summary>
    [TestMethod]
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
        Assert.IsNotNull(text);

        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.IsNotNull(sessions);

        var diffSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == pid);
        Assert.AreNotEqual(default, diffSession);
        Assert.AreEqual("diff", diffSession.GetProperty("info").GetProperty("mode").GetString());
        Assert.IsTrue(diffSession.GetProperty("info").TryGetProperty("left", out _));
        Assert.IsTrue(diffSession.GetProperty("info").TryGetProperty("right", out _));
    }

    /// <summary>
    /// Diff-mode session info carries left/right assembly names plus the current tab and filter mode.
    /// </summary>
    [TestMethod]
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
        Assert.IsNotNull(text);

        var doc = JsonDocument.Parse(text!);

        // Assembly info from the real listener's assemblyInfoProvider
        var assembly = doc.RootElement.GetProperty("assembly");
        Assert.AreEqual("diff", assembly.GetProperty("mode").GetString());
        Assert.AreEqual("RichLibrary", assembly.GetProperty("left").GetProperty("assemblyName").GetString());
        Assert.AreEqual("RichLibrary", assembly.GetProperty("right").GetProperty("assemblyName").GetString());

        // View from the real listener's currentViewProvider
        var view = doc.RootElement.GetProperty("view");
        Assert.AreEqual("diff", view.GetProperty("mode").GetString());
        Assert.IsTrue(view.TryGetProperty("tab", out _));
        Assert.IsTrue(view.TryGetProperty("filterMode", out _));
    }

    // --- NuGet mode: real DotsiderDiagnosticsListener with real NuGetPackageAnalyzer ---

    /// <summary>
    /// Discovery surfaces a real NuGet-mode listener and includes its packageId in the info.
    /// </summary>
    [TestMethod]
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
        Assert.IsNotNull(text);

        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.IsNotNull(sessions);

        var nugetSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == pid);
        Assert.AreNotEqual(default, nugetSession);
        Assert.AreEqual("nuget", nugetSession.GetProperty("info").GetProperty("mode").GetString());
        Assert.AreEqual("RichLibrary",
            nugetSession.GetProperty("info").GetProperty("packageId").GetString());
    }

    /// <summary>
    /// NuGet-mode session info reports package metadata alongside the browsing-package view state.
    /// </summary>
    [TestMethod]
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
        Assert.IsNotNull(text);

        var doc = JsonDocument.Parse(text!);

        // Assembly info from the real listener's assemblyInfoProvider
        var assembly = doc.RootElement.GetProperty("assembly");
        Assert.AreEqual("nuget", assembly.GetProperty("mode").GetString());
        Assert.AreEqual("RichLibrary", assembly.GetProperty("packageId").GetString());
        Assert.AreEqual("2.5.1", assembly.GetProperty("packageVersion").GetString());
        Assert.IsGreaterThan(0, assembly.GetProperty("dllCount").GetInt32());

        // View from the real listener's currentViewProvider
        var view = doc.RootElement.GetProperty("view");
        Assert.AreEqual("nuget", view.GetProperty("mode").GetString());
        Assert.IsTrue(view.GetProperty("isBrowsingPackage").GetBoolean());
    }

    // --- Helpers ---

    private static (int pid, DotsiderDiagnosticsListener listener, AnalyzerPair analyzers)
        CreateRealDiffListener()
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => TestJsonResponse.Element(new
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
            }),
            currentViewProvider: () => TestJsonResponse.Element(new
            {
                Mode = "diff",
                Tab = 1,
                FilterMode = DiffFilterMode.All,
            }));
        listener.StartListening(overridePid: pid);

        return (pid, listener, new AnalyzerPair(left, right));
    }

    private static (int pid, DotsiderDiagnosticsListener listener, NuGetPackageAnalyzer package)
        CreateRealNugetListener()
    {
        var pid = Interlocked.Increment(ref s_nextPid);
        var package = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);

        var listener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () => TestJsonResponse.Element(new
            {
                Mode = "nuget",
                package.FilePath,
                package.FileName,
                package.PackageId,
                package.PackageVersion,
                package.Authors,
                package.Description,
                DllCount = package.DllFiles.Count,
            }),
            currentViewProvider: () => TestJsonResponse.Element(new
            {
                Mode = "nuget",
                IsBrowsingPackage = true,
                Tab = (int?)null,
                SelectedDll = (string?)null,
            }));
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
