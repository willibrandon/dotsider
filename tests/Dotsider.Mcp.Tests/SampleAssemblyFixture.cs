using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Mcp.Tests;

public class SampleAssemblyFixture : IAsyncLifetime
{
    private string _repoRoot = null!;

    public string HelloWorldDll { get; private set; } = null!;
    public string HelloWorldExe { get; private set; } = null!;
    public string ComplexAppDll { get; private set; } = null!;
    public string RichLibraryDll { get; private set; } = null!;
    public string RichLibraryV2Dll { get; private set; } = null!;
    public string EmptyLibDll { get; private set; } = null!;
    public string NativeLibDll { get; private set; } = null!;
    public string RichLibraryNupkg { get; private set; } = null!;
    public string MinimalApiDll { get; private set; } = null!;
    public string SelfContainedConsoleExe { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _repoRoot = GetRepoRoot();

        await Task.WhenAll(
            BuildProject("samples/HelloWorld"),
            BuildProject("samples/RichLibrary"),
            BuildProject("samples/RichLibraryV2"),
            BuildProject("samples/ComplexApp"),
            BuildProject("samples/NativeLib"),
            BuildProject("samples/EmptyLib"),
            BuildProject("samples/MinimalApi"),
            PublishSelfContainedProject("samples/SelfContainedConsole")
        );

        const string config = "Debug";
        const string tfm = "net10.0";
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, $"HelloWorld{apphostExt}");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        var rid = RuntimeInformation.RuntimeIdentifier;
        SelfContainedConsoleExe = Path.Combine(_repoRoot, "samples", "SelfContainedConsole",
            "bin", "Release", tfm, rid, "publish", $"SelfContainedConsole{apphostExt}");

        Assert.True(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.True(File.Exists(HelloWorldExe), $"HelloWorld apphost not found at {HelloWorldExe}");
        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
        Assert.True(File.Exists(MinimalApiDll), $"MinimalApi.dll not found at {MinimalApiDll}");
        Assert.True(File.Exists(SelfContainedConsoleExe), $"SelfContainedConsole not found at {SelfContainedConsoleExe}");
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private string SamplePath(string project, string config, string tfm, string file)
        => Path.Combine(_repoRoot, "samples", project, "bin", config, tfm, file);

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
            // Skip if the other test project already built this sample
            var projectName = Path.GetFileName(relativePath);
            var expectedDll = Path.Combine(_repoRoot, relativePath,
                "bin", "Debug", "net10.0", $"{projectName}.dll");
            if (File.Exists(expectedDll))
            {
                lockFile.Dispose();
                return;
            }

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
            // Skip if the other test project already published
            var rid = RuntimeInformation.RuntimeIdentifier;
            var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            var expectedOutput = Path.Combine(_repoRoot, relativePath,
                "bin", "Release", "net10.0", rid, "publish",
                $"{Path.GetFileName(relativePath)}{apphostExt}");
            if (File.Exists(expectedOutput))
            {
                lockFile.Dispose();
                return;
            }

            var projectDir = Path.Combine(_repoRoot, relativePath);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c Release -r {rid} --self-contained -p:PublishSingleFile=true -v q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
            };

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
