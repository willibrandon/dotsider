using Dotsider.Core.Analysis;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DgmlReader"/> against the real dependency graph published next to the
/// NativeAOT sample, plus synthetic and malformed documents.
/// </summary>
[TestClass]
public class DgmlReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies the fixture's codegen graph parses with consistent nodes and links: every
    /// indexed link endpoint resolves to a node.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixtureDgml_ParsesNodesAndLinks()
    {
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(Samples.NativeAotConsoleDgml!);

        Assert.IsNotNull(graph);
        Assert.IsNotEmpty(graph.Nodes);
        Assert.IsNotEmpty(graph.Links);
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet();
        TestAssert.All(graph.Links, l =>
        {
            Assert.Contains(l.SourceId, ids);
            Assert.Contains(l.TargetId, ids);
        });
    }

    /// <summary>
    /// Verifies a chain from a real method node reaches a root: the walk ends at a node with
    /// no dependers, starts the returned list with it, and ends the list at the queried node.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PathToRoot_KnownMethodLabel_ReachesRoot()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(Samples.NativeAotConsoleDgml!);
        var data = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(graph);
        Assert.IsNotNull(data);

        // Any compiled method that is present in the graph will do; take the first join hit.
        var nodeName = data.Methods
            .Select(m => m.NodeName)
            .FirstOrDefault(n => n is not null && graph.FindNodeByLabel(n) is not null);
        Assert.IsNotNull(nodeName);

        var path = graph.PathToRoot(nodeName!);

        Assert.IsNotEmpty(path);
        Assert.AreEqual(nodeName, path[^1].Label);
        Assert.IsNull(path[0].Reason);
        var rootNode = graph.FindNodeByLabel(path[0].Label);
        Assert.IsNotNull(rootNode);
        Assert.DoesNotContain(l => l.TargetId == rootNode.Id, graph.Links);
    }

    /// <summary>
    /// Verifies an unknown label yields an empty chain rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PathToRoot_UnknownLabel_ReturnsEmpty()
    {
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(Samples.NativeAotConsoleDgml!);

        Assert.IsNotNull(graph);
        Assert.IsEmpty(graph.PathToRoot("no-such-node-anywhere"));
    }

    /// <summary>
    /// Verifies querying a root returns the single-step chain: the root explains itself.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PathToRoot_RootNode_ReturnsSingleStep()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
              <Nodes>
                <Node Id="0" Label="root"/>
                <Node Id="1" Label="leaf"/>
              </Nodes>
              <Links>
                <Link Source="0" Target="1" Reason="kept"/>
              </Links>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        var path = graph.PathToRoot("root");
        Assert.ContainsSingle(path);
        Assert.AreEqual("root", path[0].Label);
    }

    /// <summary>
    /// Verifies a multi-step synthetic chain reconstructs root-first with per-step reasons.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PathToRoot_SyntheticChain_ReturnsRootFirstWithReasons()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
              <Nodes>
                <Node Id="10" Label="Main method"/>
                <Node Id="20" Label="Helper"/>
                <Node Id="30" Label="Leaf"/>
              </Nodes>
              <Links>
                <Link Source="10" Target="20" Reason="call"/>
                <Link Source="20" Target="30" Reason="field access"/>
              </Links>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        var path = graph.PathToRoot("Leaf");

        Assert.HasCount(3, path);
        Assert.AreEqual(("Main method", (string?)null), (path[0].Label, path[0].Reason));
        Assert.AreEqual(("Helper", "call"), (path[1].Label, path[1].Reason));
        Assert.AreEqual(("Leaf", "field access"), (path[2].Label, path[2].Reason));
    }

    /// <summary>
    /// Verifies a cycle with no root reports the queried node alone instead of spinning.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PathToRoot_PureCycle_ReturnsQueriedNodeAlone()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph>
              <Nodes>
                <Node Id="1" Label="a"/>
                <Node Id="2" Label="b"/>
              </Nodes>
              <Links>
                <Link Source="1" Target="2"/>
                <Link Source="2" Target="1"/>
              </Links>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        var path = graph.PathToRoot("a");
        Assert.ContainsSingle(path);
        Assert.AreEqual("a", path[0].Label);
    }

    /// <summary>
    /// Verifies a document without the dgml namespace still parses — matching is by local
    /// element name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NoNamespace_StillParses()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph>
              <Nodes><Node Id="1" Label="only"/></Nodes>
              <Links/>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        Assert.ContainsSingle(graph.Nodes);
        Assert.IsEmpty(graph.Links);
    }

    /// <summary>
    /// Verifies duplicate labels resolve to the first node without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_DuplicateLabels_FirstWins()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph>
              <Nodes>
                <Node Id="1" Label="dup"/>
                <Node Id="2" Label="dup"/>
              </Nodes>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        var node = graph.FindNodeByLabel("dup");
        Assert.IsNotNull(node);
        Assert.AreEqual(1, node.Id);
    }

    /// <summary>
    /// Verifies malformed nodes and links are skipped while well-formed siblings survive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_MalformedEntries_AreSkipped()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph>
              <Nodes>
                <Node Id="notanint" Label="bad"/>
                <Node Id="1" Label="good"/>
              </Nodes>
              <Links>
                <Link Source="1" Target="nope"/>
                <Link Source="1" Target="1"/>
              </Links>
            </DirectedGraph>
            """));

        Assert.IsNotNull(graph);
        Assert.ContainsSingle(graph.Nodes);
        Assert.ContainsSingle(graph.Links);
    }

    /// <summary>
    /// Verifies a truncated document returns null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_TruncatedXml_ReturnsNull()
    {
        Assert.IsNull(DgmlReader.Read(ToStream("<DirectedGraph><Nodes><Node Id=\"1\"")));
    }

    /// <summary>
    /// Verifies a document with the wrong root element returns null.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_WrongRootElement_ReturnsNull()
    {
        Assert.IsNull(DgmlReader.Read(ToStream("<html><body/></html>")));
    }

    /// <summary>
    /// Verifies an empty file returns null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_EmptyFile_ReturnsNull()
    {
        Assert.IsNull(DgmlReader.Read(ToStream("")));
    }

    /// <summary>
    /// Verifies a missing file returns null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.IsNull(DgmlReader.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dgml.xml")));
    }

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
