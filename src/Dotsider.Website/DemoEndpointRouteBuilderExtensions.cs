namespace Dotsider.Website;

internal static class DemoEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the readiness endpoint used by deployment health checks.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured endpoint convention builder.</returns>
    internal static IEndpointConventionBuilder MapDemoHealth(
        this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/health", static () => Results.Json(new
        {
            status = "ok"
        }));
    }
}
