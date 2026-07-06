using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Builds JSON-ready Native AOT payloads shared by direct MCP tools and the diagnostics session
/// protocol, so the two transports return the same facts and error semantics.
/// </summary>
public static class NativeAotPayloadBuilder
{
    /// <summary>The default number of size contributors returned by Native AOT size tools.</summary>
    public const int DefaultTopN = 20;

    /// <summary>The default candidate count returned for ambiguous Native AOT why queries.</summary>
    public const int DefaultMaxCandidates = 20;

    /// <summary>The default number of DGML chains shown for an aggregate mstat entry.</summary>
    public const int DefaultMaxWhyChains = 3;

    /// <summary>Builds a Native AOT identity and sidecar summary for an analyzer.</summary>
    public static object BuildInfo(AssemblyAnalyzer analyzer)
    {
        RequireNativeAot(analyzer);

        var mstat = analyzer.Mstat;
        var dgmlPath = analyzer.DgmlPath;
        return new
        {
            analyzer.FilePath,
            analyzer.FileName,
            analyzer.FileSize,
            analyzer.Architecture,
            analyzer.BinaryKind,
            analyzer.NativeAotInfo,
            ReadyToRunSections = analyzer.ReadyToRunSections.Count,
            RecoveredTypes = analyzer.RecoveredTypes.Count,
            RecoveredMethods = analyzer.RecoveredTypes.Sum(t => t.MethodNames.Count),
            FrozenStrings = analyzer.FrozenStrings.Count,
            NativeSymbolCount = analyzer.NativeSymbols?.Symbols.Count ?? 0,
            NativeSymbolSource = analyzer.NativeSymbols?.Source,
            NativeSymbolStatus = analyzer.NativeSymbols?.Status,
            analyzer.MstatPath,
            HasMstat = mstat is not null,
            MstatFormat = mstat is null ? null : $"{mstat.FormatMajorVersion}.{mstat.FormatMinorVersion}",
            DgmlPath = dgmlPath,
            HasDgml = dgmlPath is not null,
            PreIlc = BuildPreIlcSummary(analyzer.PreIlcSidecars)
        };
    }

    /// <summary>Builds the Native AOT ReadyToRun module-section table payload.</summary>
    public static object BuildSections(AssemblyAnalyzer analyzer)
    {
        RequireNativeAot(analyzer);

        var sections = analyzer.ReadyToRunSections.Select(s => new
        {
            s.SectionId,
            s.Name,
            Address = $"0x{s.VirtualAddress:X}",
            s.VirtualAddress,
            s.Size,
            s.FileOffset,
            IsMapped = s.FileOffset is not null
        }).ToList();

        return new
        {
            analyzer.FilePath,
            SectionCount = sections.Count,
            Sections = sections
        };
    }

    /// <summary>Builds method-inventory rows, falling back to recovered Native AOT methods.</summary>
    public static object BuildMethodInventory(
        AssemblyAnalyzer analyzer, string? typeName, string? query, int? maxResults)
    {
        if (analyzer.HasMetadata || analyzer.RecoveredTypes.Count == 0)
            return analyzer.MethodDefs
                .Where(m => Matches(m.DeclaringType, typeName) && Matches(m.Name, query))
                .Take(PositiveOrMax(maxResults))
                .ToList();

        return analyzer.RecoveredTypes
            .Where(t => Matches(t.FullName, typeName))
            .SelectMany(t => t.MethodNames.Select((method, index) => new
            {
                Source = "RecoveredNativeAot",
                t.AssemblyName,
                DeclaringType = t.FullName,
                Name = method,
                MethodIndex = index
            }))
            .Where(m => Matches(m.Name, query))
            .Take(PositiveOrMax(maxResults))
            .ToList();
    }

