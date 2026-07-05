using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Locates the pre-ILC build outputs of a Native AOT binary: the managed input assembly
/// the compiler consumed, its portable PDB, and the mstat/DGML sidecars in the build's
/// intermediate tree.
/// </summary>
/// <remarks>
/// An AOT publish leaves its inputs behind: the managed assembly and portable PDB in the
/// intermediate directory (<c>obj\&lt;cfg&gt;\&lt;tfm&gt;\&lt;rid&gt;</c>, or the artifacts-layout
/// equivalent), and an ILC response file (<c>*.ilc.rsp</c>) in <c>native\</c> whose first
/// non-switch token names the exact root input. The probe tries three origins in
/// authority order — response file, conventional intermediate location, sibling file —
/// validating each candidate (readable CLR metadata, identity, never the binary itself)
/// and falling through on failure. Recognition is positional segment mapping, so custom
/// configuration names, TFMs, RIDs, and artifacts pivots all work without being parsed.
/// The probe never throws; failures degrade to a smaller result or null.
/// </remarks>
public static class PreIlcSidecarDetector
{
    private static readonly string[] KnownExtensions = [".exe", ".dll", ".so", ".dylib"];
    private const int MaxResponseFileDepth = 4;
    private const int MaxDetailedReferences = 12;

    /// <summary>
    /// Probes for the pre-ILC sidecars of the Native AOT binary at
    /// <paramref name="binaryPath"/>.
    /// </summary>
    /// <param name="binaryPath">Path to the Native AOT executable or library.</param>
    /// <returns>
    /// The discovered sidecars — possibly without an attachable managed assembly when only
    /// mstat/DGML files were found — or <c>null</c> when nothing was found at all. The
    /// caller is responsible for having established that the binary is Native AOT.
    /// </returns>
    public static PreIlcSidecars? Find(string binaryPath)
    {
        try
        {
            return FindCore(binaryPath);
        }
        catch
        {
            return null;
        }
    }

    private static PreIlcSidecars? FindCore(string binaryPath)
    {
        var fullPath = Path.GetFullPath(binaryPath);
        var binaryDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(binaryDir)) return null;

        var stem = StripKnownExtension(Path.GetFileName(fullPath));
        var notes = new List<string>();
        var tree = RecognizeTree(binaryDir);

        string? managed = null;
        var origin = PreIlcAssemblyOrigin.None;
        string? rspPath = null;
        IReadOnlyList<string> rspReferences = [];

        // Origin 1: the ILC response file — names the exact files the compiler consumed.
        if (tree is not null)
        {
            var candidateRsp = Path.Combine(tree.NativeDir, stem + ".ilc.rsp");
            if (File.Exists(candidateRsp))
            {
                rspPath = candidateRsp;
                var parsed = ParseResponseFile(candidateRsp, tree.ProjectDir, notes);
                rspReferences = parsed.References;
                if (parsed.RootInput is { } root)
                {
                    if (PathsEqual(root, fullPath))
                    {
                        notes.Add("ilc.rsp root input is the binary itself; ignored");
                    }
                    else if (TryReadAssemblyIdentity(root, out var rspName))
                    {
                        managed = root;
                        origin = PreIlcAssemblyOrigin.IlcResponseFile;
                        if (!string.Equals(rspName, stem, StringComparison.OrdinalIgnoreCase))
                            notes.Add($"ilc.rsp root input assembly name '{rspName}' differs from binary stem '{stem}'");
                    }
                    else
                    {
                        notes.Add($"ilc.rsp root input not readable: {root}; fell back");
                    }
                }
                else if (parsed.UnresolvableRoot is { } raw)
                {
                    notes.Add($"ilc.rsp root input '{raw}' could not be resolved; fell back");
                }
            }
        }

