using System.CommandLine;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;

namespace Dotsider.Commands;

/// <summary>
/// Headless assembly analysis command: types, methods, IL, deps, strings, size.
/// </summary>
internal static class AnalyzeCommand
{
    private static readonly Argument<FileInfo> s_fileArg = new("file")
    {
        Description = "Assembly file to analyze (.dll or .exe)"
    };

    private static readonly Option<bool> s_typesOption = new("--types")
    {
        Description = "List type definitions"
    };

    private static readonly Option<bool> s_methodsOption = new("--methods")
    {
        Description = "List method definitions"
    };

    private static readonly Option<string?> s_ilOption = new("--il")
    {
        Description = "Disassemble a method (format: Type.Method)"
    };

    private static readonly Option<bool> s_depsOption = new("--deps")
    {
        Description = "Show assembly references and dependency graph"
    };

    private static readonly Option<bool> s_stringsOption = new("--strings")
    {
        Description = "Extract strings from the assembly"
    };

    private static readonly Option<bool> s_sizeOption = new("--size")
    {
        Description = "Show size breakdown"
    };

    private static readonly Option<string?> s_outputOption = new("--output", "-o")
    {
        Description = "Write output to a file instead of stdout"
    };

    /// <summary>
    /// Creates the "analyze" command with options for types, methods, IL, deps, strings, and size.
    /// </summary>
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command("analyze", "Headless assembly analysis")
        {
            s_fileArg,
            s_typesOption,
            s_methodsOption,
            s_ilOption,
            s_depsOption,
            s_stringsOption,
            s_sizeOption,
            s_outputOption
        };

        command.SetAction((parseResult, _) =>
        {
            var file = parseResult.GetValue(s_fileArg)!;
            var json = parseResult.GetValue(jsonOption);
            var outputPath = parseResult.GetValue(s_outputOption);

            if (!file.Exists)
            {
                Console.Error.WriteLine($"Error: File not found: {file.FullName}");
                return Task.FromResult(1);
            }

            try
            {
                var filePath = file.FullName;
                var originalPath = filePath;

                var result = AssemblyLoader.Open(filePath);
                AssemblyAnalyzer analyzer;
                string analyzedPath;
                switch (result)
                {
                    case AssemblyOpenResult.ApphostWithCompanion(var host, var companion):
                        host.Dispose();
                        Console.Error.WriteLine(
                            $"Note: {file.Name} is a native apphost. "
                            + $"Analyzing {Path.GetFileName(companion)} instead.");
                        analyzer = new AssemblyAnalyzer(companion);
                        analyzedPath = companion;
                        break;
                    case AssemblyOpenResult.BundleEntry(var entry, var bundle):
                        Console.Error.WriteLine(
                            $"Note: {file.Name} is a single-file bundle. "
                            + $"Analyzing entry assembly {entry.FileName} instead.");
                        analyzer = entry;
                        analyzedPath = bundle;
                        break;
                    default:
                        analyzer = ((AssemblyOpenResult.Direct)result).Analyzer;
                        analyzedPath = filePath;
                        break;
                }

                // Output-path collision check — reject if -o matches EITHER the original
                // input path OR the resolved analyzed path, so neither can be clobbered
                if (outputPath is not null)
                {
                    var outputFull = Path.GetFullPath(outputPath);
                    if (string.Equals(Path.GetFullPath(analyzedPath), outputFull,
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetFullPath(originalPath), outputFull,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine("Error: Output path cannot be the same as the input file");
                        analyzer.Dispose();
                        return Task.FromResult(1);
                    }
                }

                using var analyzerScope = analyzer;
                var disassembler = analyzer.HasMetadata ? new IlDisassembler(analyzer) : null;

                // Defer opening the output file until we know the input is valid
                using var formatter = new OutputFormatter(outputPath) { JsonMode = json };

                if (parseResult.GetValue(s_typesOption))
                    return Task.FromResult(PrintTypes(analyzer, formatter));

                if (parseResult.GetValue(s_methodsOption))
                    return Task.FromResult(PrintMethods(analyzer, formatter));

                if (parseResult.GetValue(s_ilOption) is { } ilTarget)
                {
                    if (disassembler is null)
                    {
                        Console.Error.WriteLine("Error: --il requires a .NET assembly with metadata");
                        return Task.FromResult(1);
                    }
                    
                    return Task.FromResult(PrintIl(analyzer, disassembler, ilTarget, formatter));
                }

                if (parseResult.GetValue(s_depsOption))
                    return Task.FromResult(PrintDeps(analyzer, formatter));

                if (parseResult.GetValue(s_stringsOption))
                    return Task.FromResult(PrintStrings(analyzer, formatter));

                if (parseResult.GetValue(s_sizeOption))
                    return Task.FromResult(PrintSize(analyzer, formatter));

                // Default: show assembly info
                return Task.FromResult(PrintAssemblyInfo(analyzer, formatter));
            }
            catch (Exception ex) when (
                ex is BadImageFormatException or IOException
                    or UnauthorizedAccessException or ArgumentException
                    or PathTooLongException or NotSupportedException)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return Task.FromResult(1);
            }
        });

