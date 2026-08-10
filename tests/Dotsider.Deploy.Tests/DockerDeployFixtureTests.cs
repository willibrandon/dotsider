namespace Dotsider.Deploy.Tests;

/// <summary>
/// Verifies that deployment artifacts and their Docker image use the same Linux architecture.
/// </summary>
[TestClass]
public sealed class DockerDeployFixtureTests
{
    /// <summary>
    /// Maps Docker's architecture names and common aliases to matching Docker platforms and .NET RIDs.
    /// </summary>
    /// <param name="dockerArchitecture">The architecture reported by the Docker server.</param>
    /// <param name="expectedPlatform">The expected Docker platform.</param>
    /// <param name="expectedRuntimeIdentifier">The expected .NET runtime identifier.</param>
    [TestMethod]
    [DataRow("amd64", "linux/amd64", "linux-x64")]
    [DataRow("x86_64", "linux/amd64", "linux-x64")]
    [DataRow("arm64", "linux/arm64", "linux-arm64")]
    [DataRow("aarch64", "linux/arm64", "linux-arm64")]
    public void ResolveDeploymentTarget_KnownArchitecture_ReturnsMatchingTargets(
        string dockerArchitecture,
        string expectedPlatform,
        string expectedRuntimeIdentifier)
    {
        (string dockerPlatform, string runtimeIdentifier) =
            DockerDeployFixture.ResolveDeploymentTarget(dockerArchitecture);

        Assert.AreEqual(expectedPlatform, dockerPlatform);
        Assert.AreEqual(expectedRuntimeIdentifier, runtimeIdentifier);
    }

    /// <summary>
    /// Rejects an architecture for which the deployment fixture cannot publish a matching Native AOT host.
    /// </summary>
    [TestMethod]
    public void ResolveDeploymentTarget_UnsupportedArchitecture_Throws()
    {
        var exception = Assert.ThrowsExactly<PlatformNotSupportedException>(
            () => DockerDeployFixture.ResolveDeploymentTarget("riscv64"));

        Assert.Contains("riscv64", exception.Message);
    }
}
