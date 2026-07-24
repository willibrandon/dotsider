using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Dotsider.Website;

internal static class DemoOptionsServiceCollectionExtensions
{
    internal static IServiceCollection AddDemoOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(DemoOptions.SectionName);
        var originsConfigured = section.GetSection(nameof(DemoOptions.AllowedOrigins)).Exists();

        services.AddSingleton<IValidateOptions<DemoOptions>, DemoOptionsValidator>();
        services.AddOptions<DemoOptions>()
            .Bind(section)
            .PostConfigure(options =>
            {
                if (!originsConfigured)
                    options.AllowedOrigins = ["*"];
            })
            .ValidateOnStart();
        services.AddSingleton(static serviceProvider =>
            DemoOriginPolicy.Create(
                serviceProvider.GetRequiredService<IOptions<DemoOptions>>().Value.AllowedOrigins));
        services.Configure<ForwardedHeadersOptions>(static options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
        });

        return services;
    }
}
