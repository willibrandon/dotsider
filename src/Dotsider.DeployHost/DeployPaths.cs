namespace Dotsider.DeployHost;

/// <summary>
/// Defines fixed filesystem and service locations used by dotsider.dev deployment.
/// Central constants prevent user-controlled paths from reaching privileged operations.
/// Tests use the same names when validating the installed host layout.
/// </summary>
internal static class DeployPaths
{
    internal const string DeployUser = "brandon";
    internal const string WebsiteDirectory = "/opt/dotsider-website";
    internal const string WebsiteExecutablePath = WebsiteDirectory + "/Dotsider.Website";
    internal const string DocsDirectory = "/var/www/dotsider-docs";
    internal const string SampleDirectory = WebsiteDirectory + "/sample";
    internal const string SampleBackupDirectory = WebsiteDirectory + "/sample.bak";
    internal const string SampleManifestPath = WebsiteDirectory + "/sample.sha256";
    internal const string WebsiteService = "dotsider-website";
    internal const string IntegrityService = "integrity-check.service";
    internal const string IntegrityTimer = "integrity-check.timer";
    internal const string ReportTimer = "caddy-report.timer";
    internal const string InstalledHostPath = "/usr/local/libexec/dotsider-deploy-host";
    internal const string MetricsLogPath = "/var/log/caddy-metrics.log";
    internal const string IntegrityLogPath = "/var/log/integrity-check.log";
    internal const string PrometheusUrl = "http://localhost:9090";
    internal const string WebsiteHealthUrl = "http://localhost:5100/health";
}
