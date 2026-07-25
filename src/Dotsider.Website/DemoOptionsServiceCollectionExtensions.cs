using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Net;

namespace Dotsider.Website;

internal static class DemoOptionsServiceCollectionExtensions
{
    internal static IServiceCollection AddDemoOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(DemoOptions.SectionName);
        var originsConfigured = section.GetSection(nameof(DemoOptions.AllowedOrigins)).Exists();
        var trustedProxiesConfigured = TryGetTrustedProxies(
            configuration,
            out var trustedProxies);

        services.AddSingleton<IValidateOptions<DemoOptions>, DemoOptionsValidator>();
        services.AddOptions<DemoOptions>()
            .Bind(section)
            .PostConfigure(options =>
            {
                if (!originsConfigured)
                    options.AllowedOrigins = ["*"];
                options.TrustedProxies = trustedProxiesConfigured
                    ? trustedProxies
                    : ["127.0.0.1", "::1"];
            })
            .ValidateOnStart();
        services.AddSingleton(static serviceProvider =>
            DemoOriginPolicy.Create(
                serviceProvider.GetRequiredService<IOptions<DemoOptions>>().Value.AllowedOrigins));
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<DemoOptions>>(static (options, configuredDemoOptions) =>
            {
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();

                if (configuredDemoOptions.Value.TrustedProxies.Length == 0)
                {
                    options.ForwardedHeaders = ForwardedHeaders.None;
                    return;
                }

                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                foreach (var trustedProxy in configuredDemoOptions.Value.TrustedProxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(trustedProxy));
                }
            });

        return services;
    }

    private static bool TryGetTrustedProxies(
        IConfiguration configuration,
        out string[] trustedProxies)
    {
        const string path = $"{DemoOptions.SectionName}:{nameof(DemoOptions.TrustedProxies)}";

        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers.Reverse())
            {
                var hasScalar = provider.TryGet(path, out var scalar);
                var childKeys = provider
                    .GetChildKeys([], path)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!hasScalar && childKeys.Length == 0)
                {
                    continue;
                }

                if (hasScalar)
                {
                    trustedProxies = string.IsNullOrWhiteSpace(scalar)
                        ? []
                        : [scalar];
                    return true;
                }

                trustedProxies =
                [
                    .. childKeys
                        .Select(key => (
                            Key: key,
                            Index: int.TryParse(key, out var index)
                                ? index
                                : int.MaxValue))
                        .OrderBy(static item => item.Index)
                        .ThenBy(static item => item.Key, StringComparer.Ordinal)
                        .Select(item =>
                            provider.TryGet($"{path}:{item.Key}", out var value)
                                ? value ?? ""
                                : "")
                ];
                return true;
            }
        }

        var section = configuration.GetSection(path);
        var values = section.GetChildren()
            .Select(static child => child.Value ?? "")
            .ToArray();
        if (values.Length > 0 || section.Value is not null)
        {
            trustedProxies = values.Length > 0
                ? values
                : string.IsNullOrWhiteSpace(section.Value)
                    ? []
                    : [section.Value];
            return true;
        }

        trustedProxies = [];
        return false;
    }
}
