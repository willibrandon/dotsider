using Dotsider.Core.Analysis.Models;
using System.Xml;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads an ILC dependency-graph DGML file, emitted when publishing a Native AOT project with
/// <c>IlcGenerateDgmlFile</c>. The format is a <c>DirectedGraph</c> document of nodes (id and
/// label) and links (source depends on target, with a reason). Node labels equal the node
/// names an mstat size report stores (<see cref="MstatReader"/>), which is how sizes join to
/// dependency chains.
///
/// Parsing streams the XML — the graphs run to hundreds of thousands of links — and never
/// throws: unreadable files return null, and malformed nodes or links are skipped.
/// </summary>
public static class DgmlReader
{
    /// <summary>
    /// Reads a dependency graph from a file.
    /// </summary>
    /// <param name="filePath">The path of the <c>.dgml.xml</c> file.</param>
    /// <returns>The graph, or null when the file is missing or is not a DGML document.</returns>
    public static DgmlGraph? Read(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Read(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a dependency graph from a stream. The stream is left open.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the start of the document.</param>
    /// <returns>The graph, or null when the content is not a DGML document.</returns>
    public static DgmlGraph? Read(Stream stream)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                CloseInput = false,
            };
            using var reader = XmlReader.Create(stream, settings);

            // The root element must be DirectedGraph; namespace is accepted but not required.
            if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "DirectedGraph")
                return null;

            var nodes = new List<DgmlNode>();
            var links = new List<DgmlLink>();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                if (reader.LocalName == "Node")
                {
                    if (int.TryParse(reader.GetAttribute("Id"), out var id))
                        nodes.Add(new DgmlNode(id, reader.GetAttribute("Label") ?? ""));
                }
                else if (reader.LocalName == "Link")
                {
                    if (int.TryParse(reader.GetAttribute("Source"), out var source)
                        && int.TryParse(reader.GetAttribute("Target"), out var target))
                    {
                        var reason = reader.GetAttribute("Reason");
                        links.Add(new DgmlLink(source, target,
                            string.IsNullOrEmpty(reason) ? null : reason));
                    }
                }
            }

            return new DgmlGraph(nodes, links);
        }
        catch (XmlException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
