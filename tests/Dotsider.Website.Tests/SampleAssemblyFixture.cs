using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Website.Tests;

/// <summary>
/// Shared MSTest fixture that builds and exposes sample assemblies used across website tests.
/// </summary>
internal class SampleAssemblyFixture : IAsyncDisposable
{
    private string _repoRoot = null!;

    /// <summary>
    /// Path to the published RichLibrary.dll sitting inside the website payload's sample
    /// directory. This is what the deployed server serves, so tests that reason about
    /// production resolution behavior must read from here.
    /// </summary>
    public string RichLibraryDll { get; private set; } = null!;

    /// <summary>
    /// Directory containing the published single-file Website executable.
    /// </summary>
    public string WebsitePublishedDir { get; private set; } = null!;

    /// <summary>
    /// Path to the published single-file Website executable.
    /// </summary>
    public string WebsitePublishedExe { get; private set; } = null!;

    /// <summary>
    /// Directory containing the published sample payload (RichLibrary.dll plus its
    /// .deps.json and every NuGet dependency the sample resolves at runtime). Mirrors
    /// <c>/opt/dotsider-website/sample/</c> on the deploy target.
    /// </summary>
    public string SamplePublishedDir { get; private set; } = null!;

    /// <summary>
    /// Builds the sample projects and publishes the website once per test collection.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _repoRoot = GetRepoRoot();

        await PublishWebsite();
        await PublishSample();

        Assert.IsTrue(File.Exists(RichLibraryDll), $"RichLibrary.dll not found at {RichLibraryDll}");
        Assert.IsTrue(File.Exists(WebsitePublishedExe), $"Website not found at {WebsitePublishedExe}");
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
            TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

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

    private async Task PublishSample()
    {
        // Publish the sample project — not plain build + cp — so every runtime artifact
        // the deployed website needs (RichLibrary.dll, RichLibrary.deps.json, and the
        // NuGet package assemblies the manifest points at) lands in one directory as
        // a unit. Place it under the website publish dir at sample/ to match the
        // deploy layout exactly; tests then exercise the same shape production runs.
        var rid = RuntimeInformation.RuntimeIdentifier;
        SamplePublishedDir = Path.Combine(WebsitePublishedDir, "sample");
        RichLibraryDll = Path.Combine(SamplePublishedDir, "RichLibrary.dll");

        var lockPath = Path.Combine(Path.GetTempPath(), "dotsider-build-sample-publish.lock");
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
            // Clean the sample directory before publishing. `dotnet publish -o` is additive
            // — it overwrites files it emits but does not remove anything left behind. If a
            // previous run's Newtonsoft.Json.dll stays on disk after a regression that stops
            // publishing it, the new website regression test would still find the adjacent
            // DLL and report Newtonsoft as resolved, hiding a real break in the publish shape.
            if (Directory.Exists(SamplePublishedDir))
                Directory.Delete(SamplePublishedDir, recursive: true);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish samples/RichLibrary/RichLibrary.csproj -c Release -r {rid} -o {SamplePublishedDir} -v q",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

            var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Sample publish failed (exit {process.ExitCode}):\n{stdout}\n{stderr}");
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
