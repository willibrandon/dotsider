using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Globalization;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves a "method or address" query against a ReadyToRun image and builds the one
/// <see cref="ReadyToRunMethodReport"/> the CLI, MCP, and session surfaces all render. A method
/// name, a <c>0x06…</c> token, or a <c>0x…</c> native address all resolve here; a value that is
/// both a valid token and a covered address is reported ambiguous rather than guessed. Methods
/// present in metadata but not precompiled resolve as IL-only rather than "not found".
/// </summary>
public static class ReadyToRunCorrelationQuery
{
    /// <summary>
    /// Resolves <paramref name="methodOrAddress"/> against <paramref name="analyzer"/>.
    /// </summary>
    /// <param name="analyzer">The ReadyToRun image's analyzer.</param>
    /// <param name="methodOrAddress">A method name, a qualified <c>Type.Method</c>, a <c>0x06…</c> token, or a <c>0x…</c> native address.</param>
    /// <param name="cancellationToken">Cancels the disassembly and match sweep.</param>
    public static ReadyToRunQueryResult Resolve(
        AssemblyAnalyzer analyzer, string methodOrAddress, CancellationToken cancellationToken)
    {
        if (!analyzer.IsReadyToRun)
            return ReadyToRunQueryResult.Unavailable("not a ReadyToRun image");
        // Only a Valid image's tables are trusted. A corrupt or unsupported-version image exposes its
        // header/section diagnostics only — never a method, IL, or native body read out of a layout the
        // header does not vouch for.
        if (analyzer.ReadyToRunInfo is not { Status: ReadyToRunStatus.Valid })
            return ReadyToRunQueryResult.Unavailable(
                $"the ReadyToRun image is {analyzer.ReadyToRunInfo?.Status}; only header diagnostics are available");
        if (analyzer.ReadyToRunIndex is not { } index)
            return ReadyToRunQueryResult.Unavailable("the ReadyToRun method map is unavailable");

        var query = methodOrAddress.Trim();
        return query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ResolveNumeric(analyzer, index, query, methodOrAddress, cancellationToken)
            : ResolveByName(analyzer, index, query, methodOrAddress, cancellationToken);
    }

