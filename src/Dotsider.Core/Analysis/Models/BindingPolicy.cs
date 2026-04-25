using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Aggregated .NET Framework binding policy assembled from framework unification, machine.config,
/// publisher-policy assemblies, and the application configuration file. Layers are stored in
/// document order with first-match semantics — the same model the CLR applies — and later layers
/// (machine.config &gt; publisher &gt; app &gt; framework unification) override earlier ones when
/// they target the same identity.
/// </summary>
/// <param name="AppConfigRedirects">Redirects parsed from <c>*.exe.config</c>/<c>*.dll.config</c>.</param>
/// <param name="PublisherPolicyRedirects">
/// Redirects parsed from <c>policy.&lt;major&gt;.&lt;minor&gt;.&lt;simpleName&gt;</c> assemblies in the GAC.
/// </param>
/// <param name="MachineConfigRedirects">Redirects parsed from the architecture-correct <c>machine.config</c>.</param>
/// <param name="FrameworkUnificationRedirects">
/// Redirects produced by the CLR's built-in unification of well-known framework PKTs.
/// </param>
/// <param name="CodeBases">
/// CodeBase entries from any policy layer, ordered by precedence (machine &gt; publisher &gt; app).
/// </param>
/// <param name="PublisherPolicyDisabledFor">
/// Identities for which the application configuration set
/// <c>&lt;publisherPolicy apply="no"/&gt;</c> on a specific
/// <c>&lt;dependentAssembly&gt;</c>. Bypasses the publisher-policy layer for those identities.
/// </param>
/// <param name="PublisherPolicyDisabledGlobally">
/// <see langword="true"/> when the application configuration set runtime-scoped
/// <c>&lt;publisherPolicy apply="no"/&gt;</c>, suppressing publisher policy for every bind in
/// the app domain — including identities that have no <c>&lt;dependentAssembly&gt;</c> block.
/// </param>
/// <param name="FrameworkUnificationTable">
/// Per-identity unification table built by scanning <c>Framework[64]\v4.0.30319</c> at policy
/// load time: maps <c>(Name, PublicKeyToken)</c> for in-box framework assemblies (PKT in
/// <see cref="AssemblyAnalyzer.FrameworkUnificationPublicKeyTokens"/>) to the version actually
/// shipped in the runtime directory. <see cref="Apply"/> consults this map first; references
/// at versions less than or equal to the table version unify to the table version, so a
/// subsequent GAC lookup finds the file at its real GAC location instead of falling through to
/// a post-hoc framework-directory match.
/// </param>
public sealed record BindingPolicy(
    IReadOnlyList<BindingRedirect> AppConfigRedirects,
    IReadOnlyList<BindingRedirect> PublisherPolicyRedirects,
    IReadOnlyList<BindingRedirect> MachineConfigRedirects,
    IReadOnlyList<BindingRedirect> FrameworkUnificationRedirects,
    IReadOnlyList<CodeBaseEntry> CodeBases,
    IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> PublisherPolicyDisabledFor,
    bool PublisherPolicyDisabledGlobally = false,
    IReadOnlyDictionary<(string Name, string PublicKeyToken), Version>? FrameworkUnificationTable = null)
{
    /// <summary>An empty policy — no redirects, no codeBase, no publisher-policy bypasses.</summary>
    public static BindingPolicy Empty { get; } = new(
        [],
        [],
        [],
        [],
        [],
        []);

    /// <summary>
    /// Resolves the effective identity for the requested reference by walking the policy layers
    /// in CLR walk order — app config first, then publisher policy (skipped if bypassed for this
    /// identity), then machine.config — with later layers overriding earlier ones. Framework
    /// unification supplies the baseline mapping when no later layer rewrites the identity. The
    /// returned tuple includes which layer produced the rewrite, so callers can attach an
    /// <see cref="AppliedPolicy"/> to the resolution result.
    /// </summary>
    /// <param name="requested">The identity exactly as named by the metadata reference.</param>
    /// <param name="architecture">
    /// Effective process bitness, used to filter <c>processorArchitecture</c> entries.
    /// </param>
    /// <returns>
    /// The effective identity and the policy layer that produced any rewrite.
    /// <see cref="AppliedPolicy"/> is <see langword="null"/> when no layer rewrote the identity.
    /// </returns>
    public (AssemblyRefInfo Effective, AppliedPolicy? Applied) Apply(
        AssemblyRefInfo requested, NetFxArchitecture architecture)
    {
        if (!Version.TryParse(requested.Version, out var requestedVersion))
            return (requested, null);

        // Walk the layers in CLR order: framework unification (baseline) → app config → publisher
        // policy → machine.config. Each layer matches against the version produced by the
        // previous layer (chain semantics): if app config rewrites 1.0 → 2.0 and publisher policy
        // covers 2.0 → 3.0, both rewrites apply and the binder ends up at 3.0.
        var current = requested;
        var currentVersion = requestedVersion;
        BindingRedirect? lastWinner = null;
        AppliedPolicy? unificationApplied = null;

        // Framework unification: per-identity table built from the framework runtime directory.
        // For in-box framework assemblies the CLR rewrites the request to whatever ships in
        // Framework[64]\v4.0.30319 regardless of direction — verified against live net48:
        //   Microsoft.VisualBasic 8.0.0.0 → 10.0.0.0 (rolls up)
        //   System.IO.Compression 4.2.0.0 → 4.0.0.0 (rolls down)
        //   mscorlib 8.0.0.0          → 4.0.0.0 (rolls down)
        // Compatibility-pack PKTs (cc7b13ffcd2ddd51, adb9793829ddae60) are excluded from the
        // unification PKT set, so System.ValueTuple 4.1.0.0 still fails as the CLR does.
        if (FrameworkUnificationTable is not null
            && !string.IsNullOrEmpty(requested.PublicKeyToken)
            && FrameworkUnificationTable.TryGetValue((requested.Name, requested.PublicKeyToken!), out var unifiedVersion)
            && requestedVersion != unifiedVersion)
        {
            current = current with { Version = unifiedVersion.ToString() };
            currentVersion = unifiedVersion;
            unificationApplied = new AppliedPolicy(
                PolicyLayer.FrameworkUnification,
                requestedVersion,
                unifiedVersion,
                CodeBaseHref: null);
        }

        if (TryApplyLayer(FrameworkUnificationRedirects, current, currentVersion, architecture)
            is { } fu)
        {
            current = current with { Version = fu.NewVersion.ToString() };
            currentVersion = fu.NewVersion;
            lastWinner = fu;
        }
        if (TryApplyLayer(AppConfigRedirects, current, currentVersion, architecture) is { } app)
        {
            current = current with { Version = app.NewVersion.ToString() };
            currentVersion = app.NewVersion;
            lastWinner = app;
        }
        if (!IsPublisherPolicyDisabled(requested) &&
            TryApplyLayer(PublisherPolicyRedirects, current, currentVersion, architecture)
                is { } pub)
        {
            current = current with { Version = pub.NewVersion.ToString() };
            currentVersion = pub.NewVersion;
            lastWinner = pub;
        }
        if (TryApplyLayer(MachineConfigRedirects, current, currentVersion, architecture)
            is { } mac)
        {
            current = current with { Version = mac.NewVersion.ToString() };
            currentVersion = mac.NewVersion;
            lastWinner = mac;
        }

        if (lastWinner is null)
            return (current, unificationApplied);

        var applied = new AppliedPolicy(
            lastWinner.Source, requestedVersion, currentVersion, CodeBaseHref: null);
        return (current, applied);
    }

    /// <summary>
    /// Returns the <c>&lt;codeBase&gt;</c> entry that anchors the supplied effective identity, or
    /// <see langword="null"/> when no codeBase is configured for that identity.
    /// </summary>
    /// <param name="effective">The post-policy identity to look up.</param>
    /// <returns>A matching <see cref="CodeBaseEntry"/>, or <see langword="null"/>.</returns>
    public CodeBaseEntry? FindCodeBaseFor(AssemblyRefInfo effective)
    {
        if (!Version.TryParse(effective.Version, out var version))
            return null;
        foreach (var cb in CodeBases)
        {
            if (!string.Equals(cb.Name, effective.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!PktEquals(cb.PublicKeyToken, effective.PublicKeyToken)) continue;
            if (!CultureEquals(cb.Culture, effective.Culture)) continue;
            if (cb.Version == version) return cb;
        }
        return null;
    }

    /// <summary>
    /// Loads policy from the analyzer's app/exe config plus machine.config and any publisher-policy
    /// assemblies discovered in the supplied GAC roots. Errors are handled per CLR semantics:
    /// malformed XML at the document level yields an empty policy for that source; individual
    /// invalid <c>&lt;dependentAssembly&gt;</c>/<c>&lt;bindingRedirect&gt;</c> sections are silently
    /// dropped and the rest of the file continues to apply.
    /// </summary>
    /// <param name="appConfigPath">Path to the application configuration file, or <see langword="null"/>.</param>
    /// <param name="architecture">Effective process bitness, controls which <c>machine.config</c> to read.</param>
    /// <param name="gacRoots">GAC root directories to scan for publisher-policy assemblies.</param>
    /// <returns>A populated <see cref="BindingPolicy"/>.</returns>
    public static BindingPolicy LoadFrom(
        string? appConfigPath,
        NetFxArchitecture architecture,
        IReadOnlyList<string> gacRoots)
    {
        var app = ParseConfigFile(appConfigPath, PolicyLayer.AppConfig);

        var machineConfigPath = MachineConfigPathFor(architecture);
        var machine = ParseConfigFile(machineConfigPath, PolicyLayer.MachineConfig);

        var publisherRedirects = new List<BindingRedirect>();
        var publisherCodeBases = new List<CodeBaseEntry>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var (rs, cs) in EnumeratePublisherPolicies(gacRoots, architecture))
            {
                publisherRedirects.AddRange(rs);
                publisherCodeBases.AddRange(cs);
            }
        }

        var codeBasesAll = new List<CodeBaseEntry>(machine.CodeBases);
        codeBasesAll.AddRange(publisherCodeBases);
        codeBasesAll.AddRange(app.CodeBases);

        return new BindingPolicy(
            AppConfigRedirects: app.Redirects,
            PublisherPolicyRedirects: publisherRedirects,
            MachineConfigRedirects: machine.Redirects,
            FrameworkUnificationRedirects: [],
            CodeBases: codeBasesAll,
            PublisherPolicyDisabledFor: app.Disabled,
            PublisherPolicyDisabledGlobally: app.PublisherPolicyDisabledGlobally,
            FrameworkUnificationTable: BuildFrameworkUnificationTable(architecture));
    }

    /// <summary>
    /// Parses a single configuration file (app config, machine.config, or a publisher-policy
    /// assembly's embedded XML resource) into a <see cref="BindingPolicyParseResult"/>.
    /// Exposed so callers that already have the file path can avoid re-parsing.
    /// </summary>
    /// <param name="path">Path to the configuration file, or <see langword="null"/>.</param>
    /// <param name="source">Policy layer to attribute parsed entries to.</param>
    /// <returns>The parsed result; an empty result on missing file or malformed XML.</returns>
    public static BindingPolicyParseResult ParseConfigFile(string? path, PolicyLayer source)
    {
        var redirects = new List<BindingRedirect>();
        var codeBases = new List<CodeBaseEntry>();
        var disabled = new List<(string, string?, string)>();
        var privatePaths = new List<string>();
        var globallyDisabled = false;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled);

        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.None);
        }
        catch (XmlException) { return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled); }
        catch (IOException) { return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled); }
        catch (UnauthorizedAccessException) { return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled); }

        if (doc.Root is null || !string.Equals(doc.Root.Name.LocalName, "configuration", StringComparison.Ordinal))
            return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled);

        globallyDisabled = ParseRuntimeElement(doc.Root, source, redirects, codeBases, disabled, privatePaths);
        return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled);
    }

    private bool IsPublisherPolicyDisabled(AssemblyRefInfo requested)
    {
        // Runtime-scoped <publisherPolicy apply="no"/> suppresses publisher policy for every
        // bind in the AppDomain, including identities with no <dependentAssembly> block.
        if (PublisherPolicyDisabledGlobally) return true;

        foreach (var d in PublisherPolicyDisabledFor)
        {
            if (string.Equals(d.Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
                PktEquals(d.PublicKeyToken, requested.PublicKeyToken) &&
                CultureEquals(d.Culture, requested.Culture))
                return true;
        }
        return false;
    }

    private static BindingRedirect? TryApplyLayer(
        IReadOnlyList<BindingRedirect> entries,
        AssemblyRefInfo requested,
        Version requestedVersion,
        NetFxArchitecture architecture)
    {
        // CLR semantics: first matching <dependentAssembly> in document order wins within a layer.
        // Framework-unification entries use Name == "*" as a wildcard — they apply to any simple
        // name that carries the well-known framework PKT.
        foreach (var r in entries)
        {
            var nameMatches = r.Name == "*"
                || string.Equals(r.Name, requested.Name, StringComparison.OrdinalIgnoreCase);
            if (!nameMatches) continue;
            if (!PktEquals(r.PublicKeyToken, requested.PublicKeyToken)) continue;
            if (!CultureEquals(r.Culture, requested.Culture)) continue;
            if (!ArchitectureMatches(r.ProcessorArchitecture, architecture)) continue;
            if (requestedVersion < r.OldMin || requestedVersion > r.OldMax) continue;
            return r;
        }
        return null;
    }

    private static bool PktEquals(string? a, string? b) =>
        string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static bool CultureEquals(string a, string? b) =>
        string.Equals(
            string.IsNullOrEmpty(a) ? "neutral" : a,
            string.IsNullOrEmpty(b) ? "neutral" : b,
            StringComparison.OrdinalIgnoreCase);

    private static bool ArchitectureMatches(string? entryArch, NetFxArchitecture root)
    {
        if (string.IsNullOrEmpty(entryArch)) return true;
        if (string.Equals(entryArch, "msil", StringComparison.OrdinalIgnoreCase)) return true;
        return root switch
        {
            NetFxArchitecture.X86 => string.Equals(entryArch, "x86", StringComparison.OrdinalIgnoreCase),
            NetFxArchitecture.Amd64 => string.Equals(entryArch, "amd64", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(entryArch, "x64", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    /// <summary>
    /// Builds the per-identity framework unification table by reading every <c>*.dll</c> in
    /// the architecture-correct .NET Framework runtime directory and recording the actual
    /// version of each in-box framework assembly (PKT in
    /// <see cref="AssemblyAnalyzer.FrameworkUnificationPublicKeyTokens"/>). Captures the
    /// CLR-accurate unification mapping — e.g. <c>Microsoft.VisualBasic</c> ships at
    /// <c>10.0.0.0</c>, so a request at <c>8.0.0.0</c> rolls forward to <c>10.0.0.0</c>; the
    /// later GAC scan then locates the file at its real GAC slot
    /// (<c>v4.0_10.0.0.0__b03f5f7f11d50a3a</c>) instead of falling back to the framework dir.
    /// </summary>
    private static Dictionary<(string Name, string PublicKeyToken), Version> BuildFrameworkUnificationTable(
        NetFxArchitecture architecture)
    {
        var table = new Dictionary<(string, string), Version>(
            new FrameworkUnificationKeyComparer());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return table;

        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return table;
        var subdir = architecture == NetFxArchitecture.X86 ? "Framework" : "Framework64";
        var dir = Path.Combine(windir!, "Microsoft.NET", subdir, "v4.0.30319");
        if (!Directory.Exists(dir)) return table;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(dir, "*.dll"); }
        catch (UnauthorizedAccessException) { return table; }
        catch (IOException) { return table; }

        foreach (var file in files)
        {
            var identity = TryReadAssemblyIdentity(file);
            if (identity is null) continue;
            if (string.IsNullOrEmpty(identity.Value.PublicKeyToken)) continue;
            if (!AssemblyAnalyzer.FrameworkUnificationPublicKeyTokens.Contains(identity.Value.PublicKeyToken!))
                continue;
            if (!Version.TryParse(identity.Value.Version, out var v)) continue;
            // Preserve the highest version when the dir somehow contains duplicates.
            var key = (identity.Value.Name, identity.Value.PublicKeyToken!);
            if (!table.TryGetValue(key, out var existing) || v > existing)
                table[key] = v;
        }
        return table;
    }

    private sealed class FrameworkUnificationKeyComparer
        : IEqualityComparer<(string Name, string PublicKeyToken)>
    {
        public bool Equals((string Name, string PublicKeyToken) x, (string Name, string PublicKeyToken) y) =>
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.PublicKeyToken, y.PublicKeyToken, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, string PublicKeyToken) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicKeyToken));
    }

    private static (string Name, string Version, string Culture, string? PublicKeyToken)?
        TryReadAssemblyIdentity(string path)
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

    /// <summary>
    /// Parses every <c>&lt;runtime&gt;</c> element in the configuration document.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when any <c>&lt;runtime&gt;</c> element carried a top-level
    /// <c>&lt;publisherPolicy apply="no"/&gt;</c> directive (suppresses publisher policy globally).
    /// </returns>
    private static bool ParseRuntimeElement(
        XElement configRoot,
        PolicyLayer source,
        List<BindingRedirect> redirects,
        List<CodeBaseEntry> codeBases,
        List<(string, string?, string)> disabled,
        List<string> privatePaths)
    {
        var anyGlobalDisable = false;
        foreach (var runtime in configRoot.Elements().Where(e => e.Name.LocalName == "runtime"))
        {
            // <publisherPolicy apply="no"/> at runtime scope disables for every bind in the
            // AppDomain regardless of <dependentAssembly> blocks. Capture per-document and
            // surface it via the parse result so BindingPolicy can flip the global flag.
            var globalPublisherPolicyDisabled = false;
            foreach (var pp in runtime.Elements().Where(e => e.Name.LocalName == "publisherPolicy"))
            {
                if (string.Equals(pp.Attribute("apply")?.Value, "no", StringComparison.OrdinalIgnoreCase))
                {
                    globalPublisherPolicyDisabled = true;
                    anyGlobalDisable = true;
                }
            }

            foreach (var binding in runtime.Elements().Where(e => e.Name.LocalName == "assemblyBinding"))
            {
                var appliesTo = binding.Attribute("appliesTo")?.Value;
                if (!AppliesToMatchesV4(appliesTo)) continue;

                foreach (var child in binding.Elements())
                {
                    var localName = child.Name.LocalName;
                    if (string.Equals(localName, "probing", StringComparison.Ordinal))
                    {
                        var pp = child.Attribute("privatePath")?.Value;
                        if (!string.IsNullOrEmpty(pp))
                        {
                            foreach (var seg in pp.Split([';'], StringSplitOptions.RemoveEmptyEntries))
                                privatePaths.Add(seg.Trim());
                        }
                        continue;
                    }
                    if (string.Equals(localName, "dependentAssembly", StringComparison.Ordinal))
                    {
                        ParseDependentAssembly(child, source, globalPublisherPolicyDisabled,
                            redirects, codeBases, disabled);
                    }
                }
            }
        }
        return anyGlobalDisable;
    }

    private static void ParseDependentAssembly(
        XElement dependent,
        PolicyLayer source,
        bool globalPublisherPolicyDisabled,
        List<BindingRedirect> redirects,
        List<CodeBaseEntry> codeBases,
        List<(string, string?, string)> disabled)
    {
        var identity = dependent.Elements().FirstOrDefault(e => e.Name.LocalName == "assemblyIdentity");
        if (identity is null) return;

        var name = identity.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name)) return;

        var pkt = identity.Attribute("publicKeyToken")?.Value?.ToLowerInvariant();
        var culture = identity.Attribute("culture")?.Value;
        if (string.IsNullOrEmpty(culture)) culture = "neutral";
        var procArch = identity.Attribute("processorArchitecture")?.Value;

        // Per-dependentAssembly <publisherPolicy apply="no"/>.
        var localPublisherPolicyDisabled = false;
        foreach (var pp in dependent.Elements().Where(e => e.Name.LocalName == "publisherPolicy"))
        {
            if (string.Equals(pp.Attribute("apply")?.Value, "no", StringComparison.OrdinalIgnoreCase))
                localPublisherPolicyDisabled = true;
        }
        if (globalPublisherPolicyDisabled || localPublisherPolicyDisabled)
            disabled.Add((name, pkt, culture));

        foreach (var br in dependent.Elements().Where(e => e.Name.LocalName == "bindingRedirect"))
        {
            var oldRange = br.Attribute("oldVersion")?.Value;
            var newVer = br.Attribute("newVersion")?.Value;
            if (!TryParseVersionRange(oldRange, out var oldMin, out var oldMax)) continue;
            if (!Version.TryParse(newVer, out var newVersion)) continue;
            redirects.Add(new BindingRedirect(
                source, name, pkt, culture, procArch, oldMin, oldMax, newVersion));
        }

        foreach (var cb in dependent.Elements().Where(e => e.Name.LocalName == "codeBase"))
        {
            var version = cb.Attribute("version")?.Value;
            var href = cb.Attribute("href")?.Value;
            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(href)) continue;
            if (!Version.TryParse(version, out var v)) continue;
            codeBases.Add(new CodeBaseEntry(source, name, pkt, culture, v, href));
        }
    }

    private static bool TryParseVersionRange(string? value, out Version min, out Version max)
    {
        min = max = new Version(0, 0, 0, 0);
        if (string.IsNullOrEmpty(value)) return false;
        var dash = value.IndexOf('-');
        if (dash < 0)
        {
            if (!Version.TryParse(value, out var single)) return false;
            min = max = single;
            return true;
        }
        if (!Version.TryParse(value[..dash], out var a)) return false;
        if (!Version.TryParse(value[(dash + 1)..], out var b)) return false;
        min = a;
        max = b;
        return true;
    }

    private static bool AppliesToMatchesV4(string? appliesTo)
    {
        if (string.IsNullOrEmpty(appliesTo)) return true;
        return appliesTo.StartsWith("v4.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appliesTo, "v4", StringComparison.OrdinalIgnoreCase);
    }

    private static string? MachineConfigPathFor(NetFxArchitecture architecture)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return null;
        var subdir = architecture == NetFxArchitecture.X86 ? "Framework" : "Framework64";
        var path = Path.Combine(windir!, "Microsoft.NET", subdir, "v4.0.30319", "Config", "machine.config");
        return File.Exists(path) ? path : null;
    }

    private static IEnumerable<(IReadOnlyList<BindingRedirect>, IReadOnlyList<CodeBaseEntry>)>
        EnumeratePublisherPolicies(IReadOnlyList<string> gacRoots, NetFxArchitecture architecture)
    {
        // Discover policy.<major>.<minor>.<simpleName> assemblies in the supplied GAC roots and
        // parse the .config siblings that carry their <bindingRedirect> payload. Architecture-
        // prioritized: an x86 process never loads policies from GAC_64 and an amd64 process
        // never loads them from GAC_32, so first-match semantics on the policy redirects can't
        // pick up an architecture-incompatible publisher policy.
        var archSubdir = architecture == NetFxArchitecture.Amd64 ? "GAC_64" : "GAC_32";
        var subdirs = new[] { "GAC_MSIL", archSubdir };
        foreach (var root in gacRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var subdir in subdirs)
            {
                var gacPath = Path.Combine(root, subdir);
                if (!Directory.Exists(gacPath)) continue;
                IEnumerable<string> policyFamilies;
                try
                {
                    policyFamilies = Directory.EnumerateDirectories(gacPath, "policy.*.*");
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }

                foreach (var family in policyFamilies)
                {
                    IEnumerable<string> versionDirs;
                    try { versionDirs = Directory.EnumerateDirectories(family); }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }

                    foreach (var versionDir in versionDirs)
                    {
                        IEnumerable<string> files;
                        try { files = Directory.EnumerateFiles(versionDir, "*.config"); }
                        catch (UnauthorizedAccessException) { continue; }
                        catch (IOException) { continue; }

                        foreach (var configFile in files)
                        {
                            var parsed = ParseConfigFile(configFile, PolicyLayer.PublisherPolicy);
                            if (parsed.Redirects.Count > 0 || parsed.CodeBases.Count > 0)
                                yield return (parsed.Redirects, parsed.CodeBases);
                        }
                    }
                }
            }
        }
    }
}
