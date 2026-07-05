namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Resolves a composite ReadyToRun component (or a component's owner composite) to a sibling file on
/// disk, matched by module version id — the authoritative identity — with the manifest name only as
/// a hint. Opened analyzers are owned by the caller (the root image) and disposed with it. A missing
/// or mismatched sibling resolves to null, which the report surfaces as an honest availability state
/// rather than collapsing to "IL only".
/// </summary>
internal static class ReadyToRunComponentResolver
{
    /// <summary>Opens the sibling assembly in <paramref name="directory"/> whose MVID matches.</summary>
    /// <param name="directory">The directory to search (the root image's directory).</param>
    /// <param name="nameHint">The component's manifest name, tried first.</param>
    /// <param name="mvid">The component's module version id; the exact match required by a scan.</param>
    /// <returns>An owned analyzer, or null when no sibling matches.</returns>
    public static AssemblyAnalyzer? Resolve(string directory, string? nameHint, Guid mvid)
    {
        // The name hint is the common case (the component file name matches). Accept it even when the
        // manifest recorded no MVID, since the name is then the only identity available.
        if (!string.IsNullOrEmpty(nameHint))
        {
            foreach (var ext in new[] { ".dll", ".exe" })
            {
                var opened = TryOpenMatching(Path.Combine(directory, nameHint + ext), mvid, allowEmptyMvid: true);
                if (opened is not null) return opened;
            }
        }

        // Otherwise scan siblings for the exact MVID; a name mismatch must not pick an arbitrary file.
        if (mvid != Guid.Empty && Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll"))
            {
                var opened = TryOpenMatching(file, mvid, allowEmptyMvid: false);
                if (opened is not null) return opened;
            }
        }

        return null;
    }

    /// <summary>Opens a specific owner composite file by name (component → owner direction).</summary>
    /// <param name="directory">The component's directory.</param>
    /// <param name="ownerFileName">The <c>OwnerCompositeExecutable</c> file name.</param>
    /// <returns>An owned analyzer for the composite, or null when it is not on disk.</returns>
    public static AssemblyAnalyzer? ResolveOwner(string directory, string ownerFileName)
    {
        var path = Path.Combine(directory, ownerFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return new AssemblyAnalyzer(path);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static AssemblyAnalyzer? TryOpenMatching(string path, Guid mvid, bool allowEmptyMvid)
    {
        if (!File.Exists(path)) return null;
        AssemblyAnalyzer? analyzer = null;
        try
        {
            analyzer = new AssemblyAnalyzer(path);
            var reader = analyzer.GetMetadataReader();
            if (reader is not null)
            {
                var candidate = reader.GetGuid(reader.GetModuleDefinition().Mvid);
                if (candidate == mvid || (allowEmptyMvid && mvid == Guid.Empty))
                {
                    var resolved = analyzer;
                    analyzer = null;
                    return resolved;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            // Not a readable managed assembly; treat as no match.
        }
        finally
        {
            analyzer?.Dispose();
        }

        return null;
    }
}
