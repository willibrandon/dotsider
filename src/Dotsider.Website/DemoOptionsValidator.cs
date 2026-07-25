using Microsoft.Extensions.Options;
using System.Net;

namespace Dotsider.Website;

internal sealed class DemoOptionsValidator : IValidateOptions<DemoOptions>
{
    ValidateOptionsResult IValidateOptions<DemoOptions>.Validate(
        string? name,
        DemoOptions options)
    {
        var failures = new List<string>();

        if (options.MaxSessions <= 0)
            failures.Add("Demo:MaxSessions must be greater than zero.");

        if (options.MaxSessionsPerClient <= 0)
            failures.Add("Demo:MaxSessionsPerClient must be greater than zero.");
        else if (options.MaxSessionsPerClient > options.MaxSessions)
        {
            failures.Add(
                "Demo:MaxSessionsPerClient must not exceed Demo:MaxSessions.");
        }

        if (!DemoOriginPolicy.TryCreate(options.AllowedOrigins, out _, out var originFailure))
            failures.Add(originFailure);

        if (options.TrustedProxies is null)
        {
            failures.Add("Demo:TrustedProxies must be an array of exact IP addresses.");
        }
        else
        {
            foreach (var trustedProxy in options.TrustedProxies)
            {
                if (string.IsNullOrWhiteSpace(trustedProxy)
                    || !IPAddress.TryParse(trustedProxy, out _))
                {
                    failures.Add(
                        $"Demo:TrustedProxies contains an invalid IP address: " +
                        $"'{trustedProxy ?? "(null)"}'.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
