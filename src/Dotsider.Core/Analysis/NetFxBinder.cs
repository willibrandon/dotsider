using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// CLR-accurate .NET Framework 4.x assembly binder. Consumes a <see cref="NetFxBindingContext"/>
/// and produces a <see cref="NetFxBindResult"/> matching what the actual .NET Framework binder
/// would do at runtime: framework unification + machine.config + publisher policy + app config
/// (in CLR walk order, with later layers overriding earlier ones), then locate against the GAC
/// (architecture-prioritized, strong-named only), then the Framework[64] runtime directory, then
/// configured codeBase href (fail-fast), then the application base + private paths with
/// culture-aware probing. .NET Core / .NET 5+ roots never construct a binding context, so this
/// type is never invoked for them and their probe chain is unchanged.
/// </summary>
public static class NetFxBinder
{
    private sealed class BindCaches
    {
        public ConcurrentDictionary<AssemblyRefInfo, NetFxBindResult> RequestedBindCache { get; } = new();
        public ConcurrentDictionary<AssemblyRefInfo, LoadedAssemblyEntry> LoadedAssemblyCache { get; } = new();
        public int FilesystemProbeCount;
    }

    private static readonly ConditionalWeakTable<NetFxBindingContext, BindCaches> Caches = [];

    private static BindCaches GetCaches(NetFxBindingContext ctx) =>
        Caches.GetValue(ctx, _ => new BindCaches());

    /// <summary>
    /// Filesystem probe count for the supplied context. Test-only diagnostic that proves
    /// repeated <see cref="Bind"/> calls hit the cache without re-walking the filesystem.
    /// </summary>
    /// <param name="ctx">The binding context to inspect.</param>
    /// <returns>The number of filesystem probes performed for <paramref name="ctx"/>.</returns>
    public static int GetProbeCount(NetFxBindingContext ctx) => GetCaches(ctx).FilesystemProbeCount;

    /// <summary>
    /// Clears all per-context caches (RequestedBindCache, LoadedAssemblyCache, probe counter).
    /// Test-only diagnostic for resetting state between assertions.
    /// </summary>
    /// <param name="ctx">The binding context whose caches to clear.</param>
    public static void ClearCaches(NetFxBindingContext ctx)
    {
        var caches = GetCaches(ctx);
        caches.RequestedBindCache.Clear();
        caches.LoadedAssemblyCache.Clear();
        caches.FilesystemProbeCount = 0;
    }

    /// <summary>
    /// Binds the requested assembly identity through the supplied .NET Framework binding policy
    /// and locates the file the CLR would actually load.
    /// </summary>
    /// <param name="requested">The identity exactly as named by the metadata reference.</param>
    /// <param name="ctx">The binding context built from the analyzed root.</param>
    /// <returns>The bind outcome.</returns>
    public static NetFxBindResult Bind(AssemblyRefInfo requested, NetFxBindingContext ctx)
    {
        var caches = GetCaches(ctx);
        if (caches.RequestedBindCache.TryGetValue(requested, out var cached))
            return cached;

        var (effective, applied) = ctx.Policy.Apply(requested, ctx.EffectiveArchitecture);
        var result = Locate(requested, effective, applied, ctx, caches);
        caches.RequestedBindCache[requested] = result;
        return result;
    }