        // Origin 2: the SDK's conventional intermediate location for the recognized tree.
        if (managed is null && tree is not null)
        {
            var candidate = Path.Combine(tree.ObjDir, stem + ".dll");
            if (File.Exists(candidate) && !PathsEqual(candidate, fullPath))
            {
                if (TryReadAssemblyIdentity(candidate, out var name)
                    && string.Equals(name, stem, StringComparison.OrdinalIgnoreCase))
                {
                    managed = candidate;
                    origin = PreIlcAssemblyOrigin.BuildTreeLayout;
                }
                else
                {
                    notes.Add($"intermediate candidate rejected (identity/metadata): {candidate}");
                }
            }
        }

        // Origin 3: a sibling next to the binary — manual staging. Structurally impossible
        // for a Windows native AOT library, whose managed input shares its exact filename.
        if (managed is null)
        {
            var candidate = Path.Combine(binaryDir, stem + ".dll");
            if (File.Exists(candidate) && !PathsEqual(candidate, fullPath))
            {
                if (TryReadAssemblyIdentity(candidate, out var name)
                    && string.Equals(name, stem, StringComparison.OrdinalIgnoreCase))
                {
                    managed = candidate;
                    origin = PreIlcAssemblyOrigin.SiblingAssembly;
                }
                else
                {
                    notes.Add($"sibling candidate rejected (identity/metadata): {candidate}");
                }
            }
        }

        var pdbStatus = PreIlcPdbStatus.NotApplicable;
        string? pdbPath = null;
        if (managed is not null)
        {
            pdbStatus = ProbePdb(managed, out pdbPath);
            if (File.GetLastWriteTimeUtc(managed) > File.GetLastWriteTimeUtc(fullPath))
                notes.Add("managed input is newer than the binary; it may be from a later build");
        }

        // mstat/DGML originals in the intermediate native directory (the copies ILC wrote).
        string? mstat = null, codegenDgml = null, scanDgml = null;
        if (tree is not null)
        {
            mstat = ExistingOrNull(Path.Combine(tree.NativeDir, stem + ".mstat"));
            codegenDgml = ExistingOrNull(Path.Combine(tree.NativeDir, stem + ".codegen.dgml.xml"));
            scanDgml = ExistingOrNull(Path.Combine(tree.NativeDir, stem + ".scan.dgml.xml"));
        }

        var local = new List<string>();
        var unresolved = new List<string>();
        var packageCount = 0;
        var otherCount = 0;
        if (rspReferences.Count > 0)
        {
            CategorizeReferences(
                rspReferences, managed, tree?.ProjectDir,
                local, unresolved, ref packageCount, ref otherCount, notes);
        }

        if (managed is null && mstat is null && codegenDgml is null && scanDgml is null)
            return null;

