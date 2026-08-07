using Dotsider.DeployHost;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Verifies complete sample payload hashing, ordering, and corruption detection.
/// Missing, altered, and additional files must all invalidate a manifest.
/// Manifest paths are constrained to the sample root.
/// </summary>
[TestClass]
public sealed class SampleManifestTests
{
    private static readonly string[] s_expectedPaths = ["./nested/a.json", "./z.dll"];

    /// <summary>
    /// Creates and verifies a complete nested payload manifest.
    /// Relative paths must be sorted using their UTF-8 byte order.
    /// Exact payload content passes verification.
    /// </summary>
    [TestMethod]
    public async Task CreateAndVerifyAsync_ExactPayloadSucceedsWithSortedManifest()
    {
        string root = CreatePayload(out string sample, out string manifest);
        try
        {
            Directory.CreateDirectory(Path.Combine(sample, "nested"));
            await File.WriteAllTextAsync(Path.Combine(sample, "z.dll"), "z", TestContext.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(sample, "nested", "a.json"), "a", TestContext.CancellationToken);

            await SampleManifest.CreateAsync(sample, manifest, TestContext.CancellationToken);

            Assert.IsTrue(await SampleManifest.VerifyAsync(sample, manifest, TestContext.CancellationToken));
            string[] paths = [.. File.ReadAllLines(manifest).Select(static line => line[66..])];
            Assert.AreSequenceEqual(s_expectedPaths, paths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Verifies detection of altered, missing, and additional payload files.
    /// Each change begins from a valid generated manifest.
    /// No drift shape may pass exact verification.
    /// </summary>
    /// <param name="change">The payload change to apply.</param>
    [TestMethod]
    [DataRow("alter")]
    [DataRow("delete")]
    [DataRow("add")]
    public async Task VerifyAsync_PayloadDriftFails(string change)
    {
        string root = CreatePayload(out string sample, out string manifest);
        try
        {
            string payload = Path.Combine(sample, "RichLibrary.dll");
            await File.WriteAllTextAsync(payload, "original", TestContext.CancellationToken);
            await SampleManifest.CreateAsync(sample, manifest, TestContext.CancellationToken);
            if (change == "alter")
            {
                await File.WriteAllTextAsync(payload, "changed", TestContext.CancellationToken);
            }
            else if (change == "delete")
            {
                File.Delete(payload);
            }
            else
            {
                await File.WriteAllTextAsync(Path.Combine(sample, "extra.dll"), "extra", TestContext.CancellationToken);
            }

            Assert.IsFalse(await SampleManifest.VerifyAsync(sample, manifest, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Rejects a manifest entry that escapes the sample directory.
    /// The outside path is never opened or hashed.
    /// Invalid manifest content returns a normal verification failure.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync_TraversalEntryFailsWithoutOpeningOutsideFile()
    {
        string root = CreatePayload(out string sample, out string manifest);
        try
        {
            await File.WriteAllTextAsync(
                manifest,
                new string('0', 64) + "  ./../outside.dll\n",
                TestContext.CancellationToken);

            Assert.IsFalse(await SampleManifest.VerifyAsync(sample, manifest, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreatePayload(out string sample, out string manifest)
    {
        string root = Path.Combine(Path.GetTempPath(), "dotsider-sample-test-" + Guid.NewGuid().ToString("N"));
        sample = Path.Combine(root, "sample");
        manifest = Path.Combine(root, "sample.sha256");
        Directory.CreateDirectory(sample);
        return root;
    }

    /// <summary>
    /// Gets or sets the current MSTest execution context.
    /// The cancellation token is passed to asynchronous file operations.
    /// MSTest supplies the value before each test begins.
    /// </summary>
    public TestContext TestContext { get; set; }
}
