/// <summary>
/// Holds validated inputs for one website deployment utility operation.
/// Secret values are read from the environment and are never included in diagnostics.
/// Artifact paths are resolved before any remote operation begins.
/// </summary>
internal sealed record DeploymentOptions(
    string Mode,
    string RepositoryRoot,
    string Rid,
    string DeployHostPath,
    string DocsPath,
    string WebsitePath,
    string SamplePath,
    string Host,
    string User,
    string SshKey)
{
    /// <summary>
    /// Parses command-line artifact paths and environment-based connection settings.
    /// Package mode does not require a remote host or credentials.
    /// Remote modes reject host values that could alter SSH argument parsing.
    /// </summary>
    /// <param name="args">The utility arguments.</param>
    /// <returns>The validated deployment options.</returns>
    internal static DeploymentOptions Parse(string[] args)
    {
        (Dictionary<string, List<string>> values, _) = ScriptSupport.ParseArguments(
            args,
            ["Mode", "Rid", "DeployHost", "Docs", "Website", "Sample", "Host"],
            [],
            []);
        string repositoryRoot = ScriptSupport.FindRepositoryRoot();
        string mode = ScriptSupport.GetString(values, "Mode");
        if (mode is not ("Package" or "Provision" or "Preflight" or "Deploy"))
        {
            throw new ArgumentException("-Mode must be Package, Provision, Preflight, or Deploy.");
        }

        string rid = ScriptSupport.GetString(values, "Rid", "linux-x64");
        if (rid is not ("linux-x64" or "linux-arm64"))
        {
            throw new ArgumentException("-Rid must be linux-x64 or linux-arm64.");
        }

        string deployHost = ResolvePath(values, "DeployHost", repositoryRoot, "publish/deploy-host/dotsider-deploy-host", mode != "Package");
        string docs = ResolvePath(values, "Docs", repositoryRoot, "docs-site", mode == "Deploy");
        string website = ResolvePath(values, "Website", repositoryRoot, "website-server", mode == "Deploy");
        string sample = ResolvePath(values, "Sample", repositoryRoot, "website-sample", mode == "Deploy");
        string host = ScriptSupport.GetString(
            values,
            "Host",
            Environment.GetEnvironmentVariable("DEPLOY_HOST") ?? string.Empty);
        string user = mode == "Provision" ? "root" : "brandon";
        string sshKey = Environment.GetEnvironmentVariable("DEPLOY_SSH_KEY") ?? string.Empty;

        if (mode != "Package")
        {
            if (string.IsNullOrWhiteSpace(host)
                || host.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-')))
            {
                throw new ArgumentException("DEPLOY_HOST must be a DNS name or address containing only letters, digits, dots, and hyphens.");
            }

            if (string.IsNullOrWhiteSpace(sshKey))
            {
                throw new ArgumentException("DEPLOY_SSH_KEY is required.");
            }
        }

        return new DeploymentOptions(mode, repositoryRoot, rid, deployHost, docs, website, sample, host, user, sshKey);
    }

    private static string ResolvePath(
        Dictionary<string, List<string>> values,
        string option,
        string repositoryRoot,
        string defaultValue,
        bool mustExist)
    {
        string value = ScriptSupport.GetString(values, option, defaultValue);
        string path = Path.GetFullPath(Path.IsPathFullyQualified(value) ? value : Path.Combine(repositoryRoot, value));
        if (mustExist && !File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"{option} path '{path}' does not exist.");
        }

        return path;
    }
}