        return new PreIlcSidecars(
            managed, origin, pdbPath, pdbStatus,
            mstat, codegenDgml, scanDgml, rspPath,
            local, packageCount, otherCount, unresolved,
            notes.Count > 0 ? string.Join("; ", notes) : null);
    }

    /// <summary>
    /// Strips one known binary extension (<c>.exe</c>, <c>.dll</c>, <c>.so</c>,
    /// <c>.dylib</c>) from a filename; extensionless names pass through unchanged.
    /// Dotted assembly names survive because only the known suffix is removed.
    /// </summary>
    internal static string StripKnownExtension(string fileName)
    {
        foreach (var ext in KnownExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return fileName[..^ext.Length];
        }

        return fileName;
    }

    /// <summary>The recognized build tree around a binary: where its intermediates live.</summary>
    /// <param name="ProjectDir">The project directory when derivable (classic layout), used to resolve relative response-file paths; null for the artifacts layout.</param>
    /// <param name="ObjDir">The intermediate directory expected to hold the managed input.</param>
    /// <param name="NativeDir">The ILC output directory expected to hold the response file and mstat/DGML originals.</param>
    private sealed record TreeLayout(string? ProjectDir, string ObjDir, string NativeDir);

    private static TreeLayout? RecognizeTree(string binaryDir)
    {
        var segments = Path.GetFullPath(binaryDir)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var n = segments.Length;

        // Classic layouts, most specific first. Positional: <cfg>/<tfm>/<rid> are wildcards.
        foreach (var anchor in ClassicShapes(segments))
        {
            var projectDir = JoinSegments(binaryDir, segments, anchor);
            var objDir = Path.Combine(
                projectDir, "obj", segments[anchor + 1], segments[anchor + 2], segments[anchor + 3]);
            var layout = new TreeLayout(projectDir, objDir, Path.Combine(objDir, "native"));
            if (Directory.Exists(layout.ObjDir)) return layout;
        }

        // Artifacts layout (UseArtifactsOutput): <root>\publish|bin\<proj>\<pivot> maps to
        // <root>\obj\<proj>\<pivot> by pure segment substitution — pivots are never parsed.
        if (n >= 3 && (SegEquals(segments[n - 3], "publish") || SegEquals(segments[n - 3], "bin")))
        {
            var root = JoinSegments(binaryDir, segments, n - 3);
            var objDir = Path.Combine(root, "obj", segments[n - 2], segments[n - 1]);
            if (Directory.Exists(objDir))
                return new TreeLayout(null, objDir, Path.Combine(objDir, "native"));
        }

        return null;
    }

    /// <summary>
    /// Yields the index of the <c>bin</c>/<c>obj</c> anchor segment for each classic shape
    /// the directory matches, most specific first.
    /// </summary>
    private static IEnumerable<int> ClassicShapes(string[] segments)
    {
        var n = segments.Length;

        // bin\<cfg>\<tfm>\<rid>\publish  or  bin\<cfg>\<tfm>\<rid>\native
        if (n >= 5 && SegEquals(segments[n - 5], "bin")
            && (SegEquals(segments[n - 1], "publish") || SegEquals(segments[n - 1], "native")))
        {
            yield return n - 5;
        }

        // obj\<cfg>\<tfm>\<rid>\native — the binary run straight from the ILC output dir.
        if (n >= 5 && SegEquals(segments[n - 5], "obj") && SegEquals(segments[n - 1], "native"))
            yield return n - 5;

        // bin\<cfg>\<tfm>\<rid>
        if (n >= 4 && SegEquals(segments[n - 4], "bin"))
            yield return n - 4;
    }

    private static bool SegEquals(string segment, string name) =>
        string.Equals(segment, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Rebuilds the absolute path of the first <paramref name="count"/> segments, preserving the original root.</summary>
    private static string JoinSegments(string originalDir, string[] segments, int count)
    {
        var full = Path.GetFullPath(originalDir);
        var root = Path.GetPathRoot(full) ?? string.Empty;

        var covered = root.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries).Length;

        var result = root;
        for (var i = covered; i < count; i++)
            result = Path.Combine(result, segments[i]);
        return result;
    }

    /// <summary>The parse result of an ILC response file.</summary>
    /// <param name="RootInput">The resolved root input path, or null.</param>
    /// <param name="UnresolvableRoot">The raw root token when it was relative and no base directory was known.</param>
    /// <param name="References">Resolved (or raw, when unresolvable) reference paths from <c>-r:</c>/<c>--reference</c> switches.</param>
    private sealed record RspContent(string? RootInput, string? UnresolvableRoot, IReadOnlyList<string> References);

    private static RspContent ParseResponseFile(string rspPath, string? projectDir, List<string> notes)
    {
        var tokens = new List<string>();
        ExpandResponseFile(rspPath, tokens, [], 0, notes);

        string? rootRaw = null;
        var refs = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = Unquote(tokens[i]);
            if (token.Length == 0) continue;

            if (token is "-r" or "--reference")
            {
                if (i + 1 < tokens.Count) refs.Add(Unquote(tokens[++i]));
                continue;
            }

            if (token.StartsWith("-r:", StringComparison.Ordinal))
            {
                refs.Add(Unquote(token[3..]));
                continue;
            }

            if (token.StartsWith("--reference:", StringComparison.Ordinal))
            {
                refs.Add(Unquote(token["--reference:".Length..]));
                continue;
            }

            if (token[0] == '-') continue; // other switch; inline values ride along

            rootRaw ??= token; // first non-switch token is the root input
        }

        string? root = null, unresolvableRoot = null;
        if (rootRaw is not null)
        {
            root = ResolvePath(rootRaw, projectDir);
            if (root is null) unresolvableRoot = rootRaw;
        }

        var resolvedRefs = new List<string>(refs.Count);
        foreach (var r in refs)
            resolvedRefs.Add(ResolvePath(r, projectDir) ?? r);

        return new RspContent(root, unresolvableRoot, resolvedRefs);
    }

    private static void ExpandResponseFile(
        string path, List<string> tokens, HashSet<string> visited, int depth, List<string> notes)
    {
        if (depth > MaxResponseFileDepth)
        {
            notes.Add($"response file nesting exceeds {MaxResponseFileDepth}: {path}");
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (!visited.Add(NormalizeForComparison(fullPath)))
        {
            notes.Add($"response file inclusion cycle: {path}");
            return;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notes.Add($"response file unreadable: {path}");
            return;
        }

        var baseDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        foreach (var line in lines)
        {
            var token = line.Trim();
            if (token.Length == 0) continue;

            if (token[0] == '@')
            {
                var include = Unquote(token[1..]);
                if (include.Length == 0) continue;
                if (!Path.IsPathRooted(include)) include = Path.Combine(baseDir, include);
                ExpandResponseFile(include, tokens, visited, depth + 1, notes);
                continue;
            }

            tokens.Add(token);
        }
    }

    private static string Unquote(string token)
    {
        var t = token.Trim();
        return t.Length >= 2 && t[0] == '"' && t[^1] == '"' ? t[1..^1] : t;
    }

    /// <summary>
    /// Resolves a response-file path: rooted paths pass through, relative paths resolve
    /// against the project directory when one is known. Returns null when a relative path
    /// has no base to resolve against.
    /// </summary>
    private static string? ResolvePath(string path, string? projectDir)
    {
        try
        {
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return projectDir is null ? null : Path.GetFullPath(Path.Combine(projectDir, path));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadAssemblyIdentity(string path, out string? simpleName)
    {
        simpleName = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) return false;

            var metadata = peReader.GetMetadataReader();
            simpleName = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PreIlcPdbStatus ProbePdb(string dllPath, out string? pdbPath)
    {
        pdbPath = null;
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var entries = peReader.ReadDebugDirectory();
            var hasEmbedded = entries.Any(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
            var codeView = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.CodeView);
            var hasPortableCodeView =
                codeView.Type == DebugDirectoryEntryType.CodeView && codeView.IsPortableCodeView;

            // The BCL validates the sidecar's PDB ID against the CodeView record (a sidecar
            // that opens is a matched sidecar) and falls back to an embedded PDB itself.
            if (peReader.TryOpenAssociatedPortablePdb(
                    dllPath, p => File.Exists(p) ? File.OpenRead(p) : null,
                    out var provider, out var openedPath))
            {
                provider?.Dispose();
                if (openedPath is not null)
                {
                    pdbPath = openedPath;
                    return PreIlcPdbStatus.Matched;
                }

                return PreIlcPdbStatus.Embedded;
            }

            // Nothing opened. A sibling pdb that exists but was rejected belongs to a
            // different build; keep its path for diagnostics.
            if (hasPortableCodeView)
            {
                var dllName = Path.GetFileName(dllPath);
                var dllStem = dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? dllName[..^4]
                    : dllName;
                var candidate = Path.Combine(Path.GetDirectoryName(dllPath)!, dllStem + ".pdb");
                if (File.Exists(candidate))
                {
                    pdbPath = candidate;
                    return PreIlcPdbStatus.Mismatched;
                }
            }

            return hasEmbedded ? PreIlcPdbStatus.Embedded : PreIlcPdbStatus.Missing;
        }
        catch
        {
            return pdbPath is not null ? PreIlcPdbStatus.Mismatched : PreIlcPdbStatus.Missing;
        }
    }

    private static void CategorizeReferences(
        IReadOnlyList<string> references, string? rootManagedPath, string? projectDir,
        List<string> local, List<string> unresolved,
        ref int packageCount, ref int otherCount, List<string> notes)
    {
        var packageRoots = DiscoverPackageRoots(references);
        var otherListed = 0;

        foreach (var reference in references)
        {
            if (rootManagedPath is not null && PathsEqual(reference, rootManagedPath))
                continue;

            if (!File.Exists(reference))
            {
                unresolved.Add(reference);
                continue;
            }

            if (IsPackagePath(reference, packageRoots))
            {
                packageCount++;
                continue;
            }

            var hasLocalEvidence =
                (projectDir is not null && IsUnder(reference, projectDir))
                || IsBuildOutputShaped(reference);
            if (hasLocalEvidence && TryReadAssemblyIdentity(reference, out _))
            {
                local.Add(reference);
                continue;
            }

            // Exists but carries no positive local evidence (or failed validation) — never local.
            otherCount++;
            if (otherListed < MaxDetailedReferences)
            {
                notes.Add($"unclassified reference: {reference}");
                otherListed++;
            }
            else if (otherListed == MaxDetailedReferences)
            {
                notes.Add("further unclassified references elided");
                otherListed++;
            }
        }
    }

    /// <summary>
    /// Discovers package-store roots: the prefix above any <c>microsoft.netcore.app.runtime.*</c>
    /// package-id segment in the references themselves (robust to custom <c>NUGET_PACKAGES</c>),
    /// the <c>NUGET_PACKAGES</c> environment variable, and the default per-user cache.
    /// </summary>
    private static List<string> DiscoverPackageRoots(IReadOnlyList<string> references)
    {
        var roots = new List<string>();

        foreach (var reference in references)
        {
            var segments = reference.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!segments[i].StartsWith("microsoft.netcore.app.runtime.", StringComparison.OrdinalIgnoreCase))
                    continue;

                var root = JoinSegments(reference, segments, i);
                if (!roots.Any(r => PathsEqual(r, root))) roots.Add(root);
                break;
            }
        }

        var env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(env))
        {
            try { roots.Add(Path.GetFullPath(env)); }
            catch { /* malformed env value — ignore */ }
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            roots.Add(Path.Combine(profile, ".nuget", "packages"));

        return roots;
    }

    private static bool IsPackagePath(string path, List<string> packageRoots)
    {
        foreach (var root in packageRoots)
        {
            if (IsUnder(path, root)) return true;
        }

        // SDK workload/reference packs: ...\dotnet\packs\<pack-id>\...
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < segments.Length; i++)
        {
            if (SegEquals(segments[i], "packs") && segments[i - 1].Contains("dotnet", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a path looks like some project's build output: a <c>bin</c> or <c>obj</c>
    /// segment with at least two directories between it and the file (classic
    /// <c>bin\cfg\tfm[\rid]</c> or artifacts <c>bin\proj\pivot</c>).
    /// </summary>
    private static bool IsBuildOutputShaped(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 3; i++)
        {
            if (SegEquals(segments[i], "bin") || SegEquals(segments[i], "obj"))
                return true;
        }

        return false;
    }

    private static bool IsUnder(string path, string root)
    {
        var full = NormalizeForComparison(Path.GetFullPath(path));
        var rootFull = NormalizeForComparison(Path.GetFullPath(root)).TrimEnd(Path.DirectorySeparatorChar);
        return full.Length > rootFull.Length
            && full.StartsWith(rootFull, PathComparison)
            && full[rootFull.Length] == Path.DirectorySeparatorChar;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            NormalizeForComparison(Path.GetFullPath(a)),
            NormalizeForComparison(Path.GetFullPath(b)),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizeForComparison(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static string? ExistingOrNull(string path) => File.Exists(path) ? path : null;
}