        return command;
    }

    private static int PrintAssemblyInfo(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(new
            {
                a.FilePath, a.FileName, a.FileSize, a.AssemblyName, a.AssemblyVersion,
                a.TargetFramework, a.Architecture, a.HasMetadata,
                Types = a.TypeDefs, Methods = a.MethodDefs, References = a.AssemblyRefs
            });
        }
        else
        {
            fmt.WriteLine($"File:       {a.FileName}");
            fmt.WriteLine($"Size:       {DotsiderState.FormatSize(a.FileSize)}");
            fmt.WriteLine($"Assembly:   {a.AssemblyName ?? "(none)"}");
            fmt.WriteLine($"Version:    {a.AssemblyVersion ?? "(none)"}");
            fmt.WriteLine($"Framework:  {a.TargetFramework ?? "(none)"}");
            fmt.WriteLine($"Arch:       {a.Architecture}");
            fmt.WriteLine("");

            fmt.WriteLine($"Types ({a.TypeDefs.Count}):");
            foreach (var t in a.TypeDefs)
                fmt.WriteLine($"  {t.FullName}");
            fmt.WriteLine("");

            fmt.WriteLine($"Methods ({a.MethodDefs.Count}):");
            foreach (var m in a.MethodDefs)
                fmt.WriteLine($"  {m.DeclaringType}.{m.Name}{m.Signature}");
            fmt.WriteLine("");

            fmt.WriteLine($"References ({a.AssemblyRefs.Count}):");
            foreach (var r in a.AssemblyRefs)
                fmt.WriteLine($"  {r.Name} {r.Version}");
        }

        return 0;
    }

    private static int PrintTypes(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(a.TypeDefs);
            return 0;
        }

        fmt.WriteTable(
            ["Namespace", "Name", "Base Type", "Methods"],
            a.TypeDefs.Select(t => new[]
            {
                t.Namespace ?? "",
                t.Name,
                t.BaseType ?? "",
                t.MethodCount.ToString()
            }));

        return 0;
    }

    private static int PrintMethods(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(a.MethodDefs);
            return 0;
        }

        fmt.WriteTable(
            ["Type", "Name", "Signature"],
            a.MethodDefs.Select(m => new[]
            {
                m.DeclaringType,
                m.Name,
                m.Signature
            }));

        return 0;
    }

    private static int PrintIl(
        AssemblyAnalyzer a, IlDisassembler dis, string target, OutputFormatter fmt)
    {
        // Parse "Type.Method" or "Type::Method"
        var sep = target.Contains("::") ? "::" : ".";
        var lastDot = target.LastIndexOf(sep, StringComparison.Ordinal);
        if (lastDot < 0)
        {
            OutputFormatter.WriteError($"Error: Invalid method format '{target}'. Use Type.Method or Type::Method");
            return 1;
        }

        var typeName = target[..lastDot];
        var methodName = target[(lastDot + sep.Length)..];

        var method = a.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType.EndsWith(typeName, StringComparison.OrdinalIgnoreCase)
            && m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

        if (method is null)
        {
            OutputFormatter.WriteError($"Error: Method not found: {target}");
            return 1;
        }

        var instructions = dis.Disassemble(method);

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new { Method = method, Instructions = instructions });
            return 0;
        }

        fmt.WriteLine($"// {method.DeclaringType}.{method.Name}{method.Signature}");
        fmt.WriteLine($"// IL size: {instructions.Count} instructions");
        fmt.WriteLine("");

        foreach (var il in instructions)
            fmt.WriteLine($"  IL_{il.Offset:X4}: {il.OpCode,-12} {il.Operand}");

        return 0;
    }

    private static int PrintDeps(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            var (nodes, edges) = DependencyGraphBuilder.Build(a);
            fmt.WriteJson(new { a.AssemblyRefs, Graph = new { nodes, edges } });
            return 0;
        }

        fmt.WriteTable(
            ["Name", "Version", "Culture", "PublicKeyToken"],
            a.AssemblyRefs.Select(r => new[]
            {
                r.Name,
                r.Version,
                r.Culture ?? "",
                r.PublicKeyToken ?? ""
            }));

        return 0;
    }

    private static int PrintStrings(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        var extractor = new StringExtractor(a);
        var user = extractor.ExtractUserStrings();
        var metadata = extractor.ExtractMetadataStrings();

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new { UserStrings = user, MetadataStrings = metadata });
            return 0;
        }

        if (user.Count > 0)
        {
            fmt.WriteLine($"User Strings ({user.Count}):");
            foreach (var s in user)
                fmt.WriteLine($"  [{s.Offset:X6}] {s.Value}");
            fmt.WriteLine("");
        }

        if (metadata.Count > 0)
        {
            fmt.WriteLine($"Metadata Strings ({metadata.Count}):");
            foreach (var s in metadata)
                fmt.WriteLine($"  [{s.Offset:X6}] {s.Value}");
        }

        return 0;
    }

    private static int PrintSize(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        var tree = SizeAnalyzer.BuildSizeTree(a);

        if (fmt.JsonMode)
        {
            fmt.WriteJson(tree);
            return 0;
        }

        PrintSizeNode(tree, fmt, indent: 0);
        return 0;
    }

    private static void PrintSizeNode(SizeNode node, OutputFormatter fmt, int indent)
    {
        var prefix = new string(' ', indent * 2);
        fmt.WriteLine($"{prefix}{node.Name}  ({DotsiderState.FormatSize(node.Size)})");

        foreach (var child in node.Children.OrderByDescending(c => c.Size).Take(20))
            PrintSizeNode(child, fmt, indent + 1);
    }
}