    private static NetFxBindResult Locate(
        AssemblyRefInfo requested,
        AssemblyRefInfo effective,
        AppliedPolicy? applied,
        NetFxBindingContext ctx,
        BindCaches caches)
    {
        var isStrongNamed = !string.IsNullOrEmpty(effective.PublicKeyToken);

        if (isStrongNamed)
        {
            // mscorlib is the bootstrap assembly: the CLR always loads it from the architecture-
            // correct .NET Framework runtime directory, even though a GAC copy exists. Probe the
            // runtime directory first for that one identity so we match real load behavior.
            if (string.Equals(effective.Name, "mscorlib", StringComparison.OrdinalIgnoreCase) &&
                TryFrameworkRuntimeDir(effective, ctx, caches) is { } mscorlibPath)
                return Success(requested, effective, applied,
                    mscorlibPath, AssemblyProvenance.FrameworkRuntimeDirectory, caches);

            if (TryGac(effective, ctx, caches) is { } gacPath)
                return Success(requested, effective, applied, gacPath, AssemblyProvenance.Gac, caches);

            if (TryFrameworkRuntimeDir(effective, ctx, caches) is { } frameworkPath)
                return Success(requested, effective, applied,
                    frameworkPath, AssemblyProvenance.FrameworkRuntimeDirectory, caches);

            var cb = ctx.Policy.FindCodeBaseFor(effective);
            if (cb is not null)
            {
                var resolved = ResolveCodeBaseHref(cb.Href, ctx.AppBaseDirectory);
                caches.FilesystemProbeCount++;
                // Carry the configured codeBase href on the result regardless of success or
                // failure so the UI can render "redirected → codeBase href" in both cases. The
                // success branch picks up the bound version; the failure branch keeps the
                // pre-codeBase version (we never loaded the file, so the requested → bound
                // delta is still whatever the redirect chain produced before codeBase ran).
                var appliedWithCb = applied is null
                    ? new AppliedPolicy(
                        PolicyLayer.CodeBase,
                        ParseVersionOrZero(requested.Version),
                        ParseVersionOrZero(effective.Version),
                        cb.Href)
                    : applied with { CodeBaseHref = cb.Href };

                if (resolved is not null && File.Exists(resolved))
                {
                    var actual = TryReadIdentity(resolved);
                    if (actual is not null && IdentityMatches(effective, actual.Value))
                        return Success(requested, effective, appliedWithCb, resolved,
                            AssemblyProvenance.CodeBase, caches);
                }
                return new NetFxBindResult(
                    Requested: requested,
                    EffectiveAfterPolicy: effective,
                    Loaded: null,
                    LoadedPath: null,
                    Provenance: AssemblyProvenance.CodeBaseMissing,
                    AppliedPolicy: appliedWithCb,
                    FailureReason: $"codeBase href '{cb.Href}' does not exist or its identity did not match",
                    CandidateProbePath: cb.Href);
            }
        }

        if (TryAppBaseAndPrivatePaths(effective, ctx, caches) is { } appLocalPath)
        {
            var actual = TryReadIdentity(appLocalPath);
            if (actual is not null && IdentityMatches(effective, actual.Value))
                return Success(requested, effective, applied,
                    appLocalPath, AssemblyProvenance.AppLocal, caches);

            return new NetFxBindResult(
                Requested: requested,
                EffectiveAfterPolicy: effective,
                Loaded: null,
                LoadedPath: null,
                Provenance: AssemblyProvenance.IdentityMismatch,
                AppliedPolicy: applied,
                FailureReason: $"simple-name match at '{appLocalPath}' has identity that does not match the effective bind",
                CandidateProbePath: appLocalPath);
        }

        return Failure(requested, effective, applied, AssemblyProvenance.Unresolved,
            failureReason: "no probe produced a candidate file for the effective identity");
    }

    private static NetFxBindResult Success(
        AssemblyRefInfo requested, AssemblyRefInfo effective, AppliedPolicy? applied,
        string path, AssemblyProvenance provenance, BindCaches caches)
    {
        var loaded = effective;
        var entry = caches.LoadedAssemblyCache.GetOrAdd(loaded, l => new LoadedAssemblyEntry(l, path));
        return new NetFxBindResult(
            Requested: requested,
            EffectiveAfterPolicy: effective,
            Loaded: entry.Identity,
            LoadedPath: entry.Path,
            Provenance: provenance,
            AppliedPolicy: applied,
            FailureReason: null,
            CandidateProbePath: null);
    }

    private static NetFxBindResult Failure(
        AssemblyRefInfo requested, AssemblyRefInfo effective, AppliedPolicy? applied,
        AssemblyProvenance provenance, string failureReason) =>
        new(Requested: requested,
            EffectiveAfterPolicy: effective,
            Loaded: null,
            LoadedPath: null,
            Provenance: provenance,
            AppliedPolicy: applied,
            FailureReason: failureReason,
            CandidateProbePath: null);

