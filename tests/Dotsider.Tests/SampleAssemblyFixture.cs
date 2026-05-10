using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// xUnit fixture that builds and exposes paths to all sample assemblies shared across tests.
/// </summary>
public class SampleAssemblyFixture : IAsyncLifetime
{
    private string _repoRoot = null!;

    // Exe samples (have both .dll and apphost binary)
    /// <summary>
    /// Path to the built HelloWorld.dll managed assembly.
    /// </summary>
    public string HelloWorldDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built HelloWorld apphost executable.
    /// </summary>
    public string HelloWorldExe { get; private set; } = null!;
    /// <summary>
    /// Path to the built ComplexApp.dll sample assembly.
    /// </summary>
    public string ComplexAppDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built ComplexApp apphost executable.
    /// </summary>
    public string ComplexAppExe { get; private set; } = null!;
    /// <summary>
    /// Path to the built MinimalApi.dll sample assembly.
    /// </summary>
    public string MinimalApiDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built MinimalApi apphost executable.
    /// </summary>
    public string MinimalApiExe { get; private set; } = null!;

    // Library samples
    /// <summary>
    /// Path to the built RichLibrary.dll sample assembly.
    /// </summary>
    public string RichLibraryDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built RichLibrary v2 sample assembly.
    /// </summary>
    public string RichLibraryV2Dll { get; private set; } = null!;
    /// <summary>
    /// Path to the built NativeLib.dll sample assembly.
    /// </summary>
    public string NativeLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the built EmptyLib.dll sample assembly.
    /// </summary>
    public string EmptyLibDll { get; private set; } = null!;

    /// <summary>
    /// Path to the built AppLocalRollForward.dll sample assembly. Reproduces the AppLocal
    /// framework-PKT roll-forward scenario: Microsoft.Diagnostics.Tracing.TraceEvent's
    /// compiled AssemblyRef targets <c>Microsoft.Diagnostics.NETCore.Client v0.2.10.10501</c>,
    /// while the package-restored runtime asset on disk is a strictly higher build of the
    /// same simple name and PKT.
    /// </summary>
    public string AppLocalRollForwardDll { get; private set; } = null!;

    // NuGet package
    /// <summary>
    /// Path to the built RichLibrary NuGet package.
    /// </summary>
    public string RichLibraryNupkg { get; private set; } = null!;

    // .NET Framework sample (Windows only — net48 requires Windows)
    /// <summary>
    /// Path to the built .NET Framework sample executable (Windows only).
    /// </summary>
    public string? NetFxConsoleExe { get; private set; }

    /// <summary>
    /// Path to the built NetFxBindingRedirects sample executable (Windows only).
    /// </summary>
    public string? NetFxBindingRedirectsExe { get; private set; }

    /// <summary>
    /// Map of interesting assembly references in the NetFxBindingRedirects sample to the
    /// runtime oracle entry recording what the actual .NET Framework CLR loaded for them.
    /// Tests use this dictionary to enforce literal CLR accuracy on dotsider's NetFxBinder.
    /// <see langword="null"/> on non-Windows.
    /// </summary>
    public IReadOnlyDictionary<string, NetFxOracleEntry>? NetFxBindingRedirectsOracle { get; private set; }

    /// <summary>
    /// Path to the built NetFxBindingRedirects.Clr2 sample executable (Windows only). The CLR 2 /
    /// .NET Framework 3.5 sibling of <see cref="NetFxBindingRedirectsExe"/>; exercises the GAC
    /// at <c>%WINDIR%\assembly</c>, framework runtime <c>v2.0.50727</c>, no-prefix GAC tokens,
    /// and the v3.0 reference-assemblies allowlist via WindowsBase.
    /// </summary>
    public string? NetFxBindingRedirectsClr2Exe { get; private set; }

