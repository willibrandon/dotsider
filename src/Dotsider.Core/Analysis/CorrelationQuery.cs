using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using System.Globalization;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves a "method or address" query against an AOT binary's pre-ILC companion set and
/// correlation index, producing the one <see cref="CorrelationReport"/> the CLI, session, and
/// MCP surfaces all render. Attaches the companions on demand and builds the index once;
/// ambiguity is surfaced as candidates, never resolved by picking the first match.
/// </summary>
public static class CorrelationQuery
{
    /// <summary>
    /// Resolves <paramref name="methodOrAddress"/> against <paramref name="analyzer"/>: a
    /// <c>0x</c>-prefixed value is looked up by native address; anything else is matched by
    /// method name (optionally <c>Type.Method</c> / <c>Type::Method</c>) across the whole
    /// companion set. Attaches the pre-ILC companions if they are not yet attached.
    /// </summary>
    /// <param name="analyzer">The AOT binary's analyzer.</param>
    /// <param name="methodOrAddress">A method name, a qualified <c>Type.Method</c>, or a <c>0x</c> native address.</param>
    /// <param name="cancellationToken">Cancels the disassembly and match sweep.</param>
    /// <returns>The resolved report, the ambiguous candidates, or the reason nothing resolved.</returns>
    public static CorrelationQueryResult Resolve(
        AssemblyAnalyzer analyzer, string methodOrAddress, CancellationToken cancellationToken)
    {
        var companions = analyzer.PreIlcCompanions ?? analyzer.AttachPreIlcCompanions();
        if (companions is null)
        {
            return CorrelationQueryResult.Unavailable(
                analyzer.PreIlcSidecars is { HasAttachableCompanion: true }
                    ? "pre-ILC companion assembly could not be opened"
                    : "no pre-ILC managed assembly was found next to the binary");
        }

        if (analyzer.ManagedNativeIndex is not { } index)
            return CorrelationQueryResult.Unavailable("the correlation index could not be built");

        var query = methodOrAddress.Trim();
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ResolveByAddress(analyzer, companions, index, query, methodOrAddress, cancellationToken);

        return ResolveByName(analyzer, companions, index, query, methodOrAddress, cancellationToken);
    }

    private static CorrelationQueryResult ResolveByAddress(
        AssemblyAnalyzer analyzer, PreIlcCompanionSet companions, ManagedNativeIndex index,
        string query, string original, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(query.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var va))
            return CorrelationQueryResult.NotFound($"'{original}' is not a valid hexadecimal address");

        if (index.FindByAddress(va) is not { } correlation)
            return CorrelationQueryResult.NotFound($"no correlated method covers address 0x{va:X}");

        var owner = companions.FindByAssemblyName(correlation.AssemblyName) ?? companions.Root;
        return CorrelationQueryResult.Resolved(
            BuildReport(analyzer, index, owner, correlation, cancellationToken));
    }

    private static CorrelationQueryResult ResolveByName(
        AssemblyAnalyzer analyzer, PreIlcCompanionSet companions, ManagedNativeIndex index,
        string query, string original, CancellationToken cancellationToken)
    {
        var (typeFilter, methodName) = SplitName(query);

        var matches = new List<(AssemblyAnalyzer Owner, MethodCorrelation Correlation)>();
        foreach (var companion in companions.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var method in companion.MethodDefs)
            {
                if (!method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (typeFilter is not null
                    && !method.DeclaringType.EndsWith(typeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var correlation = index.Find(companion.AssemblyName ?? "", method.Token)
                    ?? new MethodCorrelation(
                        companion.AssemblyName ?? "", method,
                        MethodCorrelationStatus.NotInNativeImage, [], []);
                matches.Add((companion, correlation));
            }
        }

        if (matches.Count == 0)
            return CorrelationQueryResult.NotFound($"no method matches '{original}'");

        if (matches.Count > 1)
        {
            var candidates = matches
                .Select(m => new CorrelationCandidate(
                    m.Owner.AssemblyName ?? "(unknown)",
                    m.Correlation.Method.DeclaringType, m.Correlation.Method.Name,
                    m.Correlation.Method.Token,
                    m.Correlation.NativeSymbols.Count > 0
                        ? m.Correlation.NativeSymbols[0].VirtualAddress
                        : null))
                .ToList();
            return CorrelationQueryResult.Ambiguous(
                candidates, $"'{original}' is ambiguous ({matches.Count} matches)");
        }

        var (matchOwner, matchCorrelation) = matches[0];
        return CorrelationQueryResult.Resolved(
            BuildReport(analyzer, index, matchOwner, matchCorrelation, cancellationToken));
    }

    /// <summary>
    /// Splits a query into an optional trailing-type filter and a method name. <c>Type.Method</c>
    /// and <c>Type::Method</c> both yield <c>(Type, Method)</c>; a bare name yields <c>(null, name)</c>.
    /// </summary>
    private static (string? TypeFilter, string MethodName) SplitName(string query)
    {
        var separator = query.Contains("::") ? "::" : ".";
        var lastSeparator = query.LastIndexOf(separator, StringComparison.Ordinal);
        return lastSeparator < 0
            ? (null, query)
            : (query[..lastSeparator], query[(lastSeparator + separator.Length)..]);
    }

    private static CorrelationReport BuildReport(
        AssemblyAnalyzer analyzer, ManagedNativeIndex index,
        AssemblyAnalyzer owner, MethodCorrelation correlation, CancellationToken cancellationToken)
    {
        string? il = null;
        if (owner.HasMetadata)
        {
            try
            {
                il = new IlDisassembler(owner).FormatDisassembly(correlation.Method);
            }
            catch (Exception ex) when (ex is InvalidOperationException or BadImageFormatException)
            {
                il = null;
            }
        }

        string? ManagedNameResolver(ulong va) =>
            index.FindByAddress(va) is { } c ? $"{c.Method.DeclaringType}.{c.Method.Name}" : null;

        var symbols = new List<CorrelationReportSymbol>();
        var nativeChunks = new List<string>();
        foreach (var symbol in correlation.NativeSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            symbols.Add(new CorrelationReportSymbol(
                symbol.ManagedName ?? symbol.Name, symbol.VirtualAddress, symbol.FileOffset, symbol.Size));

            if (NativeDisassembler.DisassembleSymbol(analyzer, symbol, ManagedNameResolver) is { } disasm)
                nativeChunks.Add(disasm.Text);
        }

        return new CorrelationReport(
            Status: correlation.Status.ToString(),
            Assembly: correlation.AssemblyName,
            Method: $"{correlation.Method.DeclaringType}::{correlation.Method.Name}{correlation.Method.Signature}",
            Token: correlation.Method.Token,
            Symbols: symbols,
            NativeSize: correlation.NativeSize,
            SharedCandidateSize: correlation.SharedCandidateSize,
            MstatSize: correlation.MstatMethods.Sum(m => (long)m.Size),
            Il: il,
            NativeDisassembly: nativeChunks.Count > 0 ? string.Join("\n\n", nativeChunks) : null);
    }
}
