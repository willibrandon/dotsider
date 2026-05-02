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
    /// <param name="runtimeVersion">
    /// CLR generation the policy targets. Switches the machine.config path, GAC token format,
    /// reference-assemblies tree, and <c>appliesTo</c> filter between
    /// <see cref="NetFxRuntimeVersion.Clr2"/> and <see cref="NetFxRuntimeVersion.Clr4"/>.
    /// </param>
    /// <returns>A populated <see cref="BindingPolicy"/>.</returns>
    public static BindingPolicy LoadFrom(
        string? appConfigPath,
        NetFxArchitecture architecture,
        IReadOnlyList<string> gacRoots,
        NetFxRuntimeVersion runtimeVersion = NetFxRuntimeVersion.Clr4)
    {
        var app = ParseConfigFile(appConfigPath, PolicyLayer.AppConfig, runtimeVersion);

        var machineConfigPath = MachineConfigPathFor(architecture, runtimeVersion);
        var machine = ParseConfigFile(machineConfigPath, PolicyLayer.MachineConfig, runtimeVersion);

        var publisherRedirects = new List<BindingRedirect>();
        var publisherCodeBases = new List<CodeBaseEntry>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var (rs, cs) in EnumeratePublisherPolicies(gacRoots, architecture, runtimeVersion))
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
            FrameworkUnificationTable: BuildFrameworkUnificationTable(architecture, gacRoots, runtimeVersion));
    }

    /// <summary>
    /// Parses a single configuration file (app config, machine.config, or a publisher-policy
    /// assembly's embedded XML resource) into a <see cref="BindingPolicyParseResult"/>.
    /// Exposed so callers that already have the file path can avoid re-parsing.
    /// </summary>
    /// <param name="path">Path to the configuration file, or <see langword="null"/>.</param>
    /// <param name="source">Policy layer to attribute parsed entries to.</param>
    /// <param name="runtimeVersion">
    /// CLR generation the parse targets. Filters <c>&lt;assemblyBinding appliesTo="..."&gt;</c>
    /// blocks: <see cref="NetFxRuntimeVersion.Clr2"/> accepts <c>v2</c>/<c>v2.0</c>/<c>v2.0.50727</c>;
    /// <see cref="NetFxRuntimeVersion.Clr4"/> accepts <c>v4</c>/<c>v4.*</c>; an empty
    /// <c>appliesTo</c> matches both.
    /// </param>
    /// <returns>The parsed result; an empty result on missing file or malformed XML.</returns>
    public static BindingPolicyParseResult ParseConfigFile(
        string? path,
        PolicyLayer source,
        NetFxRuntimeVersion runtimeVersion = NetFxRuntimeVersion.Clr4)
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

        globallyDisabled = ParseRuntimeElement(
            doc.Root, source, runtimeVersion, redirects, codeBases, disabled, privatePaths);
        return new BindingPolicyParseResult(redirects, codeBases, disabled, privatePaths, globallyDisabled);
    }

    private bool IsPublisherPolicyDisabled(AssemblyRefInfo requested)
    {
        // Runtime-scoped <publisherPolicy apply="no"/> suppresses publisher policy for every
        // bind in the AppDomain, including identities with no <dependentAssembly> block.
        if (PublisherPolicyDisabledGlobally) return true;

        foreach (var (Name, PublicKeyToken, Culture) in PublisherPolicyDisabledFor)
        {
            if (string.Equals(Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
                PktEquals(PublicKeyToken, requested.PublicKeyToken) &&
                CultureEquals(Culture, requested.Culture))
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
    /// later GAC scan then locates the file at its real GAC slot instead of falling back to the
    /// framework dir.
    /// </summary>
    private static Dictionary<(string Name, string PublicKeyToken), Version> BuildFrameworkUnificationTable(
        NetFxArchitecture architecture,
        IReadOnlyList<string> gacRoots,
        NetFxRuntimeVersion runtimeVersion)
    {
        var table = new Dictionary<(string, string), Version>(
            new FrameworkUnificationKeyComparer());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return table;

        AddFrameworkRuntimeDirEntries(architecture, runtimeVersion, table);
        // GAC scan picks up framework-PKT assemblies that aren't in the framework runtime dir —
        // notably the WPF set (System.Printing, PresentationCore, PresentationFramework, …)
        // which lives only in the GAC. Live net48 unifies these the same as in-box assemblies:
        // System.Printing v3.0.0.0 → v4.0.0.0 from GAC_64\System.Printing\v4.0_4.0.0.0__….
        // Gate the scan on the canonical framework-assembly name set (the Reference Assemblies
        // tree) so non-framework Microsoft-signed assemblies in the GAC — VS, SQL Server
        // tooling, etc. — don't accidentally end up in the unification table.
        var frameworkNames = LoadFrameworkAssemblyNames(runtimeVersion);
        AddGacEntries(gacRoots, architecture, runtimeVersion, frameworkNames, table);
        return table;
    }

    /// <summary>
    /// Returns the set of assembly simple names that ship as part of .NET Framework, derived
    /// from the Reference Assemblies tree. Walking those trees gives the canonical list of names
    /// the CLR's framework unification table covers; anything outside it is third-party even
    /// when signed with a Microsoft framework public key token.
    /// </summary>
    /// <remarks>
    /// Roots scanned:
    /// <list type="bullet">
    ///   <item><see cref="NetFxRuntimeVersion.Clr4"/>:
    ///     <c>...\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.*</c>
    ///     plus each <c>Facades\</c> subdir.
    ///   </item>
    ///   <item><see cref="NetFxRuntimeVersion.Clr2"/>:
    ///     <c>...\Reference Assemblies\Microsoft\Framework\v3.5</c> (top-level + <c>Profile\Client\</c>),
    ///     <c>...\Reference Assemblies\Microsoft\Framework\v3.0</c> (covers WindowsBase /
    ///     PresentationCore / PresentationFramework / System.ServiceModel / System.Workflow.*),
    ///     and <c>...\Reference Assemblies\Microsoft\Framework\.NETFramework\v3.5</c>. The
    ///     HashSet collapses duplicates.
    ///   </item>
    /// </list>
    /// </remarks>
    private static HashSet<string> LoadFrameworkAssemblyNames(NetFxRuntimeVersion runtimeVersion)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("ProgramFiles"),
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var frameworkRoot = Path.Combine(root!, "Reference Assemblies", "Microsoft", "Framework");
            if (!Directory.Exists(frameworkRoot)) continue;

            if (runtimeVersion == NetFxRuntimeVersion.Clr4)
            {
                var refRoot = Path.Combine(frameworkRoot, ".NETFramework");
                if (!Directory.Exists(refRoot)) continue;
                IEnumerable<string> versionDirs;
                try { versionDirs = Directory.EnumerateDirectories(refRoot, "v4.*"); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                foreach (var versionDir in versionDirs)
                {
                    AddDllNamesFrom(versionDir, names);
                    var facades = Path.Combine(versionDir, "Facades");
                    if (Directory.Exists(facades)) AddDllNamesFrom(facades, names);
                }
            }
            else
            {
                // Three legacy locations for the Clr2 surface: v3.5 (with optional Client
                // profile), v3.0, and the .NETFramework\v3.5 mirror added when 4.0 shipped.
                var v35 = Path.Combine(frameworkRoot, "v3.5");
                if (Directory.Exists(v35))
                {
                    AddDllNamesFrom(v35, names);
                    var clientProfile = Path.Combine(v35, "Profile", "Client");
                    if (Directory.Exists(clientProfile)) AddDllNamesFrom(clientProfile, names);
                }
                var v30 = Path.Combine(frameworkRoot, "v3.0");
                if (Directory.Exists(v30)) AddDllNamesFrom(v30, names);
                var netFxV35 = Path.Combine(frameworkRoot, ".NETFramework", "v3.5");
                if (Directory.Exists(netFxV35)) AddDllNamesFrom(netFxV35, names);
            }
        }
        return names;
    }

    private static void AddDllNamesFrom(string dir, HashSet<string> names)
    {
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(dir, "*.dll"); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        foreach (var file in files)
            names.Add(Path.GetFileNameWithoutExtension(file));
    }

    private static void AddFrameworkRuntimeDirEntries(
        NetFxArchitecture architecture,
        NetFxRuntimeVersion runtimeVersion,
        Dictionary<(string Name, string PublicKeyToken), Version> table)
    {
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return;
        var subdir = architecture == NetFxArchitecture.X86 ? "Framework" : "Framework64";
        var runtimeDir = runtimeVersion == NetFxRuntimeVersion.Clr2 ? "v2.0.50727" : "v4.0.30319";
        var dir = Path.Combine(windir!, "Microsoft.NET", subdir, runtimeDir);
        if (!Directory.Exists(dir)) return;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(dir, "*.dll"); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        foreach (var file in files)
        {
            var identity = TryReadAssemblyIdentity(file);
            if (identity is null) continue;
            if (string.IsNullOrEmpty(identity.Value.PublicKeyToken)) continue;
            if (!AssemblyAnalyzer.FrameworkUnificationPublicKeyTokens.Contains(identity.Value.PublicKeyToken!))
                continue;
            if (!Version.TryParse(identity.Value.Version, out var v)) continue;
            var key = (identity.Value.Name, identity.Value.PublicKeyToken!);
            if (!table.TryGetValue(key, out var existing) || v > existing)
                table[key] = v;
        }
    }

    private static void AddGacEntries(
        IReadOnlyList<string> gacRoots,
        NetFxArchitecture architecture,
        NetFxRuntimeVersion runtimeVersion,
        HashSet<string> frameworkNames,
        Dictionary<(string Name, string PublicKeyToken), Version> table)
    {
        // Walk only the architecture-compatible GAC buckets — GAC_MSIL plus the matching
        // bitness slot — so a higher version installed for the wrong architecture doesn't end
        // up in the unification table. The locate stage uses the same scan list, so anything
        // we record here is reachable later. Clr2 also includes the bare "GAC" bucket
        // (CLR 1.x carryover, still consulted by CLR2 fusion).
        var archSubdir = architecture == NetFxArchitecture.Amd64 ? "GAC_64" : "GAC_32";
        var subdirs = runtimeVersion == NetFxRuntimeVersion.Clr2
            ? new[] { "GAC_MSIL", archSubdir, "GAC" }
            : new[] { "GAC_MSIL", archSubdir };

        foreach (var root in gacRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var gacSubdir in subdirs)
            {
                var gacPath = Path.Combine(root, gacSubdir);
                if (!Directory.Exists(gacPath)) continue;
                IEnumerable<string> nameDirs;
                try { nameDirs = Directory.EnumerateDirectories(gacPath); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }

                foreach (var nameDir in nameDirs)
                {
                    var simpleName = Path.GetFileName(nameDir);
                    if (string.IsNullOrEmpty(simpleName)) continue;
                    // Only assemblies that ship as part of .NET Framework — name must appear
                    // in the Reference Assemblies tree. Drops VS, SQL Server, Office, and any
                    // other Microsoft-signed assemblies that just happen to live in the GAC.
                    if (!frameworkNames.Contains(simpleName)) continue;

                    IEnumerable<string> tokenDirs;
                    try { tokenDirs = Directory.EnumerateDirectories(nameDir); }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }

                    foreach (var tokenDir in tokenDirs)
                    {
                        var token = Path.GetFileName(tokenDir);
                        if (!TryParseGacToken(token, runtimeVersion, out var version, out var pkt)) continue;
                        if (!AssemblyAnalyzer.FrameworkUnificationPublicKeyTokens.Contains(pkt!)) continue;
                        var key = (simpleName, pkt!);
                        if (!table.TryGetValue(key, out var existing) || version > existing)
                            table[key] = version;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Parses a GAC token directory name into version + PKT. The token format differs by CLR:
    /// <list type="bullet">
    ///   <item><see cref="NetFxRuntimeVersion.Clr4"/>: <c>v4.0_4.0.0.0__31bf3856ad364e35</c>
    ///     (or culture-specific <c>v4.0_4.0.0.0_en-US_31bf3856ad364e35</c>).</item>
    ///   <item><see cref="NetFxRuntimeVersion.Clr2"/>: <c>2.0.0.0__b77a5c561934e089</c> — no
    ///     <c>v4.0_</c> prefix.</item>
    /// </list>
    /// Returns <see langword="false"/> for non-neutral cultures (satellites aren't unified) or
    /// any token that doesn't match the expected layout.
    /// </summary>
    private static bool TryParseGacToken(
        string token,
        NetFxRuntimeVersion runtimeVersion,
        out Version version,
        out string? publicKeyToken)
    {
        version = new Version(0, 0, 0, 0);
        publicKeyToken = null;

        string rest;
        if (runtimeVersion == NetFxRuntimeVersion.Clr4)
        {
            if (!token.StartsWith("v4.0_", StringComparison.OrdinalIgnoreCase)) return false;
            rest = token.Substring(5);
        }
        else
        {
            // Clr2 tokens are not prefixed; reject any v4.0_ leftover so a mixed-cache directory
            // never feeds Clr4-shaped tokens into the Clr2 unification table.
            if (token.StartsWith("v4.0_", StringComparison.OrdinalIgnoreCase)) return false;
            rest = token;
        }

        var dunder = rest.IndexOf("__", StringComparison.Ordinal);
        if (dunder < 0) return false;
        var versionAndCulture = rest[..dunder];
        var pkt = rest[(dunder + 2)..];
        // Skip culture-specific tokens — they look like "<version>_<culture>" before the "__pkt".
        if (versionAndCulture.Contains('_')) return false;
        if (!Version.TryParse(versionAndCulture, out var v)) return false;
        if (pkt.Length != 16) return false;
        version = v;
        publicKeyToken = pkt.ToLowerInvariant();
        return true;
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
        NetFxRuntimeVersion runtimeVersion,
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
                if (!AppliesToMatches(appliesTo, runtimeVersion)) continue;

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

    private static bool AppliesToMatches(string? appliesTo, NetFxRuntimeVersion runtimeVersion)
    {
        // Empty/missing appliesTo applies to every CLR (CLR semantics: an unscoped
        // <assemblyBinding> matches any runtime).
        if (string.IsNullOrEmpty(appliesTo)) return true;
        return runtimeVersion switch
        {
            NetFxRuntimeVersion.Clr2 =>
                appliesTo.StartsWith("v2.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(appliesTo, "v2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(appliesTo, "v2.0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(appliesTo, "v2.0.50727", StringComparison.OrdinalIgnoreCase),
            NetFxRuntimeVersion.Clr4 =>
                appliesTo.StartsWith("v4.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(appliesTo, "v4", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static string? MachineConfigPathFor(NetFxArchitecture architecture, NetFxRuntimeVersion runtimeVersion)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return null;
        var subdir = architecture == NetFxArchitecture.X86 ? "Framework" : "Framework64";
        var runtimeDir = runtimeVersion == NetFxRuntimeVersion.Clr2 ? "v2.0.50727" : "v4.0.30319";
        var path = Path.Combine(windir!, "Microsoft.NET", subdir, runtimeDir, "Config", "machine.config");
        return File.Exists(path) ? path : null;
    }

    private static IEnumerable<(IReadOnlyList<BindingRedirect>, IReadOnlyList<CodeBaseEntry>)>
        EnumeratePublisherPolicies(
            IReadOnlyList<string> gacRoots,
            NetFxArchitecture architecture,
            NetFxRuntimeVersion runtimeVersion)
    {
        // Discover policy.<major>.<minor>.<simpleName> assemblies in the supplied GAC roots and
        // parse the .config siblings that carry their <bindingRedirect> payload. Architecture-
        // prioritized: an x86 process never loads policies from GAC_64 and an amd64 process
        // never loads them from GAC_32, so first-match semantics on the policy redirects can't
        // pick up an architecture-incompatible publisher policy.
        // The CLR2 GAC also has a bare "GAC" bucket (CLR 1.x carryover); CLR2 fusion still
        // consults it, so include it in the publisher-policy scan for Clr2 contexts.
        var archSubdir = architecture == NetFxArchitecture.Amd64 ? "GAC_64" : "GAC_32";
        var subdirs = runtimeVersion == NetFxRuntimeVersion.Clr2
            ? new[] { "GAC_MSIL", archSubdir, "GAC" }
            : new[] { "GAC_MSIL", archSubdir };
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
                            var parsed = ParseConfigFile(configFile, PolicyLayer.PublisherPolicy, runtimeVersion);
                            if (parsed.Redirects.Count > 0 || parsed.CodeBases.Count > 0)
                                yield return (parsed.Redirects, parsed.CodeBases);
                        }
                    }
                }
            }
        }
    }
}
