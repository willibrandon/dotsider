namespace Dotsider.Tests;

/// <summary>
/// Verifies sample fixture artifact validation and recovery behavior.
/// </summary>
[TestClass]
public class SampleAssemblyFixtureTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies a truncated ReadyToRun publish output is not reused as a fixture.
    /// </summary>
    [TestMethod]
    public void ExistingReadyToRunPathOrNull_TruncatedImage_ReturnsNull()
    {
        TestSkip.When(
            Samples.ReadyToRunCompositeImage is null,
            "ReadyToRun composite publish did not run on this leg.");

        var directory = Directory.CreateTempSubdirectory("dotsider-r2r-");
        try
        {
            var path = Path.Combine(directory.FullName, "partial.r2r.dll");
            var image = File.ReadAllBytes(Samples.ReadyToRunCompositeImage!);
            File.WriteAllBytes(path, image.AsSpan(0, Math.Min(image.Length, 4096)));

            Assert.IsNull(SampleAssemblyFixture.ExistingReadyToRunPathOrNull(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
