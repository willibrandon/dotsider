using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Dotsider.DocGenerator;

/// <summary>
/// Converts DocFX YAML metadata files to Starlight-compatible markdown.
/// </summary>
/// <param name="yamlDir">Directory containing DocFX YAML metadata files.</param>
/// <param name="outputDir">Directory where generated markdown files will be written.</param>
public partial class YamlToMarkdownConverter(string yamlDir, string outputDir)
{
    private readonly string _yamlDir = yamlDir;
    private readonly string _outputDir = outputDir;
    private readonly Dictionary<string, ApiItem> _items = [];
    private readonly Dictionary<string, int> _namespaceOrder = [];

    /// <summary>
    /// Parses all YAML files and generates Starlight-compatible markdown for each type and namespace.
    /// </summary>
    public async Task ConvertAllAsync()
    {
        foreach (var yamlFile in Directory.GetFiles(_yamlDir, "*.yml"))
        {
            if (Path.GetFileName(yamlFile) == "toc.yml")
                continue;

            var content = await File.ReadAllTextAsync(yamlFile);
            ParseYamlFile(content);
        }

        Console.WriteLine($"Loaded {_items.Count} API items");

        // Build namespace ordering so types stay grouped with their namespace
        // in Starlight's alphabetically-sorted autogenerate sidebar.
        var namespaces = _items.Values
            .Where(i => i.Type == "Namespace")
            .Select(i => i.Uid)
            .Order()
            .ToList();
        for (var i = 0; i < namespaces.Count; i++)
            _namespaceOrder[namespaces[i]] = i;

        var generatedCount = 0;
        foreach (var item in _items.Values)
        {
            if (item.Type == "Namespace" || IsTopLevelType(item.Type))
            {
                var markdown = GenerateMarkdown(item);
                var fileName = SanitizeFileName(item.Uid) + ".md";
                var filePath = Path.Combine(_outputDir, fileName);
                await File.WriteAllTextAsync(filePath, markdown);
                generatedCount++;
            }
        }

        Console.WriteLine($"Generated {generatedCount} markdown files");
    }

    private static bool IsTopLevelType(string? type) =>
        type is "Class" or "Struct" or "Interface" or "Enum" or "Delegate";

    private void ParseYamlFile(string content)
    {
        if (content.StartsWith("### YamlMime:"))
        {
            var firstNewline = content.IndexOf('\n');
            if (firstNewline >= 0)
                content = content[(firstNewline + 1)..];
        }

        using var reader = new StringReader(content);
        var yaml = new YamlStream();

        try
        {
            yaml.Load(reader);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse YAML: {ex.Message}");
            return;
        }

        if (yaml.Documents.Count == 0) return;

        if (yaml.Documents[0].RootNode is not YamlMappingNode root) return;

        if (root.Children.TryGetValue(new YamlScalarNode("items"), out var itemsNode) &&
            itemsNode is YamlSequenceNode items)
        {
            foreach (var itemNode in items.OfType<YamlMappingNode>())
            {
                var apiItem = ParseApiItem(itemNode);
                if (apiItem != null)
                    _items[apiItem.Uid] = apiItem;
            }
        }
    }

