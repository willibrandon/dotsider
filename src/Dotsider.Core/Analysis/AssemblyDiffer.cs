using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.Signatures;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Compares two assemblies and produces a detailed diff result.
/// Uses dictionary-based O(n) matching by name.
/// </summary>
public static class AssemblyDiffer
{
    /// <summary>
    /// Compares two assemblies and returns a structured diff result.
    /// </summary>
    /// <param name="left">The baseline assembly.</param>
    /// <param name="right">The changed assembly.</param>
    /// <returns>A diff result containing type, method, and reference differences.</returns>
    public static AssemblyDiffResult Compare(AssemblyAnalyzer left, AssemblyAnalyzer right)
    {
        var typeDiffs = CompareTypes(left.TypeDefs, right.TypeDefs);
        var methodDiffs = CompareMethods(left.MethodDefs, right.MethodDefs, left, right);
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
        var leftByName = new Dictionary<string, TypeDefInfo>(left.Count);
        foreach (var t in left) leftByName.TryAdd(t.FullName, t);
        var rightByName = new Dictionary<string, TypeDefInfo>(right.Count);
        foreach (var t in right) rightByName.TryAdd(t.FullName, t);
        var result = new List<DiffEntry<TypeDefInfo>>(left.Count + right.Count);

        foreach (var lt in left)
        {
            if (rightByName.TryGetValue(lt.FullName, out var rt))
            {
                string? detail = null;
                if (lt.BaseType != rt.BaseType || lt.MethodCount != rt.MethodCount
                    || lt.FieldCount != rt.FieldCount || lt.Attributes != rt.Attributes)
                {
                    var changes = new List<string>(4);
                    if (lt.BaseType != rt.BaseType) changes.Add($"base: {lt.BaseType} -> {rt.BaseType}");
                    if (lt.MethodCount != rt.MethodCount) changes.Add($"methods: {lt.MethodCount} -> {rt.MethodCount}");
                    if (lt.FieldCount != rt.FieldCount) changes.Add($"fields: {lt.FieldCount} -> {rt.FieldCount}");
                    if (lt.Attributes != rt.Attributes) changes.Add("attributes changed");
                    detail = string.Join(", ", changes);
                }

                result.Add(detail is not null
                    ? new DiffEntry<TypeDefInfo>(DiffKind.Changed, lt, rt, detail)
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

        return [.. result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.FullName)];
    }

    private static IReadOnlyList<DiffEntry<MethodDefInfo>> CompareMethods(
        IReadOnlyList<MethodDefInfo> left, IReadOnlyList<MethodDefInfo> right,
        AssemblyAnalyzer leftAnalyzer, AssemblyAnalyzer rightAnalyzer)
    {
        // Tuple key avoids 60K+ string allocations from interpolation on large assemblies
        var leftByKey = new Dictionary<(string DeclaringType, string Name, string Signature), MethodDefInfo>(left.Count);
        foreach (var m in left)
            leftByKey.TryAdd((m.DeclaringType, m.Name, m.Signature), m);

        var rightByKey = new Dictionary<(string DeclaringType, string Name, string Signature), MethodDefInfo>(right.Count);
        foreach (var m in right)
            rightByKey.TryAdd((m.DeclaringType, m.Name, m.Signature), m);

        var result = new List<DiffEntry<MethodDefInfo>>(left.Count + right.Count);

        foreach (var (key, lm) in leftByKey)
        {
            if (rightByKey.TryGetValue(key, out var rm))
            {
                bool attrsChanged = lm.Attributes != rm.Attributes;
                bool implChanged = lm.ImplAttributes != rm.ImplAttributes;
                bool bodyChanged = BodiesDiffer(lm, rm, leftAnalyzer, rightAnalyzer);

                if (attrsChanged || implChanged || bodyChanged)
                {
                    var changes = new List<string>(3);
                    if (attrsChanged) changes.Add("attributes");
                    if (implChanged) changes.Add("impl");
                    if (bodyChanged) changes.Add("body");
                    result.Add(new DiffEntry<MethodDefInfo>(
                        DiffKind.Changed, lm, rm, string.Join(", ", changes)));
                }
                else
                {
                    result.Add(new DiffEntry<MethodDefInfo>(DiffKind.Unchanged, lm, rm, null));
                }
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

        return [.. result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.DeclaringType)];
    }

    private static IReadOnlyList<DiffEntry<AssemblyRefInfo>> CompareRefs(
        IReadOnlyList<AssemblyRefInfo> left, IReadOnlyList<AssemblyRefInfo> right)
    {
        var leftByName = new Dictionary<string, AssemblyRefInfo>(left.Count);
        foreach (var r in left) leftByName.TryAdd(r.Name, r);
        var rightByName = new Dictionary<string, AssemblyRefInfo>(right.Count);
        foreach (var r in right) rightByName.TryAdd(r.Name, r);
        var result = new List<DiffEntry<AssemblyRefInfo>>(left.Count + right.Count);

        foreach (var lr in left)
        {
            if (rightByName.TryGetValue(lr.Name, out var rr))
            {
                string? detail = null;
                if (lr.Version != rr.Version || lr.PublicKeyToken != rr.PublicKeyToken)
                {
                    var changes = new List<string>(2);
                    if (lr.Version != rr.Version) changes.Add($"version: {lr.Version} -> {rr.Version}");
                    if (lr.PublicKeyToken != rr.PublicKeyToken) changes.Add("public key changed");
                    detail = string.Join(", ", changes);
                }

                result.Add(detail is not null
                    ? new DiffEntry<AssemblyRefInfo>(DiffKind.Changed, lr, rr, detail)
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

        return [.. result.OrderBy(d => d.Kind).ThenBy(d => (d.Left ?? d.Right)!.Name)];
    }

    private static bool BodiesDiffer(
        MethodDefInfo leftMethod, MethodDefInfo rightMethod,
        AssemblyAnalyzer leftAnalyzer, AssemblyAnalyzer rightAnalyzer)
    {
        // Tier 0: Both abstract/extern — no bodies to compare
        if (leftMethod.Rva == 0 && rightMethod.Rva == 0)
            return false;
        if (leftMethod.Rva == 0 || rightMethod.Rva == 0)
            return true;

        // Tier 1: Retrieve method bodies
        MethodBodyBlock? leftBody, rightBody;
        try { leftBody = leftAnalyzer.GetMethodBody(leftMethod); }
        catch (BadImageFormatException) { leftBody = null; }
        try { rightBody = rightAnalyzer.GetMethodBody(rightMethod); }
        catch (BadImageFormatException) { rightBody = null; }

        if (leftBody is null && rightBody is null) return false;
        if (leftBody is null || rightBody is null) return true;

        // Tier 2: Structural checks
        if (leftBody.MaxStack != rightBody.MaxStack) return true;
        if (leftBody.LocalVariablesInitialized != rightBody.LocalVariablesInitialized) return true;

        // Tier 3: Local variable signature comparison
        if (LocalSignaturesDiffer(
            leftAnalyzer.GetMetadataReader(), leftBody,
            rightAnalyzer.GetMetadataReader(), rightBody))
            return true;

        // Tier 4: Exception region comparison
        var leftRegions = leftBody.ExceptionRegions;
        var rightRegions = rightBody.ExceptionRegions;
        if (leftRegions.Length != rightRegions.Length) return true;

        for (int i = 0; i < leftRegions.Length; i++)
        {
            var lr = leftRegions[i];
            var rr = rightRegions[i];
            if (lr.Kind != rr.Kind
                || lr.TryOffset != rr.TryOffset
                || lr.TryLength != rr.TryLength
                || lr.HandlerOffset != rr.HandlerOffset
                || lr.HandlerLength != rr.HandlerLength
                || lr.FilterOffset != rr.FilterOffset)
                return true;

            if (!lr.CatchType.IsNil || !rr.CatchType.IsNil)
            {
                if (lr.CatchType.IsNil != rr.CatchType.IsNil) return true;
                var leftCatch = leftAnalyzer.ResolveTokenForComparison(MetadataTokens.GetToken(lr.CatchType));
                var rightCatch = rightAnalyzer.ResolveTokenForComparison(MetadataTokens.GetToken(rr.CatchType));
                if (leftCatch != rightCatch) return true;
            }
        }

        // Tier 5: Normalized IL instruction walk
        var leftIl = leftBody.GetILBytes();
        var rightIl = rightBody.GetILBytes();
        if (leftIl is null && rightIl is null) return false;
        if (leftIl is null || rightIl is null) return true;

        return NormalizedIlDiffers(leftIl, rightIl, leftAnalyzer, rightAnalyzer);
    }

    /// <summary>
    /// Compares local variable signatures by decoding both sides to type name arrays.
    /// </summary>
    /// <param name="leftReader">The metadata reader for the left assembly.</param>
    /// <param name="leftBody">The left method body.</param>
    /// <param name="rightReader">The metadata reader for the right assembly.</param>
    /// <param name="rightBody">The right method body.</param>
    /// <returns><see langword="true"/> if the local variable signatures differ.</returns>
    internal static bool LocalSignaturesDiffer(
        MetadataReader? leftReader, MethodBodyBlock leftBody,
        MetadataReader? rightReader, MethodBodyBlock rightBody)
    {
        if (leftBody.LocalSignature.IsNil && rightBody.LocalSignature.IsNil)
        {
            return false;
        }
        if (leftBody.LocalSignature.IsNil != rightBody.LocalSignature.IsNil)
        {
            return true;
        }

        if (leftReader is null || rightReader is null)
        {
            return true;
        }

        ImmutableArray<string> leftLocals, rightLocals;
        try
        {
            leftLocals = SafeSignatureDecoder.DecodeLocalSignature(
                leftReader,
                leftBody.LocalSignature,
                new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                genericContext: default);
        }
        catch (BadImageFormatException)
        {
            return true;
        }

        try
        {
            rightLocals = SafeSignatureDecoder.DecodeLocalSignature(
                rightReader,
                rightBody.LocalSignature,
                new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                genericContext: default);
        }
        catch (BadImageFormatException)
        {
            return true;
        }

        if (leftLocals.Length != rightLocals.Length)
        {
            return true;
        }

        for (var i = 0; i < leftLocals.Length; i++)
        {
            if (leftLocals[i] != rightLocals[i])
            {
                return true;
            }
        }

        return false;
    }

    private static bool NormalizedIlDiffers(
        byte[] leftIl, byte[] rightIl,
        AssemblyAnalyzer leftAnalyzer, AssemblyAnalyzer rightAnalyzer)
    {
        int leftOffset = 0, rightOffset = 0;

        while (leftOffset < leftIl.Length && rightOffset < rightIl.Length)
        {
            if (!IlOperandReader.TryReadOpCode(leftIl, ref leftOffset, out ILOpCode leftOp)
                || !IlOperandReader.TryReadOpCode(rightIl, ref rightOffset, out ILOpCode rightOp))
            {
                return true;
            }

            if (leftOp != rightOp) return true;

            var operandKind = IlDisassembler.GetOperandType(leftOp);
            if (!IlOperandReader.TryGetOperandLength(leftIl, leftOffset, operandKind, out int leftLength)
                || !IlOperandReader.TryGetOperandLength(rightIl, rightOffset, operandKind, out int rightLength))
            {
                return true;
            }

            switch (operandKind)
            {
                case OperandKind.None:
                    break;

                case OperandKind.ShortBranchTarget:
                case OperandKind.ShortInlineI:
                case OperandKind.ShortInlineVar:
                case OperandKind.InlineVar:
                case OperandKind.BranchTarget:
                case OperandKind.InlineI:
                case OperandKind.ShortInlineR:
                case OperandKind.InlineI8:
                case OperandKind.InlineR:
                case OperandKind.InlineSwitch:
                    if (leftLength != rightLength
                        || !leftIl.AsSpan(leftOffset, leftLength)
                            .SequenceEqual(rightIl.AsSpan(rightOffset, rightLength)))
                        return true;
                    break;

                case OperandKind.InlineMethod:
                case OperandKind.InlineField:
                case OperandKind.InlineType:
                case OperandKind.InlineTok:
                    {
                        var leftToken = IlOperandReader.ReadInt32(leftIl, leftOffset);
                        var rightToken = IlOperandReader.ReadInt32(rightIl, rightOffset);
                        var leftResolved = leftAnalyzer.ResolveTokenForComparison(leftToken);
                        var rightResolved = rightAnalyzer.ResolveTokenForComparison(rightToken);
                        if (leftResolved != rightResolved) return true;
                        break;
                    }

                case OperandKind.InlineString:
                    {
                        var leftToken = IlOperandReader.ReadInt32(leftIl, leftOffset);
                        var rightToken = IlOperandReader.ReadInt32(rightIl, rightOffset);
                        var leftReader = leftAnalyzer.GetMetadataReader();
                        var rightReader = rightAnalyzer.GetMetadataReader();
                        if (leftReader is null || rightReader is null) return leftToken != rightToken;
                        try
                        {
                            var leftStr = leftReader.GetUserString(
                                MetadataTokens.UserStringHandle(leftToken & 0x00FFFFFF));
                            var rightStr = rightReader.GetUserString(
                                MetadataTokens.UserStringHandle(rightToken & 0x00FFFFFF));
                            if (leftStr != rightStr) return true;
                        }
                        catch
                        {
                            if (leftToken != rightToken) return true;
                        }
                        break;
                    }

                case OperandKind.InlineSig:
                    {
                        var leftToken = IlOperandReader.ReadInt32(leftIl, leftOffset);
                        var rightToken = IlOperandReader.ReadInt32(rightIl, rightOffset);
                        var leftResolved = leftAnalyzer.ResolveTokenForComparison(leftToken);
                        var rightResolved = rightAnalyzer.ResolveTokenForComparison(rightToken);
                        if (leftResolved != rightResolved) return true;
                        break;
                    }

                default:
                    break;
            }

            leftOffset += leftLength;
            rightOffset += rightLength;
        }

        return leftOffset != leftIl.Length || rightOffset != rightIl.Length;
    }
}
