using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Dotsider.Tests;

/// <summary>
/// Tests dependency-graph background-work ownership, publication, cancellation, and disposal.
/// </summary>
/// <param name="testContext">The current test context.</param>
[TestClass]
public sealed class DotsiderGraphBuildTests(TestContext testContext) : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private readonly TestContext _testContext = testContext;
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;

    /// <summary>
    /// Verifies repeated requests share one in-flight operation and publish its result once.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EnsureCachedGraphAsync_ConcurrentRequests_AreSingleFlight()
    {
        var cancellationToken = _testContext.CancellationToken;
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            GraphBuilder = (_, token) =>
            {
                Interlocked.Increment(ref invocationCount);
                started.TrySetResult();
                release.Wait(token);
                return CreateResult("single-flight");
            }
        };

        state.EnsureCachedGraphAsync();
        await started.Task.WaitAsync(cancellationToken);
        var graphBuildTask = state.GraphBuildTask;

        state.EnsureCachedGraphAsync();

        Assert.AreSame(graphBuildTask, state.GraphBuildTask);
        Assert.AreEqual(1, Volatile.Read(ref invocationCount));
        release.Set();
        await graphBuildTask.WaitAsync(cancellationToken);

        Assert.IsFalse(state.GraphBuildInProgress);
        Assert.IsNull(state.GraphNavigationError);
        Assert.AreEqual(
            "single-flight",
            Assert.ContainsSingle(state.GraphSnapshot!.Nodes).Name);
    }

    /// <summary>
    /// Verifies topology and navigation metadata remain unavailable until one complete graph
    /// snapshot is atomically published.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EnsureCachedGraphAsync_PublishesOneCompleteSnapshot()
    {
        var cancellationToken = _testContext.CancellationToken;
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var node = new GraphNode(
            Id: "snapshot",
            Name: "snapshot",
            Version: null,
            Culture: "neutral",
            PublicKeyToken: null,
            IsRoot: true,
            Depth: 0,
            Unresolved: false);
        var navigation = new GraphNavigationContext(
            Resolved: null,
            ReferencingFilePath: null,
            ReferencingBundlePath: null,
            ReferencingTargetFramework: null,
            ReferencingPreferredRuntimePack: null,
            Provenance: AssemblyProvenance.Unresolved,
            IsFrameworkAssembly: false,
            CandidateProbePath: null);
        var expected = new DependencyGraphResult(
            [node],
            [],
            new Dictionary<string, GraphNavigationContext>(StringComparer.Ordinal)
            {
                [node.Id] = navigation
            });
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            GraphBuilder = (_, token) =>
            {
                started.TrySetResult();
                release.Wait(token);
                return expected;
            }
        };

        state.EnsureCachedGraphAsync();
        await started.Task.WaitAsync(cancellationToken);

        Assert.IsNull(state.CachedGraph);

        release.Set();
        await state.GraphBuildTask.WaitAsync(cancellationToken);

        var snapshot = state.GraphSnapshot;
        Assert.IsNotNull(snapshot);
        Assert.IsInstanceOfType<ImmutableArray<GraphNode>>(snapshot.Nodes);
        Assert.IsInstanceOfType<ImmutableArray<GraphEdge>>(snapshot.Edges);
        Assert.IsInstanceOfType<FrozenDictionary<string, GraphNavigationContext>>(
            snapshot.NavigationById);
        Assert.AreSame(node, Assert.ContainsSingle(snapshot.Nodes));
        Assert.AreSame(navigation, snapshot.NavigationById[node.Id]);

        var compatibleTopology = state.CachedGraph;
        Assert.IsNotNull(compatibleTopology);
        Assert.AreSame(node, Assert.ContainsSingle(compatibleTopology.Value.Nodes));
        Assert.AreSame(navigation, state.GraphNavigation![node.Id]);
    }

    /// <summary>
    /// Verifies the existing public topology and navigation setters preserve their signatures and
    /// bridge legacy separate assignments into the internal snapshot.
    /// </summary>
    [TestMethod]
    public void CachedGraph_PublicSettersBridgeToInternalSnapshot()
    {
        var result = CreateResult("compatible");
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll);

        state.GraphNavigation = null;
        state.CachedGraph = (result.Nodes, result.Edges);

        var topologyOnly = state.GraphSnapshot;
        Assert.IsNotNull(topologyOnly);
        Assert.IsNull(topologyOnly.NavigationById);

        state.GraphNavigation = result.NavigationById;

        var complete = state.GraphSnapshot;
        Assert.IsNotNull(complete);
        Assert.IsNotNull(complete.NavigationById);
        var compatibleTopology = state.CachedGraph;
        Assert.IsNotNull(compatibleTopology);
        Assert.AreEqual(
            "compatible",
            Assert.ContainsSingle(compatibleTopology.Value.Nodes).Name);

        state.CachedGraph = null;

        Assert.IsNull(state.GraphSnapshot);
        Assert.AreSame(result.NavigationById, state.GraphNavigation);
    }

    /// <summary>
    /// Verifies changing analyzers cancels and drains the old build before a new graph can publish.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PushAssembly_DrainsOldBuildBeforePublishingNewGraph()
    {
        var cancellationToken = _testContext.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            GraphBuilder = (_, token) =>
            {
                started.TrySetResult();
                token.WaitHandle.WaitOne();
                cancellationObserved.TrySetResult();
                return CreateResult("stale");
            }
        };

        state.EnsureCachedGraphAsync();
        await started.Task.WaitAsync(cancellationToken);
        var staleTask = state.GraphBuildTask;

        Assert.IsTrue(state.PushAssembly(Samples.RichLibraryDll));

        await cancellationObserved.Task.WaitAsync(cancellationToken);
        Assert.IsTrue(staleTask.IsCompleted);
        Assert.IsNull(state.CachedGraph);
        Assert.IsFalse(state.GraphBuildInProgress);

        state.GraphBuilder = (_, _) => CreateResult("current");
        state.EnsureCachedGraphAsync();
        await state.GraphBuildTask.WaitAsync(cancellationToken);

        Assert.AreEqual(
            "current",
            Assert.ContainsSingle(state.GraphSnapshot!.Nodes).Name);
    }

    /// <summary>
    /// Verifies a graph failure is observed, clears the building state, and does not start a hot
    /// retry loop on subsequent renders.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EnsureCachedGraphAsync_Fault_ReportsStableFailureWithoutRetryLoop()
    {
        var cancellationToken = _testContext.CancellationToken;
        var invocationCount = 0;
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            GraphBuilder = (_, _) =>
            {
                Interlocked.Increment(ref invocationCount);
                throw new InvalidOperationException("synthetic graph failure");
            }
        };

        state.EnsureCachedGraphAsync();
        var graphBuildTask = state.GraphBuildTask;
        await graphBuildTask.WaitAsync(cancellationToken);

        Assert.IsFalse(state.GraphBuildInProgress);
        Assert.IsNull(state.CachedGraph);
        Assert.AreEqual("Cannot build dependency graph", state.GraphNavigationError);

        state.EnsureCachedGraphAsync();

        Assert.AreSame(graphBuildTask, state.GraphBuildTask);
        Assert.AreEqual(1, Volatile.Read(ref invocationCount));
    }

    /// <summary>
    /// Verifies disposal cancels and drains graph work before disposing the owned analyzer.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dispose_CancelsAndDrainsGraphBuild()
    {
        var cancellationToken = _testContext.CancellationToken;
        using var releaseAfterCancellation = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            GraphBuilder = (analyzer, token) =>
            {
                started.TrySetResult();
                try
                {
                    token.WaitHandle.WaitOne();
                    token.ThrowIfCancellationRequested();
                    return CreateResult("unexpected");
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                    releaseAfterCancellation.Wait(cancellationToken);
                    _ = analyzer.RawBytes.Length;
                    throw;
                }
            }
        };

        state.EnsureCachedGraphAsync();
        await started.Task.WaitAsync(cancellationToken);
        var graphBuildTask = state.GraphBuildTask;

        var disposeTask = Task.Run(state.Dispose, cancellationToken);
        await cancellationObserved.Task.WaitAsync(cancellationToken);
        bool completedBeforeRelease;
        bool analyzerUsableBeforeRelease;
        try
        {
            completedBeforeRelease = disposeTask.IsCompleted;
            analyzerUsableBeforeRelease = state.Analyzer.RawBytes.Length > 0;
        }
        finally
        {
            releaseAfterCancellation.Set();
        }

        await disposeTask.WaitAsync(cancellationToken);
        state.Dispose();

        Assert.IsFalse(completedBeforeRelease);
        Assert.IsTrue(analyzerUsableBeforeRelease);
        Assert.IsTrue(graphBuildTask.IsCompleted);
        Assert.IsFalse(state.GraphBuildInProgress);
    }

    /// <summary>
    /// Verifies a successfully-published graph's completion nudger is lifetime-owned and fully
    /// drained before disposal returns.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dispose_AfterGraphPublication_DrainsRenderNudgers()
    {
        var cancellationToken = _testContext.CancellationToken;
        using var state = new DotsiderState(CreateApp(), Samples.HelloWorldDll)
        {
            CurrentTab = TabId.IlInspector,
            GraphBuilder = (_, _) => CreateResult("complete")
        };

        state.EnsureCachedGraphAsync();
        await state.GraphBuildTask.WaitAsync(cancellationToken);

        Assert.IsNotNull(state.CachedGraph);
        var renderNudgerTask = state.RenderNudgerTask;

        state.Dispose();

        Assert.IsTrue(renderNudgerTask.IsCompleted);
        state.RequestExtraFrame();
        Assert.IsTrue(state.RenderNudgerTask.IsCompleted);
    }

    /// <summary>
    /// Verifies saving edited bytes drains graph work before disposing its analyzer and permits
    /// the replacement analyzer to build a fresh graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SaveHexChanges_DrainsOldGraphBeforeReplacingAnalyzer()
    {
        var cancellationToken = _testContext.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("dotsider-graph-save-test-").FullName;
        var assemblyPath = Path.Combine(directory, "HelloWorld.dll");
        File.Copy(Samples.HelloWorldDll, assemblyPath);

        var state = new DotsiderState(CreateApp(), assemblyPath);
        using var releaseAfterCancellation = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var analyzerRemainedUsable = false;
        var oldAnalyzer = state.Analyzer;
        state.GraphBuilder = (analyzer, token) =>
        {
            started.TrySetResult();
            try
            {
                token.WaitHandle.WaitOne();
                token.ThrowIfCancellationRequested();
                return CreateResult("unexpected");
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult();
                releaseAfterCancellation.Wait(cancellationToken);
                analyzerRemainedUsable = analyzer.RawBytes.Length > 0;
                throw;
            }
        };

        try
        {
            state.EnsureCachedGraphAsync();
            await started.Task.WaitAsync(cancellationToken);
            var staleTask = state.GraphBuildTask;

            var saveTask = Task.Run(
                () => DotsiderApp.SaveHexChanges(
                    state,
                    (_, bytes, path) => (new AssemblyAnalyzer(bytes, path), null)),
                cancellationToken);
            await cancellationObserved.Task.WaitAsync(cancellationToken);

            Assert.IsFalse(saveTask.IsCompleted);
            Assert.AreSame(oldAnalyzer, state.Analyzer);
            releaseAfterCancellation.Set();
            await saveTask.WaitAsync(cancellationToken);

            Assert.IsTrue(staleTask.IsCompleted);
            Assert.IsTrue(analyzerRemainedUsable);
            Assert.AreNotSame(oldAnalyzer, state.Analyzer);
            Assert.IsNull(state.CachedGraph);
            Assert.IsNull(state.GraphNavigationError);

            state.GraphBuilder = (_, _) => CreateResult("replacement");
            state.EnsureCachedGraphAsync();
            await state.GraphBuildTask.WaitAsync(cancellationToken);

            Assert.AreEqual(
                "replacement",
                Assert.ContainsSingle(state.GraphSnapshot!.Nodes).Name);
        }
        finally
        {
            releaseAfterCancellation.Set();
            state.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Disposes the test terminal and application resources.
    /// </summary>
    public void Dispose()
    {
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DependencyGraphResult CreateResult(string name)
    {
        var node = new GraphNode(
            Id: name,
            Name: name,
            Version: null,
            Culture: "neutral",
            PublicKeyToken: null,
            IsRoot: true,
            Depth: 0,
            Unresolved: false);
        return new DependencyGraphResult(
            [node],
            [],
            new Dictionary<string, GraphNavigationContext>(StringComparer.Ordinal));
    }

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
}
