using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Shared MSTest fixture that builds and exposes sample assemblies used across MCP tool tests.
/// </summary>
internal class SampleAssemblyFixture : IAsyncDisposable
{
    private string _repoRoot = null!;

    /// <summary>
    /// Path to the built HelloWorld.dll sample assembly.
    /// </summary>
    public string HelloWorldDll { get; private set; } = null!;
    /// <summary>
    /// Path to the HelloWorld apphost executable (platform-specific extension).
    /// </summary>
    public string HelloWorldExe { get; private set; } = null!;
    /// <summary>
    /// Path to the ComplexApp.dll sample assembly exercising varied IL shapes.
    /// </summary>
    public string ComplexAppDll { get; private set; } = null!;
    /// <summary>
    /// Path to the RichLibrary.dll sample assembly (v1) with attributes and references.
    /// </summary>
    public string RichLibraryDll { get; private set; } = null!;
    /// <summary>
    /// Path to the RichLibrary v2 build used for assembly diff tests.
    /// </summary>
    public string RichLibraryV2Dll { get; private set; } = null!;
    /// <summary>
    /// Path to the EmptyLib.dll sample used for minimal-assembly edge cases.
    /// </summary>
    public string EmptyLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the compiler-produced assembly containing an embedded portable PDB and sources.
    /// </summary>
    public string EmbeddedSourceLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the NativeLib.dll sample with P/Invoke declarations.
    /// </summary>
    public string NativeLibDll { get; private set; } = null!;
    /// <summary>
    /// Path to the RichLibrary .nupkg used by NuGet package inspection tests.
    /// </summary>
    public string RichLibraryNupkg { get; private set; } = null!;
    /// <summary>
    /// Path to the MinimalApi.dll sample, used to verify ASP.NET Core runtime pack detection.
    /// </summary>
    public string MinimalApiDll { get; private set; } = null!;
    /// <summary>
    /// Path to the published self-contained single-file apphost used by bundle tests.
    /// </summary>
    public string SelfContainedConsoleExe { get; private set; } = null!;
    /// <summary>
    /// Path to the published NativeAOT sample executable, or null when not built.
    /// </summary>
    public string? NativeAotConsoleExe { get; private set; }

    /// <summary>
    /// Path to the ILC size report published next to the NativeAOT sample, or null when the
    /// publish did not produce one.
    /// </summary>
    public string? NativeAotConsoleMstat { get; private set; }

    /// <summary>
    /// Path to the ILC dependency-graph DGML published next to the NativeAOT sample (codegen
    /// preferred, scan fallback), or null when the publish did not produce one.
    /// </summary>
    public string? NativeAotConsoleDgml { get; private set; }

    /// <summary>
    /// Path to the pre-ILC managed assembly left in the NativeAOT sample's intermediate tree
    /// (<c>obj\Release\&lt;tfm&gt;\&lt;rid&gt;\NativeAotConsole.dll</c>), or null. Correlation tests
    /// gate on this — it is the attachable companion the sidecar probe finds.
    /// </summary>
    public string? NativeAotConsoleManagedDll { get; private set; }

    /// <summary>
    /// Path to the published V2 NativeAOT sample executable, or null when not built. The
    /// project folder is <c>NativeAotConsoleV2</c> but its AssemblyName is pinned to
    /// <c>NativeAotConsole</c>, so the output file is named <c>NativeAotConsole</c> and the
    /// folder tells the two builds apart.
    /// </summary>
    public string? NativeAotConsoleV2Exe { get; private set; }

    /// <summary>
    /// Path to the ILC size report published next to the V2 NativeAOT sample, or null when
    /// the publish did not produce one. Size-diff tool tests gate on it.
    /// </summary>
    public string? NativeAotConsoleV2Mstat { get; private set; }

    /// <summary>
    /// Path to the ILC dependency-graph DGML published next to the V2 NativeAOT sample
    /// (codegen preferred, scan fallback), or null when the publish did not produce one.
    /// </summary>
    public string? NativeAotConsoleV2Dgml { get; private set; }