    private static ApiItem? ParseApiItem(YamlMappingNode node)
    {
        var item = new ApiItem();

        foreach (var (key, value) in node.Children)
        {
            var keyStr = (key as YamlScalarNode)?.Value;
            if (keyStr == null) continue;

            switch (keyStr)
            {
                case "uid":
                    item.Uid = GetScalarValue(value) ?? "";
                    break;
                case "name":
                    item.Name = GetScalarValue(value);
                    break;
                case "nameWithType":
                    item.NameWithType = GetScalarValue(value);
                    break;
                case "fullName":
                    item.FullName = GetScalarValue(value);
                    break;
                case "type":
                    item.Type = GetScalarValue(value);
                    break;
                case "namespace":
                    item.Namespace = GetScalarValue(value);
                    break;
                case "summary":
                    item.Summary = GetScalarValue(value);
                    break;
                case "remarks":
                    item.Remarks = GetScalarValue(value);
                    break;
                case "example":
                    if (value is YamlSequenceNode exampleSeq)
                    {
                        var examples = exampleSeq.OfType<YamlScalarNode>()
                            .Select(n => n.Value ?? "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                        item.Example = string.Join("\n\n", examples);
                    }
                    break;
                case "parent":
                    item.Parent = GetScalarValue(value);
                    break;
                case "syntax":
                    ParseSyntax(value, item);
                    break;
                case "children":
                    if (value is YamlSequenceNode childrenSeq)
                    {
                        foreach (var child in childrenSeq.OfType<YamlScalarNode>())
                            item.Children.Add(child.Value ?? "");
                    }
                    break;
                case "inheritance":
                    if (value is YamlSequenceNode inheritanceSeq)
                    {
                        foreach (var inh in inheritanceSeq.OfType<YamlScalarNode>())
                            item.Inheritance.Add(inh.Value ?? "");
                    }
                    break;
                case "implements":
                    if (value is YamlSequenceNode implementsSeq)
                    {
                        foreach (var impl in implementsSeq.OfType<YamlScalarNode>())
                            item.Implements.Add(impl.Value ?? "");
                    }
                    break;
            }
        }

        return string.IsNullOrEmpty(item.Uid) ? null : item;
    }

    private static void ParseSyntax(YamlNode value, ApiItem item)
    {
        if (value is not YamlMappingNode syntaxNode) return;

        foreach (var (sKey, sVal) in syntaxNode.Children)
        {
            var sKeyStr = (sKey as YamlScalarNode)?.Value;
            if (sKeyStr == "content")
            {
                item.SyntaxContent = GetScalarValue(sVal);
            }
            else if (sKeyStr == "parameters" && sVal is YamlSequenceNode paramsSeq)
            {
                foreach (var param in paramsSeq.OfType<YamlMappingNode>())
                {
                    var paramItem = new ParameterItem();
                    foreach (var (pKey, pVal) in param.Children)
                    {
                        var pKeyStr = (pKey as YamlScalarNode)?.Value;
                        if (pKeyStr == "id")
                            paramItem.Id = GetScalarValue(pVal);
                        else if (pKeyStr == "type")
                            paramItem.Type = GetScalarValue(pVal);
                        else if (pKeyStr == "description")
                            paramItem.Description = GetScalarValue(pVal);
                    }
                    item.Parameters.Add(paramItem);
                }
            }
            else if (sKeyStr == "return" && sVal is YamlMappingNode returnNode)
            {
                foreach (var (rKey, rVal) in returnNode.Children)
                {
                    var rKeyStr = (rKey as YamlScalarNode)?.Value;
                    if (rKeyStr == "type")
                        item.ReturnType = GetScalarValue(rVal);
                    else if (rKeyStr == "description")
                        item.ReturnDescription = GetScalarValue(rVal);
                }
            }
        }
    }

    private static string? GetScalarValue(YamlNode node) =>
        (node as YamlScalarNode)?.Value;

    private string GenerateMarkdown(ApiItem item)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(item.Name ?? item.Uid)}\"");
        if (!string.IsNullOrEmpty(item.Summary))
            sb.AppendLine($"description: \"{EscapeYaml(CleanSummary(item.Summary) ?? "")}\"");
        sb.AppendLine($"slug: api/{SlugifyUid(item.Uid)}");

        var nsKey = item.Type == "Namespace" ? item.Uid : item.Namespace ?? "";
        if (_namespaceOrder.TryGetValue(nsKey, out var sidebarOrder))
        {
            sb.AppendLine("sidebar:");
            sb.AppendLine($"  order: {sidebarOrder}");
            if (item.Type == "Namespace")
            {
                sb.AppendLine("  attrs:");
                sb.AppendLine("    data-api-namespace: \"true\"");
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(item.Namespace))
        {
            sb.AppendLine($"**Namespace:** `{item.Namespace}`");
            sb.AppendLine();
        }

        if (item.Type != "Namespace")
        {
            sb.AppendLine("**Assembly:** Dotsider.Core.dll");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(item.Summary))
        {
            sb.AppendLine(ConvertXmlToMarkdown(item.Summary));
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(item.SyntaxContent))
        {
            sb.AppendLine("```csharp");
            sb.AppendLine(item.SyntaxContent);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (item.Inheritance.Count > 0)
        {
            sb.AppendLine("## Inheritance");
            sb.AppendLine();
            var chain = item.Inheritance.Select(FormatTypeLink).ToList();
            chain.Add($"**{EscapeGenerics(item.Name ?? item.Uid)}**");
            sb.AppendLine(string.Join(" → ", chain));
            sb.AppendLine();
        }

        if (item.Implements.Count > 0)
        {
            sb.AppendLine("## Implements");
            sb.AppendLine();
            foreach (var impl in item.Implements)
                sb.AppendLine($"- {FormatTypeLink(impl)}");
            sb.AppendLine();
        }

        if (item.Children.Count > 0)
        {
            var childItems = item.Children
                .Select(uid => _items.GetValueOrDefault(uid))
                .Where(c => c != null)
                .Cast<ApiItem>()
                .GroupBy(c => c.Type)
                .OrderBy(g => GetTypeOrder(g.Key));

            foreach (var group in childItems)
            {
                sb.AppendLine($"## {GetSectionTitle(group.Key)}");
                sb.AppendLine();

                foreach (var child in group.OrderBy(c => c.Name))
                {
                    if (IsTopLevelType(child.Type))
                        sb.AppendLine($"### [{EscapeGenerics(child.Name ?? child.Uid)}](/api/{SlugifyUid(child.Uid)}/)");
                    else
                        sb.AppendLine($"### {EscapeGenerics(child.Name ?? child.Uid)}");
                    sb.AppendLine();

                    if (!string.IsNullOrEmpty(child.Summary))
                    {
                        sb.AppendLine(ConvertXmlToMarkdown(child.Summary));
                        sb.AppendLine();
                    }

                    if (child.Parameters.Count > 0)
                    {
                        sb.AppendLine("**Parameters:**");
                        sb.AppendLine();
                        foreach (var param in child.Parameters)
                        {
                            var typeLink = !string.IsNullOrEmpty(param.Type) ? FormatTypeLink(param.Type) : "";
                            sb.AppendLine($"- `{param.Id}` ({typeLink}): {ConvertXmlToMarkdown(param.Description ?? "")}");
                        }
                        sb.AppendLine();
                    }

                    if (!string.IsNullOrEmpty(child.ReturnType))
                    {
                        sb.AppendLine($"**Returns:** {FormatTypeLink(child.ReturnType)}");
                        if (!string.IsNullOrEmpty(child.ReturnDescription))
                        {
                            sb.AppendLine();
                            sb.AppendLine(ConvertXmlToMarkdown(child.ReturnDescription));
                        }
                        sb.AppendLine();
                    }

                    if (!string.IsNullOrEmpty(child.SyntaxContent))
                    {
                        sb.AppendLine("```csharp");
                        sb.AppendLine(child.SyntaxContent);
                        sb.AppendLine("```");
                        sb.AppendLine();
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(item.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine();
            sb.AppendLine(ConvertXmlToMarkdown(item.Remarks));
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(item.Example))
        {
            sb.AppendLine("## Examples");
            sb.AppendLine();
            sb.AppendLine(ConvertXmlToMarkdown(item.Example));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeYaml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");

    private static string EscapeGenerics(string value) =>
        value.Replace("<", "\\<").Replace(">", "\\>");

    private static string? CleanSummary(string? summary)
    {
        if (string.IsNullOrEmpty(summary)) return null;
        var clean = XrefRegex().Replace(summary, m => m.Groups[1].Value.Split('.').Last());
        clean = SeeCrefRegex().Replace(clean, m => m.Groups[1].Value.Split('.').Last());
        clean = ParamRefRegex().Replace(clean, "$1");
        clean = TypeParamRefRegex().Replace(clean, "$1");
        clean = XmlTagRegex().Replace(clean, "");
        clean = WhitespaceRegex().Replace(clean, " ").Trim();
        return clean;
    }

    private string FormatTypeLink(string uid)
    {
        var genericStart = uid.IndexOf('{');
        var baseUid = genericStart >= 0 ? uid[..genericStart] : uid;
        var genericParams = genericStart >= 0 ? uid[genericStart..].Replace('{', '<').Replace('}', '>') : "";

        var baseDisplayName = baseUid.Split('.').Last();

        var formattedGenericParams = "";
        if (!string.IsNullOrEmpty(genericParams))
        {
            var innerTypes = genericParams[1..^1].Split(',');
            var formattedInner = innerTypes.Select(t => t.Trim().Split('.').Last());
            formattedGenericParams = $"<{string.Join(", ", formattedInner)}>";
        }

        var displayName = baseDisplayName + formattedGenericParams;

        if (uid.StartsWith("System.") || uid.StartsWith("Microsoft."))
        {
            var baseType = baseUid.Split('`')[0];
            var msDocsUid = baseType.ToLowerInvariant();
            if (genericStart >= 0)
            {
                var arity = genericParams.Count(c => c == ',') + 1;
                msDocsUid += $"-{arity}";
            }
            return $"[{EscapeGenerics(displayName)}](https://learn.microsoft.com/dotnet/api/{msDocsUid})";
        }

        if (_items.ContainsKey(baseUid))
            return $"[{EscapeGenerics(displayName)}](/api/{SlugifyUid(baseUid)}/)";

        if (_items.ContainsKey(uid))
            return $"[{EscapeGenerics(displayName)}](/api/{SlugifyUid(uid)}/)";

        return EscapeGenerics(displayName);
    }

    private string ConvertXmlToMarkdown(string xml)
    {
        var result = xml;

        result = HtmlCodeBlockRegex().Replace(result, m =>
        {
            var code = m.Groups[1].Value
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"");
            return $"\n```csharp\n{code}\n```\n";
        });

        result = result.Replace("<p>", "\n\n").Replace("</p>", "\n\n");
        result = HtmlPreRegex().Replace(result, "\n```\n$1\n```\n");
        result = XrefRegex().Replace(result, m => FormatTypeLink(m.Groups[1].Value));
        result = SeeCrefRegex().Replace(result, m => FormatTypeLink(m.Groups[1].Value));
        result = ParamRefRegex().Replace(result, "`$1`");
        result = TypeParamRefRegex().Replace(result, "`$1`");
        result = CCodeRegex().Replace(result, "`$1`");
        result = CodeBlockRegex().Replace(result, "`$1`");
        result = XmlTagRegex().Replace(result, "");
        result = MultipleNewlinesRegex().Replace(result, "\n\n");

        return result.Trim();
    }

    private static int GetTypeOrder(string? type) => type switch
    {
        "Class" => 1,
        "Struct" => 2,
        "Interface" => 3,
        "Enum" => 4,
        "Delegate" => 5,
        "Constructor" => 10,
        "Property" => 11,
        "Method" => 12,
        "Field" => 13,
        "Event" => 14,
        _ => 99
    };

    private static string GetSectionTitle(string? type) => type switch
    {
        "Class" => "Classes",
        "Struct" => "Structs",
        "Interface" => "Interfaces",
        "Enum" => "Enums",
        "Delegate" => "Delegates",
        "Constructor" => "Constructors",
        "Property" => "Properties",
        "Method" => "Methods",
        "Field" => "Fields",
        "Event" => "Events",
        _ => "Members"
    };

    private static string SanitizeFileName(string uid) =>
        uid.Replace('.', '-').Replace('`', '-').Replace('<', '-').Replace('>', '-')
           .Replace(',', '-').Replace('{', '-').Replace('}', '-');

    private static string SlugifyUid(string uid) =>
        uid.Replace('`', '-').Replace('<', '-').Replace('>', '-')
           .Replace(',', '-').Replace('{', '-').Replace('}', '-')
           .ToLowerInvariant();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<see\s+cref=""([^""]+)""\s*/>")]
    private static partial Regex SeeCrefRegex();

    [GeneratedRegex(@"<paramref\s+name=""([^""]+)""\s*/>")]
    private static partial Regex ParamRefRegex();

    [GeneratedRegex(@"<typeparamref\s+name=""([^""]+)""\s*/>")]
    private static partial Regex TypeParamRefRegex();

    [GeneratedRegex(@"<c>([^<]+)</c>")]
    private static partial Regex CCodeRegex();

    [GeneratedRegex(@"<code>([^<]+)</code>", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"<pre><code[^>]*>(.+?)</code></pre>", RegexOptions.Singleline)]
    private static partial Regex HtmlCodeBlockRegex();

    [GeneratedRegex(@"<pre>(.+?)</pre>", RegexOptions.Singleline)]
    private static partial Regex HtmlPreRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"<xref\s+href=""([^""]+)""[^>]*/?>(?:</xref>)?")]
    private static partial Regex XrefRegex();
}
