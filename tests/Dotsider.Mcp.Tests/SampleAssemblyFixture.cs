using System.Diagnostics;

namespace Dotsider.Mcp.Tests;

public class SampleAssemblyFixture : IAsyncLifetime
{
    private string _repoRoot = null!;

    public string HelloWorldDll { get; private set; } = null!;
    public string ComplexAppDll { get; private set; } = null!;
    public string RichLibraryDll { get; private set; } = null!;
    public string RichLibraryV2Dll { get; private set; } = null!;
    public string EmptyLibDll { get; private set; } = null!;
    public string NativeLibDll { get; private set; } = null!;
    public string RichLibraryNupkg { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _repoRoot = GetRepoRoot();

        await Task.WhenAll(
            BuildProject("samples/HelloWorld"),
            BuildProject("samples/RichLibrary"),
            BuildProject("samples/RichLibraryV2"),
            BuildProject("samples/ComplexApp"),
            BuildProject("samples/NativeLib"),
            BuildProject("samples/EmptyLib")
        );

        const string config = "Debug";
        const string tfm = "net10.0";

        HelloWorldDll = SamplePath("HelloWorld", config, tfm, "HelloWorld.dll");
        ComplexAppDll = SamplePath("ComplexApp", config, tfm, "ComplexApp.dll");
        RichLibraryDll = SamplePath("RichLibrary", config, tfm, "RichLibrary.dll");
        RichLibraryV2Dll = SamplePath("RichLibraryV2", config, tfm, "RichLibrary.dll");
        EmptyLibDll = SamplePath("EmptyLib", config, tfm, "EmptyLib.dll");
        NativeLibDll = SamplePath("NativeLib", config, tfm, "NativeLib.dll");
        RichLibraryNupkg = Path.Combine(_repoRoot, "samples", "RichLibrary",
            "bin", config, "RichLibrary.2.5.1.nupkg");

        Assert.True(File.Exists(HelloWorldDll), $"HelloWorld.dll not found at {HelloWorldDll}");
        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(RichLibraryNupkg), $"RichLibrary.nupkg not found at {RichLibraryNupkg}");
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
