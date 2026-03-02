using System.Reflection;
using ComplexApp.Pipeline;

// Load and display embedded resource
var assembly = Assembly.GetExecutingAssembly();
using var stream = assembly.GetManifestResourceStream("ComplexApp.Resources.banner.txt");
if (stream is not null)
{
    using var reader = new StreamReader(stream);
    Console.WriteLine(reader.ReadToEnd());
}

// Run processing pipeline
var pipeline = new ProcessingPipeline<string>();
pipeline.AddStep(new TrimStep());
pipeline.AddStep(new UpperCaseStep());
pipeline.AddStep(new PrefixStep(">>> "));

var inputs = new[] { "  hello world  ", "  dotnet rocks  ", "  dotsider test  " };
foreach (var input in inputs)
{
    var result = await pipeline.ExecuteAsync(input);
    Console.WriteLine(result);
}