    private static ReadyToRunQueryResult ResolveNumeric(
        AssemblyAnalyzer analyzer, ReadyToRunIndex index, string query, string original, CancellationToken ct)
    {
        if (!ulong.TryParse(query.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return ReadyToRunQueryResult.NotFound($"'{original}' is not a valid hexadecimal value");

        var matches = new List<ReadyToRunMethodEntry>();

        // As a MethodDef token (0x06rrrrrr).
        if ((value & 0xFF00_0000) == 0x0600_0000 && value <= 0x06FF_FFFF)
            foreach (var m in index.Methods)
                if (m.Token == (int)value && !matches.Contains(m))
                    matches.Add(m);

        // As a native address.
        if (index.FindByAddress(value) is { } byAddress && !matches.Contains(byAddress))
            matches.Add(byAddress);

        if (matches.Count == 0)
            return ReadyToRunQueryResult.NotFound($"no method or address matches '{original}'");
        if (matches.Count > 1)
            return Ambiguous(matches, original);

        return ReadyToRunQueryResult.Resolved(BuildReport(analyzer, index, matches[0], ct));
    }

    private static ReadyToRunQueryResult ResolveByName(
        AssemblyAnalyzer analyzer, ReadyToRunIndex index, string query, string original, CancellationToken ct)
    {
        var (typeFilter, methodName) = SplitName(query);

        var precompiled = new List<ReadyToRunMethodEntry>();
        foreach (var m in index.Methods)
        {
            ct.ThrowIfCancellationRequested();
            if (Matches(m.DeclaringType, m.Name, typeFilter, methodName))
                precompiled.Add(m);
        }

        if (precompiled.Count == 1)
            return ReadyToRunQueryResult.Resolved(BuildReport(analyzer, index, precompiled[0], ct));
        if (precompiled.Count > 1)
            return Ambiguous(precompiled, original);

        // Not in the precompiled map — fall back to metadata so the method still resolves. Search each
        // metadata provider: this image, or (for a composite opened directly, which has no own metadata)
        // every resolved component. Names come from the provider that owns the method.
        var providers = analyzer.ReadyToRunMetadataProviders.Count > 0
            ? analyzer.ReadyToRunMetadataProviders
            : [analyzer];
        var ilOnly = new List<(AssemblyAnalyzer Provider, MethodDefInfo Method)>();
        foreach (var provider in providers)
            foreach (var m in provider.MethodDefs)
                if (Matches(m.DeclaringType, m.Name, typeFilter, methodName))
                    ilOnly.Add((provider, m));

        if (ilOnly.Count == 0)
            return ReadyToRunQueryResult.NotFound($"no method matches '{original}'");
        if (ilOnly.Count > 1)
        {
            List<CorrelationCandidate> candidates = [.. ilOnly.Select(e => new CorrelationCandidate(
                e.Provider.AssemblyName ?? "(unknown)", e.Method.DeclaringType, e.Method.Name, e.Method.Token, null))];
            return ReadyToRunQueryResult.Ambiguous(candidates, $"'{original}' is ambiguous ({ilOnly.Count} matches)");
        }

        // A component whose owner composite is not on disk is a distinct state — its native code
        // lives in the missing owner, not simply "not precompiled". Surface it honestly.
        if (analyzer.ReadyToRunInfo is { IsComponent: true } && analyzer.ReadyToRunCodeImage is null)
        {
            return ReadyToRunQueryResult.Resolved(BuildMetadataOnlyReport(
                ilOnly[0].Provider, ilOnly[0].Method, ReadyToRunNativeAvailability.OwnerCompositeMissing,
                "owner composite missing; native code unavailable"));
        }

        return ReadyToRunQueryResult.Resolved(BuildMetadataOnlyReport(
            ilOnly[0].Provider, ilOnly[0].Method, ReadyToRunNativeAvailability.NotPrecompiled,
            "IL only; not precompiled in this image"));
    }

    private static ReadyToRunQueryResult Ambiguous(List<ReadyToRunMethodEntry> matches, string original)
    {
        var candidates = matches.Select(m => new CorrelationCandidate(
            m.AssemblyName, m.DeclaringType ?? "(unknown)", m.Name ?? "(unknown)", m.Token,
            m.CodeRanges.Count > 0 ? m.CodeRanges[0].VirtualAddress : null)).ToList();
        return ReadyToRunQueryResult.Ambiguous(candidates, $"'{original}' is ambiguous ({matches.Count} matches)");
    }

    private static ReadyToRunMethodReport BuildReport(
        AssemblyAnalyzer analyzer, ReadyToRunIndex index, ReadyToRunMethodEntry entry, CancellationToken ct)
    {
        var info = analyzer.ReadyToRunInfo;
        // A composite's IL lives in the resolved component, not the composite itself; route through it.
        var metadataProvider = analyzer.ReadyToRunMetadataProviderFor(entry.Mvid);
        var (il, ilInstructions) = DisassembleIl(metadataProvider, entry.Token);
        // In a composite, a component whose metadata could not be resolved by name + MVID falls back
        // to the composite (which has no own metadata) — surface that honestly, not as plain IL-only.
        var componentMetadataMissing = info?.IsComposite == true && ReferenceEquals(metadataProvider, analyzer);

        string? nativeText = null;
        IReadOnlyList<NativeInstruction>? nativeInstructions = null;
        var availability = ReadyToRunNativeAvailability.Precompiled;
        string? diagnostic = null;

        var codeImage = analyzer.ReadyToRunCodeImage;
        if (codeImage is null)
        {
            availability = ReadyToRunNativeAvailability.OwnerCompositeMissing;
            diagnostic = "owner composite missing; native code unavailable";
        }
        else if (codeImage.ReadyToRunInfo?.Architecture is not (NativeArchitecture.X64 or NativeArchitecture.Arm64))
        {
            availability = ReadyToRunNativeAvailability.ArchUnsupported;
            diagnostic = $"precompiled; disassembly unsupported for {codeImage.ReadyToRunInfo?.Architecture}";
        }
        else
        {
            string? Resolver(ulong va) =>
                index.FindByAddress(va) is { DeclaringType: not null } e ? $"{e.DeclaringType}.{e.Name}" : null;
            NativeSymbolResolver? importResolver = null;
            if (!ReferenceEquals(codeImage, analyzer) && analyzer.ReadyToRunComponents.Count > 0)
            {
                var importMap = ReadyToRunImportMap.Build(
                    codeImage, analyzer.ReadyToRunComponents, analyzer.ReadyToRunMetadataProviderFor);
                importResolver = (va, out symbol) =>
                {
                    if (importMap is not null && importMap.TryResolve(va, out symbol))
                        return true;
                    symbol = default;
                    return false;
                };
            }

            if (ReadyToRunDisassembler.DisassembleMethod(codeImage, entry, Resolver, importResolver) is { } d)
            {
                nativeText = d.Text;
                nativeInstructions = d.Instructions;
            }

            ct.ThrowIfCancellationRequested();

            // Native is precompiled and rendered, but the managed name/IL is unavailable.
            if (componentMetadataMissing)
            {
                availability = ReadyToRunNativeAvailability.ComponentMetadataUnavailable;
                diagnostic = "component metadata unavailable; native code shown without IL";
            }
        }

        return new ReadyToRunMethodReport(
            availability, entry.AssemblyName, entry.Mvid,
            $"{entry.DeclaringType}::{entry.Name}{entry.Signature}", entry.Token,
            info?.IsComposite ?? false,
            info?.IsComposite == true ? entry.AssemblyName : null,
            entry.IsGenericInstantiation, entry.InstantiationDisplay,
            [.. entry.CodeRanges.Select(r => new CorrelationReportSymbol(
                r.Kind.ToString(), r.VirtualAddress, r.FileOffset, r.Size))],
            entry.TotalSize, il, ilInstructions, nativeText, nativeInstructions, diagnostic);
    }

    private static ReadyToRunMethodReport BuildMetadataOnlyReport(
        AssemblyAnalyzer analyzer, MethodDefInfo method,
        ReadyToRunNativeAvailability availability, string diagnostic)
    {
        var (il, ilInstructions) = DisassembleIl(analyzer, method.Token);
        return new ReadyToRunMethodReport(
            availability, analyzer.AssemblyName ?? "",
            Guid.Empty, $"{method.DeclaringType}::{method.Name}{method.Signature}", method.Token,
            analyzer.ReadyToRunInfo?.IsComposite ?? false, null,
            IsGenericInstantiation: false, InstantiationDisplay: null,
            Ranges: [], NativeSize: 0, il, ilInstructions,
            NativeText: null, NativeInstructions: null,
            Diagnostic: diagnostic);
    }

    private static (string? Il, IReadOnlyList<IlInstruction>? Instructions) DisassembleIl(
        AssemblyAnalyzer metadataProvider, int token)
    {
        if (!metadataProvider.HasMetadata) return (null, null);
        var method = metadataProvider.MethodDefs.FirstOrDefault(m => m.Token == token);
        if (method is null) return (null, null);

        try
        {
            var disassembler = new IlDisassembler(metadataProvider);
            return (disassembler.FormatDisassembly(method), disassembler.Disassemble(method));
        }
        catch (Exception ex) when (ex is InvalidOperationException or BadImageFormatException)
        {
            return (null, null);
        }
    }

    private static bool Matches(string? declaringType, string? name, string? typeFilter, string methodName) =>
        name is not null
        && name.Equals(methodName, StringComparison.OrdinalIgnoreCase)
        && (typeFilter is null || (declaringType?.EndsWith(typeFilter, StringComparison.OrdinalIgnoreCase) ?? false));

    private static (string? TypeFilter, string MethodName) SplitName(string query)
    {
        var separator = query.Contains("::") ? "::" : ".";
        var lastSeparator = query.LastIndexOf(separator, StringComparison.Ordinal);
        return lastSeparator < 0
            ? (null, query)
            : (query[..lastSeparator], query[(lastSeparator + separator.Length)..]);
    }
}