    /// <summary>Builds member-search results, falling back to recovered Native AOT metadata.</summary>
    public static object BuildMemberSearch(
        AssemblyAnalyzer analyzer, string query, int? maxResults, bool includeCompilerGenerated)
    {
        var max = PositiveOr(maxResults, 100);
        if (analyzer.HasMetadata || analyzer.RecoveredTypes.Count == 0)
        {
            var types = analyzer.TypeDefs
                .Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));
            var methods = analyzer.MethodDefs
                .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || m.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (!includeCompilerGenerated)
            {
                types = types.Where(t => !t.Name.StartsWith("<>", StringComparison.Ordinal)
                    && !t.Name.Contains("__", StringComparison.Ordinal));
                methods = methods.Where(m => !m.DeclaringType.StartsWith("<>", StringComparison.Ordinal));
            }

            return new
            {
                Types = types.Take(max).ToList(),
                Methods = methods.Take(max).ToList(),
                MemberRefs = analyzer.MemberRefs
                    .Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(max)
                    .ToList()
            };
        }

        var recoveredTypes = analyzer.RecoveredTypes
            .Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (!includeCompilerGenerated)
            recoveredTypes = recoveredTypes.Where(t => !IsCompilerGenerated(t.FullName));

        var typeRows = recoveredTypes.Take(max).Select(t => new
        {
            Source = "RecoveredNativeAot",
            t.AssemblyName,
            t.FullName,
            MethodCount = t.MethodNames.Count
        }).ToList();

        var methodRows = analyzer.RecoveredTypes
            .Where(t => includeCompilerGenerated || !IsCompilerGenerated(t.FullName))
            .SelectMany(t => t.MethodNames.Select((method, index) => new
            {
                Source = "RecoveredNativeAot",
                t.AssemblyName,
                DeclaringType = t.FullName,
                Name = method,
                MethodIndex = index
            }))
            .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();

        return new
        {
            Types = typeRows,
            Methods = methodRows,
            MemberRefs = Array.Empty<object>()
        };
    }

    /// <summary>Builds largest-method rows, using native mstat data for Native AOT.</summary>
    public static object BuildLargestMethods(AssemblyAnalyzer analyzer, int? maxResults)
    {
        var max = PositiveOr(maxResults, 20);
        if (analyzer.BinaryKind == BinaryKind.NativeAot)
        {
            if (analyzer.Mstat is { } mstat)
            {
                var index = MstatSizeIndex.Create(mstat);
                return index.Entries
                    .Where(e => e.Section == MstatSectionKind.Method)
                    .OrderByDescending(e => e.Size)
                    .Take(max)
                    .Select(e => new
                    {
                        Source = "Mstat",
                        Method = new
                        {
                            e.AssemblyName,
                            e.Namespace,
                            DeclaringType = e.TypeName,
                            Name = e.DisplayName,
                            Signature = e.LeafName == e.DisplayName ? null : e.LeafName
                        },
                        e.Size,
                        e.FullPath,
                        e.NodeNames
                    })
                    .ToList();
            }

            if (analyzer.NativeSymbols is { Symbols.Count: > 0 } symbols)
            {
                return symbols.Symbols
                    .Where(s => s.Kind is NativeSymbolKind.Function or NativeSymbolKind.Stub)
                    .OrderByDescending(s => s.Size)
                    .Take(max)
                    .Select(s => new
                    {
                        Source = "NativeSymbols",
                        Method = new
                        {
                            Name = s.ManagedName ?? s.Name,
                            Address = $"0x{s.VirtualAddress:X}"
                        },
                        s.Size,
                        s.FileOffset,
                        s.VirtualAddress
                    })
                    .ToList();
            }
        }

        return analyzer.MethodDefs
            .Select(m => new { Method = m, Size = GetIlSize(analyzer, m) })
            .OrderByDescending(x => x.Size)
            .Take(max)
            .ToList();
    }

    /// <summary>Builds top Native AOT size contributors from an mstat-backed input.</summary>
    public static object BuildSizeContributors(
        MstatSource source,
        string? query,
        string? section,
        string? assemblyName,
        string? namespaceName,
        int? topN,
        bool includeWhy,
        int? maxWhyChains)
    {
        var index = MstatSizeIndex.Create(source.Data);
        var entries = index.Entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(section))
        {
            var parsed = ParseSection(section);
            entries = entries.Where(e => e.Section == parsed);
        }

        if (!string.IsNullOrWhiteSpace(assemblyName))
            entries = entries.Where(e => e.AssemblyName.Contains(assemblyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(namespaceName))
            entries = entries.Where(e => e.Namespace.Contains(namespaceName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
            entries = entries.Where(e => EntryMatches(e, query));

        var filtered = entries.OrderByDescending(e => e.Size).ToList();
        var top = PositiveOr(topN, DefaultTopN);
        var dgml = includeWhy && source.DgmlPath is not null ? DgmlReader.Read(source.DgmlPath) : null;
        var whyLimit = PositiveOr(maxWhyChains, DefaultMaxWhyChains);

        return new
        {
            Source = BuildMstatSourceSummary(source, index),
            Filters = new { Query = query, Section = section, AssemblyName = assemblyName, Namespace = namespaceName },
            TotalMatches = filtered.Count,
            Returned = Math.Min(top, filtered.Count),
            Truncated = filtered.Count > top,
            WhyAvailable = includeWhy ? dgml is not null : (bool?)null,
            WhyUnavailableReason = includeWhy && dgml is null
                ? "DGML sidecar not found; publish with IlcGenerateDgmlFile to explain dependency roots."
                : null,
            Contributors = filtered.Take(top).Select(e => BuildContributor(e, dgml, whyLimit)).ToList()
        };
    }

    /// <summary>Builds a Native AOT DGML explanation for one mstat contributor target.</summary>
    public static object BuildWhy(MstatSource source, string target, int? maxCandidates, int? maxWhyChains)
    {
        if (source.DgmlPath is null)
            throw new InvalidOperationException(
                "DGML sidecar not found; publish with IlcGenerateDgmlFile to explain dependency roots.");

        var dgml = DgmlReader.Read(source.DgmlPath)
            ?? throw new InvalidOperationException("DGML sidecar could not be read.");

        var index = MstatSizeIndex.Create(source.Data);
        var matches = ResolveContributorCandidates(index.Entries, target).ToList();
        if (matches.Count == 0)
        {
            return new
            {
                Target = target,
                Source = BuildMstatSourceSummary(source, index),
                Outcome = "not_found",
                Message = $"No Native AOT size contributor matches '{target}'."
            };
        }

        var max = PositiveOr(maxCandidates, DefaultMaxCandidates);
        if (matches.Count > 1)
        {
            return new
            {
                Target = target,
                Source = BuildMstatSourceSummary(source, index),
                Outcome = "ambiguous",
                CandidateCount = matches.Count,
                Candidates = matches.Take(max).Select(BuildCandidate).ToList(),
                Truncated = matches.Count > max
            };
        }

        var entry = matches[0];
        return new
        {
            Target = target,
            Source = BuildMstatSourceSummary(source, index),
            Outcome = "resolved",
            Contributor = BuildContributor(entry, dgml, PositiveOr(maxWhyChains, DefaultMaxWhyChains))
        };
    }

    /// <summary>Resolves a Native AOT analyzer's mstat source, or null when no size report exists.</summary>
    public static MstatSource? ResolveMstatSource(AssemblyAnalyzer analyzer)
    {
        RequireNativeAot(analyzer);
        return analyzer.MstatPath is { } path && analyzer.Mstat is { } data
            ? new MstatSource(data, path, analyzer.FilePath, analyzer.FileSize, analyzer.DgmlPath)
            : null;
    }

    private static void RequireNativeAot(AssemblyAnalyzer analyzer)
    {
        if (analyzer.BinaryKind != BinaryKind.NativeAot)
            throw new InvalidOperationException("Native AOT analysis requires a Native AOT binary.");
    }

    private static object? BuildPreIlcSummary(PreIlcSidecars? s) => s is null
        ? null
        : new
        {
            s.HasAttachableCompanion,
            RootAssembly = s.ManagedAssemblyPath is { } p ? Path.GetFileName(p) : null,
            Origin = s.Origin.ToString(),
            PdbStatus = s.PdbStatus.ToString(),
            HasMstat = s.MstatPath is not null,
            HasDgml = (s.CodegenDgmlPath ?? s.ScanDgmlPath) is not null,
            LocalReferenceCount = s.LocalReferencePaths.Count,
            s.PackageReferenceCount,
            s.OtherReferenceCount
        };

    private static object BuildMstatSourceSummary(MstatSource source, MstatSizeIndex index) => new
    {
        Target = source.BinaryPath ?? source.MstatPath,
        source.BinaryPath,
        source.BinaryFileSize,
        source.MstatPath,
        source.DgmlPath,
        Format = $"{source.Data.FormatMajorVersion}.{source.Data.FormatMinorVersion}",
        MstatTotal = index.Total,
        FileSize = source.BinaryFileSize,
        EntryCount = index.Entries.Count
    };

    private static object BuildContributor(MstatSizeEntry entry, DgmlGraph? dgml, int maxWhyChains) => new
    {
        entry.Section,
        entry.Key,
        entry.AssemblyName,
        entry.Namespace,
        entry.TypeName,
        entry.LeafName,
        entry.DisplayName,
        entry.FullPath,
        entry.Size,
        entry.EntryCount,
        entry.NodeNames,
        WhyChains = dgml is null ? null : BuildWhyChains(entry, dgml, maxWhyChains)
    };

    private static object BuildCandidate(MstatSizeEntry entry) => new
    {
        entry.Section,
        entry.Key,
        entry.FullPath,
        entry.DisplayName,
        entry.Size,
        entry.EntryCount,
        entry.NodeNames
    };

    private static List<object> BuildWhyChains(MstatSizeEntry entry, DgmlGraph dgml, int maxWhyChains)
    {
        var shown = Math.Min(maxWhyChains, entry.NodeNames.Count);
        var chains = new List<object>(shown);
        for (var i = 0; i < shown; i++)
        {
            var nodeName = entry.NodeNames[i];
            var steps = dgml.PathToRoot(nodeName);
            chains.Add(new
            {
                NodeName = nodeName,
                Found = steps.Count > 0,
                Steps = steps
            });
        }

        return chains;
    }

    private static IEnumerable<MstatSizeEntry> ResolveContributorCandidates(
        IReadOnlyList<MstatSizeEntry> entries, string target)
    {
        var exact = entries.Where(e =>
            string.Equals(e.FullPath, target, StringComparison.Ordinal)
            || string.Equals(e.Key, target, StringComparison.Ordinal)
            || string.Equals(e.DisplayName, target, StringComparison.Ordinal)
            || string.Equals(e.LeafName, target, StringComparison.Ordinal)
            || e.NodeNames.Contains(target, StringComparer.Ordinal)).ToList();
        if (exact.Count > 0)
            return exact.OrderByDescending(e => e.Size);

        return entries
            .Where(e => EntryMatches(e, target))
            .OrderByDescending(e => e.Size);
    }

    private static bool EntryMatches(MstatSizeEntry entry, string query) =>
        entry.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.LeafName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.AssemblyName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Namespace.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.NodeNames.Any(n => n.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static MstatSectionKind ParseSection(string section)
    {
        var normalized = section.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        foreach (var name in Enum.GetNames<MstatSectionKind>())
        {
            if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<MstatSectionKind>(name);
        }

        throw new InvalidOperationException(
            $"Unknown Native AOT mstat section '{section}'. Expected one of: "
            + string.Join(", ", Enum.GetNames<MstatSectionKind>()) + ".");
    }

    private static int GetIlSize(AssemblyAnalyzer analyzer, MethodDefInfo method)
    {
        try
        {
            return analyzer.GetMethodBody(method)?.GetILBytes()?.Length ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool Matches(string value, string? query) =>
        string.IsNullOrEmpty(query) || value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static int PositiveOr(int? value, int fallback) => value is > 0 ? value.Value : fallback;

    private static int PositiveOrMax(int? value) => value is > 0 ? value.Value : int.MaxValue;

    private static bool IsCompilerGenerated(string fullName) =>
        fullName.StartsWith("<>", StringComparison.Ordinal) || fullName.Contains("__", StringComparison.Ordinal);
}
