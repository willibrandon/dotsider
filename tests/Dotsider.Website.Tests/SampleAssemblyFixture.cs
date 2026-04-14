using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Website.Tests;

/// <summary>
/// Shared xUnit fixture that builds and exposes sample assemblies used across website tests.
/// </summary>
public class SampleAssemblyFixture : IAsyncLifetime
{
    private string _repoRoot = null!;

    /// <summary>
    /// Path to the built RichLibrary.dll sample assembly.
    /// </summary>
    public string RichLibraryDll { get; private set; } = null!;

    /// <summary>
    /// Directory containing the published single-file Website and RichLibrary.dll.
    /// </summary>
    public string WebsitePublishedDir { get; private set; } = null!;

    /// <summary>
    /// Path to the published single-file Website executable.
    /// </summary>
    public string WebsitePublishedExe { get; private set; } = null!;

    /// <summary>
    /// Builds the sample projects and publishes the website once per test collection.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _repoRoot = GetRepoRoot();

        await BuildProject("samples/RichLibrary");
        await PublishWebsite();

        const string config = "Debug";
        const string tfm = "net10.0";

        RichLibraryDll = Path.Combine(_repoRoot, "samples", "RichLibrary", "bin", config, tfm, "RichLibrary.dll");

        Assert.True(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.True(File.Exists(WebsitePublishedExe), $"Website not found at {WebsitePublishedExe}");

        // Copy RichLibrary.dll into publish directory (mirrors deploy workflow)
        File.Copy(RichLibraryDll,
            Path.Combine(WebsitePublishedDir, "RichLibrary.dll"), overwrite: true);
    }

    /// <summary>
    /// Releases fixture resources; published artifacts are intentionally retained for inspection.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task PublishWebsite()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var apphostExt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        WebsitePublishedDir = Path.Combine(_repoRoot, "tests", "Dotsider.Website.Tests",
            "bin", "website-publish");
        WebsitePublishedExe = Path.Combine(WebsitePublishedDir, $"Dotsider.Website{apphostExt}");

        var lockPath = Path.Combine(Path.GetTempPath(), "dotsider-build-website-publish.lock");
        FileStream lockFile;
        while (true)
        {
            try
            {
                lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException) { await Task.Delay(200); }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish src/Dotsider.Website/Dotsider.Website.csproj -c Release -r {rid} --self-contained -p:PublishSingleFile=true -o {WebsitePublishedDir} -v q",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
            };

            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Website publish failed (exit {process.ExitCode})");
        }
        finally
        {
            lockFile.Dispose();
        }
    }

    private async Task BuildProject(string relativePath)
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
            catch (IOException) { await Task.Delay(200); }
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
