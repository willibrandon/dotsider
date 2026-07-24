using Microsoft.Extensions.Options;

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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
