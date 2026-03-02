using Dotsider.Analysis.Models;

namespace Dotsider.Analysis;

/// <summary>
/// Compares two assemblies and produces a detailed diff result.
/// Uses dictionary-based O(n) matching by name.
/// </summary>
public static class AssemblyDiffer
{
    public static AssemblyDiffResult Compare(AssemblyAnalyzer left, AssemblyAnalyzer right)
    {
        var typeDiffs = CompareTypes(left.TypeDefs, right.TypeDefs);
        var methodDiffs = CompareMethods(left.MethodDefs, right.MethodDefs);
        var refDiffs = CompareRefs(left.AssemblyRefs, right.AssemblyRefs);

        var summary = new DiffSummary(
            TypesAdded: typeDiffs.Count(d => d.Kind == DiffKind.Added),
            TypesRemoved: typeDiffs.Count(d => d.Kind == DiffKind.Removed),
            TypesChanged: typeDiffs.Count(d => d.Kind == DiffKind.Changed),
            MethodsAdded: methodDiffs.Count(d => d.Kind == DiffKind.Added),
            MethodsRemoved: methodDiffs.Count(d => d.Kind == DiffKind.Removed),
            MethodsChanged: methodDiffs.Count(d => d.Kind == DiffKind.Changed),
            RefsAdded: refDiffs.Count(d => d.Kind == DiffKind.Added),
            RefsRemoved: refDiffs.Count(d => d.Kind == DiffKind.Removed),
            RefsChanged: refDiffs.Count(d => d.Kind == DiffKind.Changed),
            SizeDelta: right.FileSize - left.FileSize);

        return new AssemblyDiffResult(typeDiffs, methodDiffs, refDiffs, summary);
    }

    private static IReadOnlyList<DiffEntry<TypeDefInfo>> CompareTypes(
        IReadOnlyList<TypeDefInfo> left, IReadOnlyList<TypeDefInfo> right)
    {
        var leftByName = new Dictionary<string, TypeDefInfo>();
        foreach (var t in left) leftByName.TryAdd(t.FullName, t);
        var rightByName = new Dictionary<string, TypeDefInfo>();
        foreach (var t in right) rightByName.TryAdd(t.FullName, t);
        var result = new List<DiffEntry<TypeDefInfo>>();

        foreach (var lt in left)
        {
            if (rightByName.TryGetValue(lt.FullName, out var rt))
            {
                var changes = new List<string>();
                if (lt.BaseType != rt.BaseType) changes.Add($"base: {lt.BaseType} -> {rt.BaseType}");
                if (lt.MethodCount != rt.MethodCount) changes.Add($"methods: {lt.MethodCount} -> {rt.MethodCount}");
                if (lt.FieldCount != rt.FieldCount) changes.Add($"fields: {lt.FieldCount} -> {rt.FieldCount}");
                if (lt.Attributes != rt.Attributes) changes.Add("attributes changed");

                result.Add(changes.Count > 0
                    ? new DiffEntry<TypeDefInfo>(DiffKind.Changed, lt, rt, string.Join(", ", changes))
                    : new DiffEntry<TypeDefInfo>(DiffKind.Unchanged, lt, rt, null));
            }
            else
            {
                result.Add(new DiffEntry<TypeDefInfo>(DiffKind.Removed, lt, null, "removed"));
            }
        }

        foreach (var rt in right)
        {
            if (!leftByName.ContainsKey(rt.FullName))
                result.Add(new DiffEntry<TypeDefInfo>(DiffKind.Added, null, rt, "added"));
        }

        return result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.FullName).ToList();
    }

    private static IReadOnlyList<DiffEntry<MethodDefInfo>> CompareMethods(
        IReadOnlyList<MethodDefInfo> left, IReadOnlyList<MethodDefInfo> right)
    {
        static string Key(MethodDefInfo m) => $"{m.DeclaringType}::{m.Name}{m.Signature}";

        var leftByKey = new Dictionary<string, MethodDefInfo>();
        foreach (var m in left)
        {
            var key = Key(m);
            leftByKey.TryAdd(key, m);
        }

        var rightByKey = new Dictionary<string, MethodDefInfo>();
        foreach (var m in right)
        {
            var key = Key(m);
            rightByKey.TryAdd(key, m);
        }

        var result = new List<DiffEntry<MethodDefInfo>>();

        foreach (var (key, lm) in leftByKey)
        {
            if (rightByKey.TryGetValue(key, out var rm))
            {
                var changes = new List<string>();
                if (lm.Attributes != rm.Attributes) changes.Add("attributes changed");
                if (lm.ImplAttributes != rm.ImplAttributes) changes.Add("impl changed");

                result.Add(changes.Count > 0
                    ? new DiffEntry<MethodDefInfo>(DiffKind.Changed, lm, rm, string.Join(", ", changes))
                    : new DiffEntry<MethodDefInfo>(DiffKind.Unchanged, lm, rm, null));
            }
            else
            {
                result.Add(new DiffEntry<MethodDefInfo>(DiffKind.Removed, lm, null, "removed"));
            }
        }

        foreach (var (key, rm) in rightByKey)
        {
            if (!leftByKey.ContainsKey(key))
                result.Add(new DiffEntry<MethodDefInfo>(DiffKind.Added, null, rm, "added"));
        }

        return result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.DeclaringType).ToList();
    }

    private static IReadOnlyList<DiffEntry<AssemblyRefInfo>> CompareRefs(
        IReadOnlyList<AssemblyRefInfo> left, IReadOnlyList<AssemblyRefInfo> right)
    {
        var leftByName = new Dictionary<string, AssemblyRefInfo>();
        foreach (var r in left) leftByName.TryAdd(r.Name, r);
        var rightByName = new Dictionary<string, AssemblyRefInfo>();
        foreach (var r in right) rightByName.TryAdd(r.Name, r);
        var result = new List<DiffEntry<AssemblyRefInfo>>();

        foreach (var lr in left)
        {
            if (rightByName.TryGetValue(lr.Name, out var rr))
            {
                var changes = new List<string>();
                if (lr.Version != rr.Version) changes.Add($"version: {lr.Version} -> {rr.Version}");
                if (lr.PublicKeyToken != rr.PublicKeyToken) changes.Add("public key changed");

                result.Add(changes.Count > 0
                    ? new DiffEntry<AssemblyRefInfo>(DiffKind.Changed, lr, rr, string.Join(", ", changes))
                    : new DiffEntry<AssemblyRefInfo>(DiffKind.Unchanged, lr, rr, null));
            }
            else
            {
                result.Add(new DiffEntry<AssemblyRefInfo>(DiffKind.Removed, lr, null, "removed"));
            }
        }

        foreach (var rr in right)
        {
            if (!leftByName.ContainsKey(rr.Name))
                result.Add(new DiffEntry<AssemblyRefInfo>(DiffKind.Added, null, rr, "added"));
        }

        return result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.Name).ToList();
    }
}
