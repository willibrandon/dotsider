using System.Diagnostics;

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

    // Non-.NET binary for error case testing
    public string NonDotNetBinaryPath { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _repoRoot = TestHelpers.GetRepoRoot();

        // Build all 7 samples in parallel
        await Task.WhenAll(
            BuildProject("samples/HelloWorld"),
            BuildProject("samples/RichLibrary"),
            BuildProject("samples/RichLibraryV2"),
            BuildProject("samples/ComplexApp"),
            BuildProject("samples/MinimalApi"),
            BuildProject("samples/NativeLib"),
            BuildProject("samples/EmptyLib")
        );

        var config = "Debug";
        var tfm = "net10.0";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        HelloWorldExe = SamplePath("HelloWorld", config, tfm, "HelloWorld");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        ComplexAppExe = SamplePath("ComplexApp", config, tfm, "ComplexApp");
        MinimalApiDll = SamplePath("MinimalApi", config, tfm, "MinimalApi.dll");
        MinimalApiExe = SamplePath("MinimalApi", config, tfm, "MinimalApi");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");

        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        // Create non-.NET binary for BadImageFormatException testing
        NonDotNetBinaryPath = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(NonDotNetBinaryPath, [0xDE, 0xAD, 0xBE, 0xEF]);

        // Verify critical paths exist
        Assert.True(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
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
