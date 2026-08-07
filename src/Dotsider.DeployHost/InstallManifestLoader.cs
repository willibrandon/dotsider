using System.Reflection;
using System.Text.Json;

namespace Dotsider.DeployHost;

/// <summary>
/// Loads and validates the installation manifest embedded in the deploy host.
/// Validation rejects malformed ownership, modes, resources, and destinations.
/// The resulting entries are safe inputs for privileged file installation.
/// </summary>
internal static class InstallManifestLoader
{
    internal const string ManifestResourceName = "Dotsider.DeployHost.install-manifest.json";
    private const string AssetResourcePrefix = "Dotsider.DeployHost.Assets.";

    /// <summary>
    /// Reads the embedded installation manifest using source-generated JSON metadata.
    /// Every entry is checked before any privileged filesystem operation begins.
    /// A valid manifest contains unique resources and destinations.
    /// </summary>
    /// <param name="assembly">The assembly containing deployment resources.</param>
    /// <returns>The validated installation manifest.</returns>
    internal static InstallManifest Load(Assembly? assembly = null)
    {
        assembly ??= typeof(InstallManifestLoader).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ManifestResourceName}' is missing.");
        InstallManifest manifest = JsonSerializer.Deserialize(
            stream,
            DeployHostJsonContext.Default.InstallManifest)
            ?? throw new InvalidOperationException("The installation manifest is empty.");
        Validate(manifest, assembly.GetManifestResourceNames());
        return manifest;
    }

    private static void Validate(InstallManifest manifest, IReadOnlyCollection<string> resources)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported installation manifest schema version {manifest.SchemaVersion}.");
        }

        if (manifest.Files.Length == 0 || manifest.Files.Length > 32)
        {
            throw new InvalidOperationException("The installation manifest must contain between 1 and 32 files.");
        }

        var resourceSet = new HashSet<string>(StringComparer.Ordinal);
        var destinationSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (InstallFile file in manifest.Files)
        {
            if (!file.Resource.StartsWith(AssetResourcePrefix, StringComparison.Ordinal)
                || !resources.Contains(file.Resource, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Unknown embedded asset '{file.Resource}'.");
            }

            if (!resourceSet.Add(file.Resource) || !destinationSet.Add(file.Destination))
            {
                throw new InvalidOperationException("Installation resources and destinations must be unique.");
            }

            ValidateDestination(file.Destination);
            if (!file.Mode.Equals("0644", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported mode '{file.Mode}'.");
            }

            if (!file.Owner.Equals("root", StringComparison.Ordinal)
                || !file.Group.Equals("root", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Installed configuration assets must be owned by root:root.");
            }
        }
    }

    private static void ValidateDestination(string destination)
    {
        if (destination.Length == 0
            || destination[0] != '/'
            || destination.Contains("..", StringComparison.Ordinal)
            || destination.Contains('\0'))
        {
            throw new InvalidOperationException($"Unsafe installation destination '{destination}'.");
        }

        bool permitted = destination.Equals("/etc/caddy/Caddyfile", StringComparison.Ordinal)
            || destination.Equals("/etc/prometheus/prometheus.yml", StringComparison.Ordinal)
            || destination.Equals("/etc/logrotate.d/caddy-metrics", StringComparison.Ordinal)
            || destination.StartsWith("/etc/systemd/system/", StringComparison.Ordinal);
        if (!permitted)
        {
            throw new InvalidOperationException($"Installation destination '{destination}' is not permitted.");
        }
    }
}