    private static string? TryGac(AssemblyRefInfo effective, NetFxBindingContext ctx, BindCaches caches)
    {
        if (string.IsNullOrEmpty(effective.PublicKeyToken)) return null;
        var pkt = effective.PublicKeyToken!.ToLowerInvariant();
        var version = effective.Version;
        // GAC layout: v4.0_<version>_<culture>__<pkt> when culture is non-neutral,
        // v4.0_<version>__<pkt> (no culture, double underscore separates version from PKT) when neutral.
        var isNeutral = string.IsNullOrEmpty(effective.Culture)
                     || string.Equals(effective.Culture, "neutral", StringComparison.OrdinalIgnoreCase);
        var token = isNeutral
            ? $"v4.0_{version}__{pkt}"
            : $"v4.0_{version}_{effective.Culture}__{pkt}";
        foreach (var subdir in ctx.GacScanList())
        {
            var candidate = Path.Combine(subdir, effective.Name, token, $"{effective.Name}.dll");
            caches.FilesystemProbeCount++;
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? TryFrameworkRuntimeDir(
        AssemblyRefInfo effective, NetFxBindingContext ctx, BindCaches caches)
    {
        var dir = ctx.FrameworkRuntimeDirectory();
        if (dir is null) return null;
        var candidate = Path.Combine(dir, $"{effective.Name}.dll");
        caches.FilesystemProbeCount++;
        if (!File.Exists(candidate)) return null;
        var actual = TryReadIdentity(candidate);
        return actual is not null && IdentityMatches(effective, actual.Value) ? candidate : null;
    }

    private static string? TryAppBaseAndPrivatePaths(
        AssemblyRefInfo effective, NetFxBindingContext ctx, BindCaches caches)
    {
        var name = effective.Name;
        var culture = string.IsNullOrEmpty(effective.Culture) || string.Equals(effective.Culture, "neutral", StringComparison.OrdinalIgnoreCase)
            ? null
            : effective.Culture;

        if (culture is null)
        {
            foreach (var path in NeutralAppBaseProbePaths(ctx, name))
            {
                caches.FilesystemProbeCount++;
                if (File.Exists(path)) return path;
            }
            return null;
        }

        foreach (var path in CulturedAppBaseProbePaths(ctx, name, culture))
        {
            caches.FilesystemProbeCount++;
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static IEnumerable<string> NeutralAppBaseProbePaths(NetFxBindingContext ctx, string name)
    {
        yield return Path.Combine(ctx.AppBaseDirectory, $"{name}.dll");
        yield return Path.Combine(ctx.AppBaseDirectory, $"{name}.exe");
        yield return Path.Combine(ctx.AppBaseDirectory, name, $"{name}.dll");
        yield return Path.Combine(ctx.AppBaseDirectory, name, $"{name}.exe");
        foreach (var p in ctx.PrivatePaths)
        {
            yield return Path.Combine(ctx.AppBaseDirectory, p, $"{name}.dll");
            yield return Path.Combine(ctx.AppBaseDirectory, p, $"{name}.exe");
            yield return Path.Combine(ctx.AppBaseDirectory, p, name, $"{name}.dll");
            yield return Path.Combine(ctx.AppBaseDirectory, p, name, $"{name}.exe");
        }
    }

    private static IEnumerable<string> CulturedAppBaseProbePaths(NetFxBindingContext ctx, string name, string culture)
    {
        yield return Path.Combine(ctx.AppBaseDirectory, culture, $"{name}.dll");
        yield return Path.Combine(ctx.AppBaseDirectory, culture, $"{name}.exe");
        yield return Path.Combine(ctx.AppBaseDirectory, culture, name, $"{name}.dll");
        yield return Path.Combine(ctx.AppBaseDirectory, culture, name, $"{name}.exe");
        yield return Path.Combine(ctx.AppBaseDirectory, culture, $"{name}.resources.dll");
        foreach (var p in ctx.PrivatePaths)
        {
            yield return Path.Combine(ctx.AppBaseDirectory, p, culture, $"{name}.dll");
            yield return Path.Combine(ctx.AppBaseDirectory, p, culture, $"{name}.exe");
            yield return Path.Combine(ctx.AppBaseDirectory, p, culture, name, $"{name}.dll");
            yield return Path.Combine(ctx.AppBaseDirectory, p, culture, name, $"{name}.exe");
            yield return Path.Combine(ctx.AppBaseDirectory, p, culture, $"{name}.resources.dll");
        }
    }

    private static string? ResolveCodeBaseHref(string href, string appBase)
    {
        if (Path.IsPathRooted(href)) return href;
        if (href.Contains("://"))
        {
            if (href.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(href[8..]).Replace('/', Path.DirectorySeparatorChar);
            if (href.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(href[7..]).Replace('/', Path.DirectorySeparatorChar);
            return null;
        }
        return Path.Combine(appBase, href.Replace('/', Path.DirectorySeparatorChar));
    }

    private static (string Name, string Version, string Culture, string? PublicKeyToken)? TryReadIdentity(string path)
    {
        try
        {
            using var analyzer = new AssemblyAnalyzer(path);
            if (!analyzer.HasMetadata || analyzer.AssemblyName is null) return null;
            return (analyzer.AssemblyName,
                    analyzer.AssemblyVersion ?? string.Empty,
                    analyzer.Culture ?? "neutral",
                    analyzer.PublicKeyToken);
        }
        catch { return null; }
    }

    private static bool IdentityMatches(
        AssemblyRefInfo effective,
        (string Name, string Version, string Culture, string? PublicKeyToken) actual)
    {
        if (!string.Equals(effective.Name, actual.Name, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(effective.Version, actual.Version, StringComparison.Ordinal)) return false;
        var requestedCulture = string.IsNullOrEmpty(effective.Culture) ? "neutral" : effective.Culture;
        var actualCulture = string.IsNullOrEmpty(actual.Culture) ? "neutral" : actual.Culture;
        if (!string.Equals(requestedCulture, actualCulture, StringComparison.OrdinalIgnoreCase)) return false;
        var rPkt = effective.PublicKeyToken ?? string.Empty;
        var aPkt = actual.PublicKeyToken ?? string.Empty;
        return string.Equals(rPkt, aPkt, StringComparison.OrdinalIgnoreCase);
    }

    private static Version ParseVersionOrZero(string s) =>
        Version.TryParse(s, out var v) ? v : new Version(0, 0, 0, 0);
}
