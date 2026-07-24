using System.Diagnostics.CodeAnalysis;

namespace Dotsider.Website;

internal sealed class DemoOriginPolicy
{
    private DemoOriginPolicy(string[] allowedOrigins, bool allowsAnyOrigin)
    {
        AllowedOrigins = allowedOrigins;
        AllowsAnyOrigin = allowsAnyOrigin;
    }

    internal string[] AllowedOrigins { get; }

    internal bool AllowsAnyOrigin { get; }

    internal static DemoOriginPolicy Create(string[]? configuredOrigins)
    {
        if (TryCreate(configuredOrigins, out var policy, out var failure))
            return policy;

        throw new InvalidOperationException(failure);
    }

    internal static bool TryCreate(
        string[]? configuredOrigins,
        [NotNullWhen(true)] out DemoOriginPolicy? policy,
        [NotNullWhen(false)] out string? failure)
    {
        policy = null;

        if (configuredOrigins is null || configuredOrigins.Length == 0)
        {
            failure = "Demo:AllowedOrigins must contain at least one origin.";
            return false;
        }

        var normalizedOrigins = new List<string>(configuredOrigins.Length);
        var uniqueOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredOrigin in configuredOrigins)
        {
            if (string.IsNullOrWhiteSpace(configuredOrigin))
            {
                failure = "Demo:AllowedOrigins entries must not be empty.";
                return false;
            }

            var origin = configuredOrigin.Trim();
            if (origin == "*")
            {
                if (configuredOrigins.Length != 1)
                {
                    failure =
                        "Demo:AllowedOrigins cannot combine '*' with explicit origins.";
                    return false;
                }

                policy = new DemoOriginPolicy(["*"], allowsAnyOrigin: true);
                failure = null;
                return true;
            }

            if (!TryNormalizeOrigin(origin, out var normalizedOrigin))
            {
                failure =
                    $"Demo:AllowedOrigins entry '{origin}' must be an HTTP or HTTPS origin without credentials, a path, query, or fragment.";
                return false;
            }

            if (uniqueOrigins.Add(normalizedOrigin))
                normalizedOrigins.Add(normalizedOrigin);
        }

        policy = new DemoOriginPolicy([.. normalizedOrigins], allowsAnyOrigin: false);
        failure = null;
        return true;
    }

    private static bool TryNormalizeOrigin(
        string origin,
        [NotNullWhen(true)] out string? normalizedOrigin)
    {
        normalizedOrigin = null;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.AbsolutePath != "/" && uri.AbsolutePath.Length != 0) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        normalizedOrigin = uri.GetComponents(
            UriComponents.SchemeAndServer,
            UriFormat.UriEscaped);
        return true;
    }
}
