using Microsoft.AspNetCore.Http.Features;

namespace Dotsider.Website.Tests;

internal sealed class TestUpgradableRequestFeature : IHttpUpgradeFeature
{
    internal static TestUpgradableRequestFeature Instance { get; } = new();

    private TestUpgradableRequestFeature()
    {
    }

    bool IHttpUpgradeFeature.IsUpgradableRequest => true;

    Task<Stream> IHttpUpgradeFeature.UpgradeAsync()
    {
        return Task.FromException<Stream>(
            new NotSupportedException("Rejected requests must not be upgraded."));
    }
}
