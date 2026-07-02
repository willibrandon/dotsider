using System.Text;
using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DgmlReader"/> against the real dependency graph published next to the
/// NativeAOT sample, plus synthetic and malformed documents.
/// </summary>
[Collection("SampleAssemblies")]
public class DgmlReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies the fixture's codegen graph parses with consistent nodes and links: every
    /// indexed link endpoint resolves to a node.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Read_FixtureDgml_ParsesNodesAndLinks()
    {
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(samples.NativeAotConsoleDgml!);

        Assert.NotNull(graph);
        Assert.NotEmpty(graph.Nodes);
        Assert.NotEmpty(graph.Links);
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet();
        Assert.All(graph.Links, l =>
        {
            Assert.Contains(l.SourceId, ids);
            Assert.Contains(l.TargetId, ids);
        });
    }

    /// <summary>
    /// Verifies a chain from a real method node reaches a root: the walk ends at a node with
    /// no dependers, starts the returned list with it, and ends the list at the queried node.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void PathToRoot_KnownMethodLabel_ReachesRoot()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(samples.NativeAotConsoleDgml!);
        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(graph);
        Assert.NotNull(data);

        // Any compiled method that is present in the graph will do; take the first join hit.
        var nodeName = data.Methods
            .Select(m => m.NodeName)
            .FirstOrDefault(n => n is not null && graph.FindNodeByLabel(n) is not null);
        Assert.NotNull(nodeName);

        var path = graph.PathToRoot(nodeName!);

        Assert.NotEmpty(path);
        Assert.Equal(nodeName, path[^1].Label);
        Assert.Null(path[0].Reason);
        var rootNode = graph.FindNodeByLabel(path[0].Label);
        Assert.NotNull(rootNode);
        Assert.DoesNotContain(graph.Links, l => l.TargetId == rootNode.Id);
    }

    /// <summary>
    /// Verifies an unknown label yields an empty chain rather than throwing.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void PathToRoot_UnknownLabel_ReturnsEmpty()
    {
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        var graph = DgmlReader.Read(samples.NativeAotConsoleDgml!);

        Assert.NotNull(graph);
        Assert.Empty(graph.PathToRoot("no-such-node-anywhere"));
    }

    /// <summary>
    /// Verifies querying a root returns the single-step chain: the root explains itself.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

        Assert.NotNull(graph);
        var path = graph.PathToRoot("root");
        Assert.Single(path);
        Assert.Equal("root", path[0].Label);
    }

    /// <summary>
    /// Verifies a multi-step synthetic chain reconstructs root-first with per-step reasons.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

        Assert.NotNull(graph);
        var path = graph.PathToRoot("Leaf");

        Assert.Equal(3, path.Count);
        Assert.Equal(("Main method", (string?)null), (path[0].Label, path[0].Reason));
        Assert.Equal(("Helper", "call"), (path[1].Label, path[1].Reason));
        Assert.Equal(("Leaf", "field access"), (path[2].Label, path[2].Reason));
    }

    /// <summary>
    /// Verifies a cycle with no root reports the queried node alone instead of spinning.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

        Assert.NotNull(graph);
        var path = graph.PathToRoot("a");
        Assert.Single(path);
        Assert.Equal("a", path[0].Label);
    }

    /// <summary>
    /// Verifies a document without the dgml namespace still parses — matching is by local
    /// element name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NoNamespace_StillParses()
    {
        var graph = DgmlReader.Read(ToStream("""
            <DirectedGraph>
              <Nodes><Node Id="1" Label="only"/></Nodes>
              <Links/>
            </DirectedGraph>
            """));

        Assert.NotNull(graph);
        Assert.Single(graph.Nodes);
        Assert.Empty(graph.Links);
    }

    /// <summary>
    /// Verifies duplicate labels resolve to the first node without throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

        Assert.NotNull(graph);
        Assert.Equal(1, graph.FindNodeByLabel("dup")?.Id);
    }

    /// <summary>
    /// Verifies malformed nodes and links are skipped while well-formed siblings survive.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

        Assert.NotNull(graph);
        Assert.Single(graph.Nodes);
        Assert.Single(graph.Links);
    }

    /// <summary>
    /// Verifies a truncated document returns null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_TruncatedXml_ReturnsNull()
    {
        Assert.Null(DgmlReader.Read(ToStream("<DirectedGraph><Nodes><Node Id=\"1\"")));
    }

    /// <summary>
    /// Verifies a document with the wrong root element returns null.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_WrongRootElement_ReturnsNull()
    {
        Assert.Null(DgmlReader.Read(ToStream("<html><body/></html>")));
    }

    /// <summary>
    /// Verifies an empty file returns null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_EmptyFile_ReturnsNull()
    {
        Assert.Null(DgmlReader.Read(ToStream("")));
    }

    /// <summary>
    /// Verifies a missing file returns null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.Null(DgmlReader.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dgml.xml")));
    }

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
