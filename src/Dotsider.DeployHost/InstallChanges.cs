namespace Dotsider.DeployHost;

/// <summary>
/// Records which installed asset groups changed during one operation.
/// Callers use the result to reload only affected system services.
/// Unchanged configuration leaves running services undisturbed.
/// </summary>
internal sealed record InstallChanges(bool CaddyChanged, bool PrometheusChanged, bool SystemdChanged);