    /// <summary>
    /// Map of interesting assembly references in the NetFxBindingRedirects.Clr2 sample to the
    /// runtime oracle entry recording what the actual CLR 2.0 runtime loaded for them.
    /// <see langword="null"/> when CLR 2 isn't present on the host (see <see cref="Clr2RuntimePresent"/>)
    /// or on non-Windows.
    /// </summary>
    public IReadOnlyDictionary<string, NetFxOracleEntry>? NetFxBindingRedirectsClr2Oracle { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>mscorlib.dll</c> exists under the architecture-correct
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727</c> directory, i.e. the .NET Framework
    /// 3.5 runtime is installed. Tests that probe live CLR 2 paths gate on this; synthetic
    /// temp-tree tests do not.
    /// </summary>
    public bool Clr2RuntimePresent { get; private set; }

    // Dotted assembly name sample (e.g., Company.Product.Tool)
    /// <summary>
    /// Path to the dotted-name sample assembly (Dotted.Name.App.dll).
    /// </summary>
    public string DottedNameAppDll { get; private set; } = null!;
    /// <summary>
    /// Path to the dotted-name sample apphost executable.
    /// </summary>
    public string DottedNameAppExe { get; private set; } = null!;

    // NativeAOT sample
    /// <summary>
    /// Path to the published NativeAOT sample executable.
    /// </summary>
    public string? NativeAotConsoleExe { get; private set; }

    // Self-contained single-file sample
    /// <summary>
    /// Path to the published self-contained single-file sample executable.
    /// </summary>
    public string? SelfContainedConsoleExe { get; private set; }

    // Non-.NET binary for error case testing
    /// <summary>
    /// Path to a small non-.NET binary used for BadImageFormatException scenarios.
    /// </summary>
    public string NonDotNetBinaryPath { get; private set; } = null!;

    /// <summary>
    /// Builds all sample projects and materializes the fixture paths.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _repoRoot = TestHelpers.GetRepoRoot();

        // Build core samples in parallel
        var builds = new List<Task>
        {
            BuildProject("samples/HelloWorld"),
            BuildProject("samples/RichLibrary"),
            BuildProject("samples/RichLibraryV2"),
            BuildProject("samples/ComplexApp"),
            BuildProject("samples/MinimalApi"),
            BuildProject("samples/NativeLib"),
            BuildProject("samples/EmptyLib"),
            BuildProject("samples/Dotted.Name.App"),
            BuildProject("samples/AppLocalRollForward"),
        };

        // net48 needs Windows; NativeAOT builds on all platforms.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builds.Add(BuildProject("samples/NetFxConsole"));
            // Build only the EXE — its csproj's AfterTargets runs MSBuild on the five helper
            // projects and copies their outputs into lib/, external/, fr/. Building the helpers
            // separately would race with the EXE's post-build copy.
            builds.Add(BuildProject("samples/NetFxBindingRedirects"));
            // CLR 2 sibling — same shape, AfterTargets stages its own helpers.
            builds.Add(BuildProject("samples/NetFxBindingRedirects.Clr2"));
        }

        builds.Add(PublishNativeAotProject("samples/NativeAotConsole"));
        builds.Add(PublishSelfContainedProject("samples/SelfContainedConsole"));

        await Task.WhenAll(builds);