    /// <summary>
    /// Path to the published non-composite ReadyToRun assembly (crossgen'd in place), or null when the
    /// crossgen2 publish did not run for this RID. R2R MCP tool tests gate on it.
    /// </summary>
    public string? ReadyToRunConsoleDll { get; private set; }

    /// <summary>
    /// Path to the SDK-produced browser Wasm runtime module when the wasm-tools workload is present.
    /// MCP symbol and assembly-info tests gate on it because not every developer machine has the
    /// workload installed.
    /// </summary>
    public string? ReadyToRunConsoleWasmNativeWasm { get; private set; }

    /// <summary>
    /// Path to the dedicated SDK-produced browser Wasm runtime module from <c>samples/WasmConsole</c>.
    /// MCP raw Wasm tests prefer this focused fixture when the wasm-tools workload is installed.
    /// </summary>
    public string? WasmConsoleNativeWasm { get; private set; }

    /// <summary>
    /// Path to the Webcil-wrapped managed app assembly from <c>samples/WasmConsole</c>.
    /// MCP managed Webcil tests use this to verify normal metadata tools still work.
    /// </summary>
    public string? WasmConsoleWebcilWasm { get; private set; }

    /// <summary>
    /// Builds all sample projects once per collection and resolves their output paths.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _repoRoot = GetRepoRoot();

        // Build sequentially to avoid saturating CI runners when
        // Dotsider.Tests is running its own builds in parallel.
        await BuildProject("samples/HelloWorld");
        await BuildProject("samples/RichLibrary");
        await BuildProject("samples/RichLibraryV2");
        await BuildProject("samples/ComplexApp");
        await BuildProject("samples/EmbeddedSourceLib");
        await BuildProject("samples/NativeLib");
        await BuildProject("samples/EmptyLib");
        await BuildProject("samples/MinimalApi");
        await PublishSelfContainedProject("samples/SelfContainedConsole");
        await PublishNativeAotProject("samples/NativeAotConsole");
        await PublishNativeAotProject("samples/NativeAotConsoleV2");
        await PublishReadyToRunProject("samples/ReadyToRunConsole");
        await PublishWasmProject("samples/ReadyToRunConsole");
        await PublishWasmProject("samples/WasmConsole");

