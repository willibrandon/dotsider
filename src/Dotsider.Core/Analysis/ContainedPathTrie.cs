using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Tracks canonical extraction destinations and identifies file-versus-directory conflicts in
/// work proportional to the total number of path segments.
/// </summary>
/// <param name="comparer">The platform filesystem segment comparer.</param>
internal sealed class ContainedPathTrie(StringComparer comparer)
{
    private readonly ContainedPathTrieNode _root = new(comparer);

    /// <summary>
    /// Adds a canonical destination and marks it and every conflicting entry as unsafe.
    /// </summary>
    /// <param name="path">The canonical platform comparison key.</param>
    /// <param name="entry">The package entry that owns the destination.</param>
    /// <param name="unsafeEntries">The set that receives conflicting entries.</param>
    internal void Add(
        string path,
        NuGetFileEntry entry,
        HashSet<NuGetFileEntry> unsafeEntries)
    {
        var node = _root;
        foreach (var segment in path.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            MarkConflicts(node.TerminalEntries, entry, unsafeEntries);
            node.SubtreeEntries.Add(entry);
            node = node.GetOrAddChild(segment);
        }

        MarkConflicts(node.SubtreeEntries, entry, unsafeEntries);
        node.SubtreeEntries.Add(entry);
        node.TerminalEntries.Add(entry);
    }

    private static void MarkConflicts(
        List<NuGetFileEntry> conflictingEntries,
        NuGetFileEntry entry,
        HashSet<NuGetFileEntry> unsafeEntries)
    {
        if (conflictingEntries.Count == 0)
            return;

        unsafeEntries.Add(entry);
        foreach (var conflictingEntry in conflictingEntries)
            unsafeEntries.Add(conflictingEntry);
    }
}
