using System.Reflection;
using Dotsider.DeployHost;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Verifies that the install manifest is the single source of deployed configuration assets.
/// Embedded bytes must exactly match their checked-in deploy files.
/// System services must invoke the installed Native AOT helper rather than removed scripts.
/// </summary>
[TestClass]
public sealed class InstallManifestTests
{
    /// <summary>
    /// Compares every embedded asset with its authoritative checked-in source.
    /// Ownership and mode metadata must retain their privileged fixed values.
    /// A missing or altered resource fails the comparison.
    /// </summary>
    [TestMethod]
    public async Task EmbeddedAssets_ExactlyMatchCheckedInDeployFiles()
    {
        Assembly assembly = typeof(DeployHostApplication).Assembly;
        InstallManifest manifest = InstallManifestLoader.Load(assembly);
        string repositoryRoot = FindRepositoryRoot();

        Assert.HasCount(8, manifest.Files);
        foreach (InstallFile file in manifest.Files)
        {
            string sourceFileName = Path.GetFileName(file.Destination) == "caddy-metrics"
                ? "caddy-metrics-logrotate"
                : Path.GetFileName(file.Destination);
            string sourcePath = Path.Combine(repositoryRoot, "deploy", sourceFileName);
            await using Stream resource = assembly.GetManifestResourceStream(file.Resource)!;
            using var reader = new MemoryStream();
            await resource.CopyToAsync(reader, TestContext.CancellationToken);
            Assert.AreSequenceEqual(
                await File.ReadAllBytesAsync(sourcePath, TestContext.CancellationToken),
                reader.ToArray(),
                sourcePath);
            Assert.AreEqual("0644", file.Mode);
            Assert.AreEqual("root", file.Owner);
            Assert.AreEqual("root", file.Group);
        }
    }

    /// <summary>
    /// Verifies that systemd invokes the installed deployment host commands.
    /// Legacy shell helper paths must not remain in either service.
    /// Both report and integrity operations are covered.
    /// </summary>
    [TestMethod]
    public void SystemdHelpers_InvokeInstalledDeployHost()
    {
        string repositoryRoot = FindRepositoryRoot();
        string report = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "caddy-report.service"));
        string integrity = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "integrity-check.service"));

        Assert.Contains("ExecStart=/usr/local/libexec/dotsider-deploy-host report", report);
        Assert.Contains("ExecStart=/usr/local/libexec/dotsider-deploy-host integrity", integrity);
        Assert.DoesNotContain(".sh", report);
        Assert.DoesNotContain(".sh", integrity);
    }

    /// <summary>
    /// Confirms that every established static extension has a cache matcher.
    /// Image extensions previously omitted from generated configuration remain covered.
    /// The test reads the authoritative Caddyfile directly.
    /// </summary>
    [TestMethod]
    public void Caddyfile_CoversEstablishedStaticAssetExtensions()
    {
        string caddyfile = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "deploy", "Caddyfile"));

        foreach (string extension in new[] { "*.js", "*.css", "*.png", "*.webp", "*.avif", "*.gif", "*.ico", "*.jpg", "*.svg", "*.woff2" })
        {
            Assert.Contains(extension, caddyfile);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dotsider.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    /// <summary>
    /// Gets or sets the current MSTest execution context.
    /// The cancellation token is passed to asynchronous file operations.
    /// MSTest supplies the value before each test begins.
    /// </summary>
    public TestContext TestContext { get; set; }
}