        string config = TestProcessEnvironment.DebugBuildConfiguration;
        string releaseConfig = TestProcessEnvironment.ReleaseBuildConfiguration;
        const string tfm = "net10.0";
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, $"HelloWorld{apphostExt}");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");
        EmbeddedSourceLibDll = SamplePath(
            "EmbeddedSourceLib",
            config,
            tfm,
            "EmbeddedSourceLib.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        var rid = RuntimeInformation.RuntimeIdentifier;
        SelfContainedConsoleExe = Path.Combine(_repoRoot, "samples", "SelfContainedConsole",
            "bin", releaseConfig, tfm, rid, "publish", $"SelfContainedConsole{apphostExt}");

        NativeAotConsoleExe = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
            "bin", releaseConfig, tfm, rid, "publish", $"NativeAotConsole{apphostExt}");

        var r2rConsoleDll = Path.Combine(_repoRoot, "samples", "ReadyToRunConsole",
            "bin", releaseConfig, tfm, rid, "publish", "ReadyToRunConsole.dll");
        ReadyToRunConsoleDll = File.Exists(r2rConsoleDll) ? r2rConsoleDll : null;
        var wasmNative = Path.Combine(_repoRoot, "samples", "ReadyToRunConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "publish", "dotnet.native.wasm");
        ReadyToRunConsoleWasmNativeWasm = File.Exists(wasmNative) ? wasmNative : null;
        var wasmConsoleNative = Path.Combine(_repoRoot, "samples", "WasmConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "publish", "dotnet.native.wasm");
        WasmConsoleNativeWasm = File.Exists(wasmConsoleNative) ? wasmConsoleNative : null;
        var wasmConsoleWebcil = Path.Combine(_repoRoot, "samples", "WasmConsole",
            "bin", releaseConfig, tfm, "browser-wasm", "AppBundle", "_framework", "WasmConsole.wasm");
        WasmConsoleWebcilWasm = File.Exists(wasmConsoleWebcil) ? wasmConsoleWebcil : null;

        Assert.IsTrue(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.IsTrue(File.Exists(HelloWorldExe), $"HelloWorld apphost not found at {HelloWorldExe}");
        Assert.IsTrue(
            File.Exists(EmbeddedSourceLibDll),
            $"EmbeddedSourceLib.dll not found at {EmbeddedSourceLibDll}");
        Assert.IsTrue(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.IsTrue(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
        Assert.IsTrue(File.Exists(MinimalApiDll), $"MinimalApi.dll not found at {MinimalApiDll}");
        Assert.IsTrue(File.Exists(SelfContainedConsoleExe), $"SelfContainedConsole not found at {SelfContainedConsoleExe}");

        if (!File.Exists(NativeAotConsoleExe))
            NativeAotConsoleExe = null;

        if (NativeAotConsoleExe is not null)
        {
            var aotPublishDir = Path.GetDirectoryName(NativeAotConsoleExe)!;
            var mstat = Path.Combine(aotPublishDir, "NativeAotConsole.mstat");
            NativeAotConsoleMstat = File.Exists(mstat) ? mstat : null;
            var codegenDgml = Path.Combine(aotPublishDir, "NativeAotConsole.codegen.dgml.xml");
            var scanDgml = Path.Combine(aotPublishDir, "NativeAotConsole.scan.dgml.xml");
            NativeAotConsoleDgml = File.Exists(codegenDgml) ? codegenDgml
                : File.Exists(scanDgml) ? scanDgml
                : null;

            var aotObjDir = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
                "obj", releaseConfig, tfm, rid);
            var managedDll = Path.Combine(aotObjDir, "NativeAotConsole.dll");
            NativeAotConsoleManagedDll = File.Exists(managedDll) ? managedDll : null;
        }

        // V2 of the AOT sample: same AssemblyName, so the publish output is also named
        // NativeAotConsole — the project folder is what tells the two builds apart.
        var aotV2PublishDir = Path.Combine(_repoRoot, "samples", "NativeAotConsoleV2",
            "bin", releaseConfig, tfm, rid, "publish");
        var v2Exe = Path.Combine(aotV2PublishDir, $"NativeAotConsole{apphostExt}");
        NativeAotConsoleV2Exe = File.Exists(v2Exe) ? v2Exe : null;
        var v2Mstat = Path.Combine(aotV2PublishDir, "NativeAotConsole.mstat");
        NativeAotConsoleV2Mstat = File.Exists(v2Mstat) ? v2Mstat : null;
        var v2CodegenDgml = Path.Combine(aotV2PublishDir, "NativeAotConsole.codegen.dgml.xml");
        var v2ScanDgml = Path.Combine(aotV2PublishDir, "NativeAotConsole.scan.dgml.xml");
        NativeAotConsoleV2Dgml = File.Exists(v2CodegenDgml) ? v2CodegenDgml
            : File.Exists(v2ScanDgml) ? v2ScanDgml
            : null;
    }

    /// <summary>
    /// No-op disposal; sample outputs are left on disk for subsequent runs.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private string SamplePath(string project, string config, string tfm, string file)
        => Path.Combine(_repoRoot, "samples", project, "bin", config, tfm, file);

    private async Task BuildProject(string relativePath)
    {
        // Skip if Dotsider.Tests already built this sample
        var projectName = Path.GetFileName(relativePath);
        var configuration = TestProcessEnvironment.DebugBuildConfiguration;
        var expectedDll = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", $"{projectName}.dll");
        if (File.Exists(expectedDll))
            return;

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
            // Re-check after acquiring lock
            if (File.Exists(expectedDll))
            {
                lockFile.Dispose();
                return;
            }

            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build --no-restore -c {configuration} -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

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

    private async Task PublishSelfContainedProject(string relativePath)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var configuration = TestProcessEnvironment.ReleaseBuildConfiguration;
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        var expectedOutput = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", rid, "publish",
            $"{Path.GetFileName(relativePath)}{apphostExt}");

        // Skip if Dotsider.Tests already published
        if (File.Exists(expectedOutput))
            return;

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
            // Re-check after acquiring lock
            if (File.Exists(expectedOutput))
            {
                lockFile.Dispose();
                return;
            }

            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {configuration} -r {rid} --self-contained "
                    + "-p:PublishSingleFile=true -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
            };
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode})");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task PublishReadyToRunProject(string relativePath)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var configuration = TestProcessEnvironment.ReleaseBuildConfiguration;
        var expectedOutput = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", rid, "publish", $"{Path.GetFileName(relativePath)}.dll");

        // Reuse the Dotsider.Tests publish when it already ran (framework-dependent crossgen).
        if (File.Exists(expectedOutput))
            return;

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
            if (File.Exists(expectedOutput)) { lockFile.Dispose(); return; }
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {configuration} -r {rid} --self-contained false -v q",
                WorkingDirectory = Path.Combine(_repoRoot, relativePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);
            var process = Process.Start(psi)!;
            _ = await process.StandardOutput.ReadToEndAsync();
            _ = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            // A non-zero exit means crossgen2 is unavailable for this RID; the outputs stay absent.
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task PublishWasmProject(string relativePath)
    {
        var configuration = TestProcessEnvironment.ReleaseBuildConfiguration;
        var expectedOutput = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", "browser-wasm", "publish", "dotnet.native.wasm");

        if (File.Exists(expectedOutput))
            return;

        var lockName = "dotsider-build-" + relativePath.Replace('/', '-').Replace('\\', '-') + "-browser-wasm.lock";
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
            if (File.Exists(expectedOutput))
            {
                lockFile.Dispose();
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {configuration} -r browser-wasm --self-contained true "
                    + "-p:PublishReadyToRun=false -v q",
                WorkingDirectory = Path.Combine(_repoRoot, relativePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.ConfigureBuild(psi);
            var process = Process.Start(psi)!;
            _ = await process.StandardOutput.ReadToEndAsync();
            _ = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            // A non-zero exit means wasm-tools is unavailable; the output stays absent.
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task PublishNativeAotProject(string relativePath)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var configuration = TestProcessEnvironment.ReleaseBuildConfiguration;
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        var publishDir = Path.Combine(_repoRoot, relativePath,
            "bin", configuration, "net10.0", rid, "publish");
        var expectedOutput = Path.Combine(publishDir,
            $"{Path.GetFileName(relativePath)}{apphostExt}");
        // The mstat sidecar joins the up-to-date check so a publish that predates sidecar
        // emission republishes once instead of leaving sidecar tests skipping forever.
        var expectedMstat = Path.Combine(publishDir, $"{Path.GetFileName(relativePath)}.mstat");

        // Skip if Dotsider.Tests already published
        if (File.Exists(expectedOutput) && File.Exists(expectedMstat))
            return;

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
            // Re-check after acquiring lock
            if (File.Exists(expectedOutput) && File.Exists(expectedMstat))
            {
                lockFile.Dispose();
                return;
            }

            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c {configuration} -r {rid} -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
            };

            // NoDefaultCurrentDirectoryInExePath breaks the ILCompiler's
            // findvcvarsall → VsDevCmd toolchain discovery (vswhere.exe
            // is not resolved from the current directory inside FOR /F).
            // Clear it for this process only.
            psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath");
            TestProcessEnvironment.ConfigureBuild(psi);

            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"dotnet publish failed for {relativePath} (exit {process.ExitCode})");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Dotsider.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find repo root (Dotsider.slnx)");
    }
}
