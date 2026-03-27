using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

public class SampleAssemblyFixture : IAsyncLifetime
{
    private string _repoRoot = null!;

    // Exe samples (have both .dll and apphost binary)
    public string HelloWorldDll { get; private set; } = null!;
    public string HelloWorldExe { get; private set; } = null!;
    public string ComplexAppDll { get; private set; } = null!;
    public string ComplexAppExe { get; private set; } = null!;
    public string MinimalApiDll { get; private set; } = null!;
    public string MinimalApiExe { get; private set; } = null!;

    // Library samples
    public string RichLibraryDll { get; private set; } = null!;
    public string RichLibraryV2Dll { get; private set; } = null!;
    public string NativeLibDll { get; private set; } = null!;
    public string EmptyLibDll { get; private set; } = null!;

    // NuGet package
    public string RichLibraryNupkg { get; private set; } = null!;

    // .NET Framework sample (Windows only — net48 requires Windows)
    public string? NetFxConsoleExe { get; private set; }

    // NativeAOT sample (Windows-only — ELF/Mach-O outputs aren't PE files)
    public string? NativeAotConsoleExe { get; private set; }

    // Non-.NET binary for error case testing
    public string NonDotNetBinaryPath { get; private set; } = null!;

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
        };

        // Both samples are Windows-only: net48 needs Windows, and NativeAOT
        // outputs ELF/Mach-O on Linux/macOS which aren't PE files.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builds.Add(BuildProject("samples/NetFxConsole"));
            builds.Add(PublishNativeAotProject("samples/NativeAotConsole"));
        }

        await Task.WhenAll(builds);

        var config = "Debug";
        var tfm = "net10.0";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, "HelloWorld.exe");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        ComplexAppExe = SamplePath("ComplexApp", config, tfm, "ComplexApp.exe");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        MinimalApiExe = SamplePath("MinimalApi", config, tfm, "MinimalApi.exe");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NetFxConsoleExe = Path.Combine(_repoRoot, "samples", "NetFxConsole",
                "bin", config, "net48", "NetFxConsole.exe");

            var rid = RuntimeInformation.RuntimeIdentifier;
            NativeAotConsoleExe = Path.Combine(_repoRoot, "samples", "NativeAotConsole",
                "bin", "Release", tfm, rid, "publish", "NativeAotConsole.exe");
        }

        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        // Create non-.NET binary for BadImageFormatException testing
        NonDotNetBinaryPath = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(NonDotNetBinaryPath, [0xDE, 0xAD, 0xBE, 0xEF]);

        // Verify critical paths exist
        Assert.True(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
        if (NetFxConsoleExe is not null)
            Assert.True(File.Exists(NetFxConsoleExe), $"NetFxConsole.exe not found at {NetFxConsoleExe}");
        if (NativeAotConsoleExe is not null)
            Assert.True(File.Exists(NativeAotConsoleExe), $"NativeAotConsole.exe not found at {NativeAotConsoleExe}");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(File.Exists(HelloWorldExe), $"HelloWorld.exe not found at {HelloWorldExe}");
            Assert.True(File.Exists(ComplexAppExe), $"ComplexApp.exe not found at {ComplexAppExe}");
            Assert.True(File.Exists(MinimalApiExe), $"MinimalApi.exe not found at {MinimalApiExe}");
        }
    }

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
}
