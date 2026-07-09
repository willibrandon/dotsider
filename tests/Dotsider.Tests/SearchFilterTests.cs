using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for search filtering logic across different data types.
/// </summary>
[TestClass]
public class SearchFilterTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _app;
    }

    /// <summary>
    /// Verifies strings empty query returns all.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Strings_EmptyQuery_ReturnsAll()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1; // Metadata strings
        state.Search[TabId.Strings].Reset(); // No query
        var all = state.GetActiveStrings();
        Assert.IsNotEmpty(all);
    }

    /// <summary>
    /// Verifies strings with query filters results.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Strings_WithQuery_FiltersResults()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1;
        var all = state.GetActiveStrings();

        // Search for a string that exists in the metadata
        if (all.Count > 0)
        {
            var target = all[0].Value[..Math.Min(3, all[0].Value.Length)];
            state.Search[TabId.Strings].ActivateOrCycle();
            state.Search[TabId.Strings].UpdateQuery(target);
            var filtered = state.GetActiveStrings();
            Assert.IsLessThanOrEqualTo(all.Count, filtered.Count);
            TestAssert.All(filtered, e =>
                Assert.Contains(target, e.Value, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Verifies strings no match returns empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Strings_NoMatch_ReturnsEmpty()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1;
        state.Search[TabId.Strings].ActivateOrCycle();
        state.Search[TabId.Strings].UpdateQuery("zzzNoMatchEver12345zzz");
        var filtered = state.GetActiveStrings();
        Assert.IsEmpty(filtered);
    }

    /// <summary>
    /// Verifies strings case insensitive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Strings_CaseInsensitive()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1;
        var all = state.GetActiveStrings();
        if (all.Count > 0)
        {
            var target = all[0].Value[..Math.Min(3, all[0].Value.Length)].ToUpper();
            state.Search[TabId.Strings].ActivateOrCycle();
            state.Search[TabId.Strings].UpdateQuery(target);
            var filtered = state.GetActiveStrings();
            Assert.IsNotEmpty(filtered);
        }
    }

    /// <summary>
    /// Verifies assembly refs filter by name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyRefs_FilterByName()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        var refs = state.Analyzer.AssemblyRefs;
        if (refs.Count > 0)
        {
            var query = refs[0].Name[..Math.Min(5, refs[0].Name.Length)];
            var filtered = refs
                .Where(r => $"{r.Name} {r.Version} {r.Culture} {r.PublicKeyToken}"
                    .Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.IsNotEmpty(filtered);
            Assert.IsLessThanOrEqualTo(refs.Count, filtered.Count);
        }
    }

    /// <summary>
    /// Verifies special characters no regex error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SpecialCharacters_NoRegexError()
    {
        var app = CreateApp();
        using var state = new DotsiderState(app, Samples.RichLibraryDll);
        state.StringsSourceTab = 1;
        state.Search[TabId.Strings].ActivateOrCycle();
        state.Search[TabId.Strings].UpdateQuery("test.*(+?)");
        // Should not throw — uses string.Contains, not regex
        var filtered = state.GetActiveStrings();
        Assert.IsNotNull(filtered);
    }

    /// <summary>
    /// Verifies diff entries filter by type name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DiffEntries_FilterByTypeName()
    {
        var app = CreateApp();
        using var diffState = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        var entries = diffState.DiffResult.TypeDiffs;
        if (entries.Count > 0)
        {
            var type = entries[0].Right ?? entries[0].Left!;
            var query = type.FullName[..Math.Min(5, type.FullName.Length)];
            var filtered = entries.Where(e =>
            {
                var t = e.Right ?? e.Left!;
                return t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            Assert.IsNotEmpty(filtered);
        }
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
