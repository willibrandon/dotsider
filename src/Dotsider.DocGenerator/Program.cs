using Docfx.Dotnet;
using Dotsider.DocGenerator;

var projectRoot = FindProjectRoot();
var outputDir = Path.Combine(projectRoot, "docs", "src", "content", "docs", "api");
var docGeneratorDir = Path.Combine(projectRoot, "src", "Dotsider.DocGenerator");
var yamlOutputDir = Path.Combine(docGeneratorDir, "_metadata");
var docfxJsonPath = Path.Combine(docGeneratorDir, "docfx.json");

Console.WriteLine($"Project root: {projectRoot}");
Console.WriteLine($"Output directory: {outputDir}");

// Clean stale metadata before regenerating
if (Directory.Exists(yamlOutputDir))
    Directory.Delete(yamlOutputDir, recursive: true);
Directory.CreateDirectory(yamlOutputDir);

Console.WriteLine("Generating API metadata with DocFX...");

if (!File.Exists(docfxJsonPath))
{
    Console.Error.WriteLine($"docfx.json not found at {docfxJsonPath}");
    Environment.Exit(1);
}

await DotnetApiCatalog.GenerateManagedReferenceYamlFiles(docfxJsonPath);

Console.WriteLine("Converting to Starlight markdown...");

// Preserve hand-written index
var indexPath = Path.Combine(outputDir, "index.md");
string? existingIndex = null;
if (File.Exists(indexPath))
    existingIndex = await File.ReadAllTextAsync(indexPath);

// Clean generated files only
if (Directory.Exists(outputDir))
{
    foreach (var file in Directory.GetFiles(outputDir, "*.md"))
    {
        var fileName = Path.GetFileName(file);
        if (fileName != "index.md")
            File.Delete(file);
    }
}
else
{
    Directory.CreateDirectory(outputDir);
}

if (existingIndex != null)
    await File.WriteAllTextAsync(indexPath, existingIndex);

var converter = new YamlToMarkdownConverter(yamlOutputDir, outputDir);
await converter.ConvertAllAsync();

Console.WriteLine("Done!");

static string FindProjectRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir, "docs")) &&
            Directory.Exists(Path.Combine(dir, "src", "Dotsider.Core")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    throw new InvalidOperationException(
        "Could not find project root (looking for docs/ and src/Dotsider.Core/)");
}