        var config = "Debug";
        var tfm = "net10.0";
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, $"HelloWorld{apphostExt}");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        ComplexAppExe = SamplePath("ComplexApp", config, tfm, $"ComplexApp{apphostExt}");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        MinimalApiExe = SamplePath("MinimalApi", config, tfm, $"MinimalApi{apphostExt}");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");
        AppLocalRollForwardDll = SamplePath(
            "AppLocalRollForward", config, tfm, "AppLocalRollForward.dll");
        DottedNameAppDll = SamplePath("Dotted.Name.App", config, tfm, "Dotted.Name.App.dll");
        DottedNameAppExe = SamplePath("Dotted.Name.App", config, tfm, $"Dotted.Name.App{apphostExt}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NetFxConsoleExe = Path.Combine(_repoRoot, "samples", "NetFxConsole",
                "bin", config, "net48", "NetFxConsole.exe");

            NetFxBindingRedirectsExe = Path.Combine(_repoRoot, "samples", "NetFxBindingRedirects",
                "bin", config, "net48", "NetFxBindingRedirects.exe");

            NetFxBindingRedirectsOracle = await CaptureNetFxOracleAsync(NetFxBindingRedirectsExe);

            NetFxBindingRedirectsClr2Exe = Path.Combine(_repoRoot, "samples", "NetFxBindingRedirects.Clr2",
                "bin", config, "net35", "NetFxBindingRedirects.Clr2.exe");

            // CLR 2 runtime detection: arch-aware. Either Framework64 or Framework slot may host
            // the v2.0.50727 mscorlib. Test the architecture-correct slot first; accept the other
            // as a fallback for x86-only hosts.
            var windir = Environment.GetEnvironmentVariable("WINDIR");
            if (!string.IsNullOrEmpty(windir))
            {
                Clr2RuntimePresent =
                    File.Exists(Path.Combine(windir!, "Microsoft.NET", "Framework64", "v2.0.50727", "mscorlib.dll"))
                    || File.Exists(Path.Combine(windir!, "Microsoft.NET", "Framework", "v2.0.50727", "mscorlib.dll"));
            }

            // Run the runtime oracle only when CLR 2 is installed; otherwise the EXE either
            // shims to CLR 4 (defeating the test) or fails to start. CLR 2 runtime-dependent
            // tests skip when the oracle map is null.
            if (Clr2RuntimePresent)
                NetFxBindingRedirectsClr2Oracle = await CaptureNetFxOracleAsync(NetFxBindingRedirectsClr2Exe);
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        NativeAotConsoleExe = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
            "bin", "Release", tfm, rid, "publish", $"NativeAotConsole{apphostExt}");

        SelfContainedConsoleExe = Path.Combine(_repoRoot, "samples", "SelfContainedConsole",
            "bin", "Release", tfm, rid, "publish", $"SelfContainedConsole{apphostExt}");

        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        // Create non-.NET binary for BadImageFormatException testing
        NonDotNetBinaryPath = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(NonDotNetBinaryPath, [0xDE, 0xAD, 0xBE, 0xEF]);

        // Verify critical paths exist
        Assert.True(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
        Assert.True(File.Exists(AppLocalRollForwardDll),
            $"AppLocalRollForward.dll not found at {AppLocalRollForwardDll}");
        var rollForwardBin = Path.GetDirectoryName(AppLocalRollForwardDll)!;
        Assert.True(File.Exists(Path.Combine(rollForwardBin, "Microsoft.Diagnostics.NETCore.Client.dll")),
            "AppLocalRollForward must deploy NETCore.Client.dll app-local for the roll-forward probe");
        Assert.True(File.Exists(Path.Combine(rollForwardBin, "Microsoft.Diagnostics.Tracing.TraceEvent.dll")),
            "AppLocalRollForward must deploy TraceEvent.dll app-local for its stale AssemblyRef to drive the test");
        if (NetFxConsoleExe is not null)
            Assert.True(File.Exists(NetFxConsoleExe), $"NetFxConsole.exe not found at {NetFxConsoleExe}");
        if (NetFxBindingRedirectsExe is not null)
        {
            Assert.True(File.Exists(NetFxBindingRedirectsExe),
                $"NetFxBindingRedirects.exe not found at {NetFxBindingRedirectsExe}");
            var binDir = Path.GetDirectoryName(NetFxBindingRedirectsExe)!;
            Assert.True(File.Exists(Path.Combine(binDir, "NetFxBindingRedirects.exe.config")),
                "NetFxBindingRedirects.exe.config missing — app.config did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "lib")),
                "lib/ subdir missing — privatePath helper did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "external")),
                "external/ subdir missing — codeBase helper did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "fr")),
                "fr/ subdir missing — culture satellite did not deploy");
            Assert.NotNull(NetFxBindingRedirectsOracle);
        }
        if (NetFxBindingRedirectsClr2Exe is not null)
        {
            Assert.True(File.Exists(NetFxBindingRedirectsClr2Exe),
                $"NetFxBindingRedirects.Clr2.exe not found at {NetFxBindingRedirectsClr2Exe}");
            var binDir = Path.GetDirectoryName(NetFxBindingRedirectsClr2Exe)!;
            Assert.True(File.Exists(Path.Combine(binDir, "NetFxBindingRedirects.Clr2.exe.config")),
                "NetFxBindingRedirects.Clr2.exe.config missing — app.config did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "lib")),
                "lib/ subdir missing — Clr2 privatePath helper did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "external")),
                "external/ subdir missing — Clr2 codeBase helper did not deploy");
            Assert.True(Directory.Exists(Path.Combine(binDir, "fr")),
                "fr/ subdir missing — Clr2 culture satellite did not deploy");

            // Identity-based copy-local guard: V1 and V2 emit the same filename, so a path
            // check can't disambiguate. Reject silently re-introduced V1 by reading the staged
            // assembly's version. The redirect collapses on V2; any other value means the
            // wrong build leaked app-local through the project graph.
            var stagedSharedDep = Path.Combine(binDir, "NetFxBindingRedirects.Clr2.SharedDep.dll");
            Assert.True(File.Exists(stagedSharedDep),
                $"SharedDep V2 was not staged app-local at {stagedSharedDep}");
            var stagedVersion = System.Reflection.AssemblyName.GetAssemblyName(stagedSharedDep).Version?.ToString();
            Assert.Equal("2.0.0.0", stagedVersion);
        }
        if (NativeAotConsoleExe is not null)
            Assert.True(File.Exists(NativeAotConsoleExe), $"NativeAotConsole.exe not found at {NativeAotConsoleExe}");
        if (SelfContainedConsoleExe is not null)
            Assert.True(File.Exists(SelfContainedConsoleExe), $"SelfContainedConsole not found at {SelfContainedConsoleExe}");
        Assert.True(File.Exists(HelloWorldExe), $"HelloWorld apphost not found at {HelloWorldExe}");
        Assert.True(File.Exists(ComplexAppExe), $"ComplexApp apphost not found at {ComplexAppExe}");
        Assert.True(File.Exists(MinimalApiExe), $"MinimalApi apphost not found at {MinimalApiExe}");
        Assert.True(File.Exists(DottedNameAppDll), $"Dotted.Name.App.dll not found at {DottedNameAppDll}");
        Assert.True(File.Exists(DottedNameAppExe), $"Dotted.Name.App apphost not found at {DottedNameAppExe}");
    }

    /// <summary>
    /// Cleans up temporary files created by the fixture.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (File.Exists(NonDotNetBinaryPath))
        {
            try { File.Delete(NonDotNetBinaryPath); }
            catch { /* best effort */ }
        }
        return ValueTask.CompletedTask;
    }

    private string SamplePath(string project, string config, string tfm, string file)
        => Path.Combine(_repoRoot, "samples", project, "bin", config, tfm, file);

    private async Task PublishNativeAotProject(string relativePath)
    {
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c Release -r {RuntimeInformation.RuntimeIdentifier} -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // NoDefaultCurrentDirectoryInExePath breaks the ILCompiler's
            // findvcvarsall → VsDevCmd toolchain discovery (vswhere.exe
            // is not resolved from the current directory inside FOR /F).
            // Clear it for this process only.
            psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath");

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task PublishSelfContainedProject(string relativePath)
    {
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c Release -r {RuntimeInformation.RuntimeIdentifier} --self-contained -p:PublishSingleFile=true -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task BuildProject(string relativePath)
    {
        // Use a file lock to prevent concurrent builds of the same project
        // across test assemblies (e.g. Dotsider.Tests and Dotsider.Mcp.Tests).
        // File locks are cross-platform, unlike named Mutex/Semaphore.
        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + ".lock";
        var lockPath = Path.Combine(Path.GetTempPath(), lockName);

        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        try
        {
            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --no-restore -c Debug -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet build failed for {relativePath} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    /// <summary>
    /// Runs <c>NetFxBindingRedirects.exe --oracle &lt;temp.json&gt;</c> on Windows under the
    /// real .NET Framework CLR, captures the JSON document, and returns it as a typed map.
    /// Used as the runtime ground truth for NetFxBinder oracle-parity tests.
    /// </summary>
    /// <param name="exePath">Absolute path to the built sample executable.</param>
    /// <returns>The parsed oracle map.</returns>
    private static async Task<IReadOnlyDictionary<string, NetFxOracleEntry>> CaptureNetFxOracleAsync(string exePath)
    {
        var oraclePath = Path.Combine(Path.GetTempPath(),
            $"netfx-binder-oracle-{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--oracle \"" + oraclePath + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
            };
            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !File.Exists(oraclePath))
                throw new InvalidOperationException(
                    $"NetFxBindingRedirects oracle run failed (exit {process.ExitCode}):\n{stdout}\n{stderr}");

            var json = await File.ReadAllTextAsync(oraclePath);
            using var doc = JsonDocument.Parse(json);
            var map = new Dictionary<string, NetFxOracleEntry>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var fullName = prop.Value.GetProperty("fullName").GetString() ?? string.Empty;
                var location = prop.Value.GetProperty("location").GetString() ?? string.Empty;
                var loaded = prop.Value.GetProperty("loaded").GetBoolean();
                var error = prop.Value.GetProperty("error").GetString();
                map[prop.Name] = new NetFxOracleEntry(fullName, location, loaded, error);
            }
            return map;
        }
        finally
        {
            try { File.Delete(oraclePath); } catch { /* best effort */ }
        }
    }
}

/// <summary>
/// One entry in the NetFxBindingRedirects runtime oracle JSON: what the actual .NET Framework
/// CLR loaded for a particular reference.
/// </summary>
/// <param name="FullName">The bound assembly's <c>Assembly.FullName</c>, or empty on failure.</param>
/// <param name="Location">The bound assembly's <c>Assembly.Location</c>, or empty on failure.</param>
/// <param name="Loaded">Whether the load succeeded.</param>
/// <param name="Error">Type-name + message of the load exception, or <see langword="null"/> on success.</param>
public sealed record NetFxOracleEntry(string FullName, string Location, bool Loaded, string? Error);
