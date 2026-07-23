using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using Dotsider.Infrastructure;
using System.CommandLine;

namespace Dotsider.Commands;

/// <summary>
/// Headless file analysis command: managed metadata, IL, deps, strings, size, native symbols,
/// ReadyToRun correlation, Native AOT sidecars, and raw WebAssembly modules.
/// </summary>
internal static class AnalyzeCommand
{
    private static readonly Argument<FileInfo> s_fileArg = new("file")
    {
        Description = "Assembly or native module to analyze (.dll, .exe, or .wasm)"
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

    private static readonly Option<string?> s_embeddedSourceOption = new("--embedded-source")
    {
        Description = "Print embedded source for a method (format: Type.Method)"
    };

    private static readonly Option<bool> s_depsOption = new("--deps")
    {
        Description = "Show assembly references and dependency graph"
    };

    private static readonly Option<bool> s_stringsOption = new("--strings")
    {
        Description = "Extract strings from the assembly"
    };

    private static readonly Option<int> s_minLenOption = new("--min-len", "-n")
    {
        Description = "Minimum length for raw string extraction (default: 4)",
        DefaultValueFactory = _ => 4
    };

    private static readonly Option<bool> s_sizeOption = new("--size")
    {
        Description = "Show size breakdown"
    };

    private static readonly Option<string?> s_disasmOption = new("--disasm")
    {
        Description = "Disassemble a native function (name or 0xVA)"
    };

    private static readonly Option<bool> s_symbolsOption = new("--symbols")
    {
        Description = "List native symbols (Native AOT and other native binaries)"
    };

    private static readonly Option<string?> s_whyOption = new("--why")
    {
        Description = "Explain why a type or method is in a Native AOT binary (requires mstat and DGML sidecars)"
    };

    private static readonly Option<string?> s_correlateOption = new("--correlate")
    {
        Description = "Correlate a Native AOT binary with its pre-ILC assembly. "
            + "Bare: print correlation counts. With a value (Type.Method or 0xVA): print IL and native code side by side.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Option<string?> s_r2rCorrelateOption = new("--r2r-correlate")
    {
        Description = "Correlate a ReadyToRun image's managed methods with their precompiled native code. "
            + "Bare: print precompiled-method stats. With a value (Type.Method, 0xTOKEN, or 0xVA): print IL and native code together.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Option<bool> s_fieldsOption = new("--fields")
    {
        Description = "List field definitions"
    };

    private static readonly Option<bool> s_bundleOption = new("--bundle")
    {
        Description = "Show single-file bundle manifest"
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
            s_embeddedSourceOption,
            s_depsOption,
            s_stringsOption,
            s_minLenOption,
            s_sizeOption,
            s_symbolsOption,
            s_disasmOption,
            s_whyOption,
            s_correlateOption,
            s_r2rCorrelateOption,
            s_fieldsOption,
            s_bundleOption,
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

            // --bundle short-circuits before assembly loading — inspects the bundle itself
            if (parseResult.GetValue(s_bundleOption))
            {
                if (outputPath is not null
                    && string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(file.FullName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Error: Output path cannot be the same as the input file");
                    return Task.FromResult(1);
                }

                using var formatter = new OutputFormatter(outputPath) { JsonMode = json };
                return Task.FromResult(PrintBundle(file.FullName, formatter));
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
                    case AssemblyOpenResult.NativeAot(var aot):
                        analyzer = aot;
                        analyzedPath = filePath;
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

                if (parseResult.GetValue(s_embeddedSourceOption) is { } embeddedSourceTarget)
                    return Task.FromResult(PrintEmbeddedSource(analyzer, embeddedSourceTarget, formatter));

                if (parseResult.GetValue(s_depsOption))
                    return Task.FromResult(PrintDeps(analyzer, formatter));

                if (parseResult.GetValue(s_stringsOption))
                    return Task.FromResult(PrintStrings(analyzer, formatter,
                        parseResult.GetValue(s_minLenOption)));

                if (parseResult.GetValue(s_sizeOption))
                    return Task.FromResult(PrintSize(analyzer, formatter));

                if (parseResult.GetValue(s_symbolsOption))
                    return Task.FromResult(PrintSymbols(analyzer, formatter));

                if (parseResult.GetValue(s_disasmOption) is { } disasmTarget)
                    return Task.FromResult(PrintDisasm(analyzer, disasmTarget, formatter));

                if (parseResult.GetValue(s_whyOption) is { } whyTarget)
                    return Task.FromResult(PrintWhy(analyzer, whyTarget, formatter));

                if (parseResult.GetResult(s_correlateOption) is not null)
                    return Task.FromResult(PrintCorrelate(
                        analyzer, parseResult.GetValue(s_correlateOption), formatter));

                if (parseResult.GetResult(s_r2rCorrelateOption) is not null)
                    return Task.FromResult(PrintR2rCorrelate(
                        analyzer, parseResult.GetValue(s_r2rCorrelateOption), formatter));

                if (parseResult.GetValue(s_fieldsOption))
                    return Task.FromResult(PrintFields(analyzer, formatter));

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
                a.TargetFramework, a.Architecture, a.HasMetadata, a.BinaryKind, a.NativeAotInfo,
                a.DisplayName, a.IsBundleBacked, a.SourceBundlePath, a.LaunchPath, a.CanSaveInPlace, a.PreferredRuntimePack,
                a.PdbProvenance, a.SourceLink, a.DebugDirectory,
                a.ReadyToRunSections,
                RecoveredTypeCount = a.RecoveredTypes.Count,
                FrozenStringCount = a.FrozenStrings.Count,
                NativeSymbolCount = a.NativeSymbols?.Symbols.Count ?? 0,
                NativeSymbolSource = a.NativeSymbols?.Source,
                NativeSymbolStatus = a.NativeSymbols?.Status,
                a.NativeSymbolsPath,
                PreIlc = BuildPreIlcProbeJson(a),
                ReadyToRun = BuildReadyToRunJson(a),
                Webcil = WebcilPayloadBuilder.BuildSummary(a),
                Wasm = WasmPayloadBuilder.BuildSummary(a),
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
            if (a.NativeAotInfo is { } aot)
            {
                fmt.WriteLine("Kind:       Native AOT (.NET)");
                fmt.WriteLine($"RTR Format: v{aot.MajorVersion}.{aot.MinorVersion} ({aot.SectionCount} sections)");
                fmt.WriteLine($"Runtime:    {aot.RuntimeVersion ?? "(not detected)"}");
                fmt.WriteLine($"Imports:    {a.Imports.Count} modules, "
                    + $"{a.Imports.Sum(m => m.Functions.Count)} functions");
                fmt.WriteLine($"R2R:        {a.ReadyToRunSections.Count} sections");
                fmt.WriteLine($"Recovered:  {a.RecoveredTypes.Count} types, "
                    + $"{a.RecoveredTypes.Sum(t => t.MethodNames.Count)} methods");
                fmt.WriteLine($"Frozen:     {a.FrozenStrings.Count} strings");
                if (a.NativeSymbols is { } symbols)
                {
                    fmt.WriteLine(symbols.Symbols.Count > 0
                        ? $"Symbols:    {symbols.Symbols.Count} from {symbols.Source}"
                        : $"Symbols:    {symbols.Diagnostic ?? symbols.Status.ToString()}");
                }

                if (a.PreIlcSidecars is { } sidecars)
                    WritePreIlcProbeSummary(sidecars, fmt);
            }
            else if (a.ReadyToRunInfo is { } r2r)
            {
                fmt.WriteLine("Kind:       ReadyToRun (.NET)");
                fmt.WriteLine($"R2R Format: v{r2r.MajorVersion}.{r2r.MinorVersion} ({r2r.Status}, {r2r.SectionCount} sections)");
                fmt.WriteLine($"Composite:  {r2r.IsComposite}{(r2r.IsComponent ? " (component)" : "")}{(r2r.IsPartialImage ? ", partial" : "")}");
                if (r2r.OwnerCompositeExecutable is { } owner)
                    fmt.WriteLine($"Owner:      {owner}");
                if (a.ReadyToRunIndex is { } index)
                    fmt.WriteLine($"Precompiled: {index.Methods.Count} methods "
                        + $"({index.InstantiationCount} instantiations), {DotsiderState.FormatSize(index.TotalCodeSize)}");
                if (r2r.Diagnostic is { } diagnostic)
                    fmt.WriteLine($"Note:       {diagnostic}");
            }
            else if (a.WasmModuleInfo is { } wasm)
            {
                fmt.WriteLine("Kind:       WebAssembly (.NET)");
                fmt.WriteLine($"Wasm:       v{wasm.Version}, {wasm.Sections.Count} sections");
                fmt.WriteLine($"Functions:  {wasm.DefinedFunctionCount} defined, {wasm.ImportedFunctionCount} imported");
                fmt.WriteLine($"Code:       {DotsiderState.FormatSize(wasm.CodeSize)}");
                fmt.WriteLine($"Data:       {wasm.DataSegments.Count} segments, {DotsiderState.FormatSize(wasm.DataSize)}");
                fmt.WriteLine($"Imports:    {wasm.Imports.Count} entries");
                fmt.WriteLine($"Exports:    {wasm.Exports.Count} entries");
                fmt.WriteLine($"Symbols:    {wasm.SymbolMapStatus}"
                    + (wasm.SymbolMapEntryCount > 0 ? $", {wasm.SymbolMapEntryCount} names" : ""));
                if (wasm.SymbolMapPath is { } symbolMapPath)
                    fmt.WriteLine($"Symbol Map: {symbolMapPath}");
                if (wasm.Diagnostic is { } diagnostic)
                    fmt.WriteLine($"Note:       {diagnostic}");
            }
            else if (a.WebcilInfo is { } webcil)
            {
                fmt.WriteLine("Kind:       Managed");
                fmt.WriteLine($"Webcil:     v{webcil.VersionMajor}.{webcil.VersionMinor}"
                    + (webcil.IsWasmWrapped ? " (Wasm-wrapped)" : ""));
                fmt.WriteLine($"Webcil:     {webcil.SectionCount} sections, "
                    + $"{DotsiderState.FormatSize(webcil.MetadataSize)} metadata");
            }
            fmt.WriteLine($"PDB:        {a.PdbProvenance}");
            fmt.WriteLine($"SourceLink: {(a.SourceLink.IsPresent ? $"present, {a.SourceLink.Mappings.Count} mappings" : "not present")}");
            if (a.IsBundleBacked)
            {
                fmt.WriteLine($"Display:    {a.DisplayName} (from bundle)");
                fmt.WriteLine($"Bundle:     {a.SourceBundlePath}");
            }
            fmt.WriteLine($"Runtime Pack: {a.PreferredRuntimePack}");
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
        // A Native AOT binary has no metadata TypeDefs; fall back to the types recovered
        // from its embedded NativeFormat metadata so --types still describes it.
        if (!a.HasMetadata && a.RecoveredTypes.Count > 0)
            return PrintRecoveredTypes(a, fmt);

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

    private static int PrintRecoveredTypes(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(a.RecoveredTypes);
            return 0;
        }

        fmt.WriteTable(
            ["Type", "Methods"],
            a.RecoveredTypes.Select(t => new[] { t.FullName, t.MethodNames.Count.ToString() }));

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
            ["Namespace", "Type", "Name", "Signature"],
            a.MethodDefs.Select(m =>
            {
                var lastDot = m.DeclaringType.LastIndexOf('.');
                var ns = lastDot >= 0 ? m.DeclaringType[..lastDot] : "";
                var typeName = lastDot >= 0 ? m.DeclaringType[(lastDot + 1)..] : m.DeclaringType;
                return new[] { ns, typeName, m.Name, m.Signature };
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
            fmt.WriteJson(new
            {
                Method = method,
                Pdb = a.PdbProvenance,
                a.SourceLink,
                DebugInfo = a.GetMethodDebugInfo(method),
                Instructions = instructions
            });
            return 0;
        }

        fmt.WriteLine(dis.FormatDisassembly(method));

        return 0;
    }

    private static int PrintDisasm(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        // A ReadyToRun method's body is several code ranges that share one managed name; resolve to the
        // method and render every range, rather than treating each range as a separate ("ambiguous")
        // symbol. This routes through the same query the correlate surface uses.
        if (a.IsReadyToRun)
            return PrintReadyToRunDisasm(a, target, fmt);

        var info = a.NativeSymbols;
        if (info is null || info.Symbols.Count == 0)
        {
            OutputFormatter.WriteError("Error: --disasm requires a binary with native symbols");
            return 1;
        }

        var matches = NativeDisassembler.FindExecutableSymbols(info, target);

        if (matches.Count == 0)
        {
            OutputFormatter.WriteError($"Error: No native symbol matches '{target}'");
            return 1;
        }

        if (matches.Count > 1)
        {
            OutputFormatter.WriteError($"Error: '{target}' is ambiguous ({matches.Count} matches):");
            foreach (var m in matches.OrderBy(m => m.VirtualAddress))
                OutputFormatter.WriteError($"  0x{m.VirtualAddress:x}  {m.ManagedName ?? m.Name}");
            return 2;
        }

        var symbol = matches[0];
        var result = NativeDisassembler.DisassembleSymbol(a, symbol);
        if (result is null)
        {
            OutputFormatter.WriteError($"Error: '{symbol.ManagedName ?? symbol.Name}' has no disassemblable bytes");
            return 1;
        }

        var (text, instructions, _) = result.Value;
        if (fmt.JsonMode)
        {
            fmt.WriteJson(new { Symbol = symbol.ManagedName ?? symbol.Name, a.Architecture, Instructions = instructions });
            return 0;
        }

        fmt.WriteLine(text);
        return 0;
    }

    private static int PrintReadyToRunDisasm(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        var result = ReadyToRunCorrelationQuery.Resolve(a, target, CancellationToken.None);
        switch (result.Outcome)
        {
            case ReadyToRunQueryOutcome.Unavailable:
            case ReadyToRunQueryOutcome.NotFound:
                OutputFormatter.WriteError($"Error: {result.Message}");
                return 1;

            case ReadyToRunQueryOutcome.Ambiguous:
                OutputFormatter.WriteError($"Error: {result.Message}:");
                foreach (var c in result.Candidates)
                    OutputFormatter.WriteError(
                        $"  {c.AssemblyName}  {c.DeclaringType}::{c.Name}  token 0x{c.Token:X8}"
                        + (c.VirtualAddress is { } va ? $"  @ 0x{va:X}" : ""));
                return 2;
        }

        var report = result.Report!;
        if (report.NativeText is not { } native)
        {
            OutputFormatter.WriteError($"Error: '{report.Method}' has no precompiled native code"
                + (report.Diagnostic is { } d ? $" ({d})" : ""));
            return 1;
        }

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new { Symbol = report.Method, a.Architecture, Instructions = report.NativeInstructions });
            return 0;
        }

        fmt.WriteLine(native);
        return 0;
    }

    private static int PrintEmbeddedSource(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        if (!a.HasMetadata)
        {
            OutputFormatter.WriteError("Error: --embedded-source requires a .NET assembly with metadata");
            return 1;
        }

        if (!TryFindMethod(a, target, out var method, out var error))
        {
            OutputFormatter.WriteError(error!);
            return 1;
        }

        var source = a.GetEmbeddedSource(method!);
        if (source is null)
        {
            OutputFormatter.WriteError($"Error: Embedded source not found for {target}");
            return 1;
        }

        if (fmt.JsonMode)
        {
            fmt.WriteJson(source);
            return 0;
        }

        fmt.WriteLine(source.Text);
        return 0;
    }

    private static bool TryFindMethod(
        AssemblyAnalyzer a,
        string target,
        out MethodDefInfo? method,
        out string? error)
    {
        method = null;
        error = null;

        var sep = target.Contains("::") ? "::" : ".";
        var lastDot = target.LastIndexOf(sep, StringComparison.Ordinal);
        if (lastDot < 0)
        {
            error = $"Error: Invalid method format '{target}'. Use Type.Method or Type::Method";
            return false;
        }

        var typeName = target[..lastDot];
        var methodName = target[(lastDot + sep.Length)..];

        method = a.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType.EndsWith(typeName, StringComparison.OrdinalIgnoreCase)
            && m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

        if (method is not null) return true;

        error = $"Error: Method not found: {target}";
        return false;
    }

    private static int PrintDeps(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            var graph = DependencyGraphBuilder.Build(a);
            fmt.WriteJson(new { a.AssemblyRefs, Graph = new { graph.Nodes, graph.Edges } });
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

    private static int PrintStrings(AssemblyAnalyzer a, OutputFormatter fmt, int minLength)
    {
        var extractor = new StringExtractor(a);
        var user = extractor.ExtractUserStrings();
        var metadata = extractor.ExtractMetadataStrings();

        // Metadata-less binaries (Native AOT, apphosts) have no string heaps —
        // fall back to the raw scans so --strings never comes back empty-handed.
        var scanRaw = !a.HasMetadata;
        IReadOnlyList<StringEntry> raw = scanRaw ? extractor.ExtractRawStrings(minLength) : [];
        IReadOnlyList<StringEntry> rawUtf16 = scanRaw ? extractor.ExtractRawUtf16Strings(minLength) : [];
        var frozen = a.FrozenStrings;

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new
            {
                UserStrings = user, MetadataStrings = metadata,
                RawStrings = raw, RawUtf16Strings = rawUtf16, FrozenStrings = frozen
            });
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

        if (raw.Count > 0)
        {
            fmt.WriteLine($"Raw Strings (ASCII) ({raw.Count}):");
            foreach (var s in raw)
                fmt.WriteLine($"  [{s.Offset:X6}] {s.Value}");
            fmt.WriteLine("");
        }

        if (rawUtf16.Count > 0)
        {
            fmt.WriteLine($"Raw Strings (UTF-16) ({rawUtf16.Count}):");
            foreach (var s in rawUtf16)
                fmt.WriteLine($"  [{s.Offset:X6}] {s.Value}");
            fmt.WriteLine("");
        }

        if (frozen.Count > 0)
        {
            fmt.WriteLine($"Frozen Strings (AOT) ({frozen.Count}):");
            foreach (var s in frozen)
                fmt.WriteLine($"  [{s.Offset:X6}] {s.Value}");
        }

        return 0;
    }

    private static int PrintSymbols(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (a.NativeSymbols is not { } info)
        {
            Console.Error.WriteLine("Error: managed assembly; no native symbols to read");
            return 1;
        }

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new
            {
                info.Source, info.Status, info.Path, info.Diagnostic,
                info.Symbols.Count,
                info.Symbols
            });
            return 0;
        }

        fmt.WriteLine($"Source:     {info.Source} ({info.Status})");
        fmt.WriteLine($"Path:       {info.Path ?? "(none)"}");
        if (info.Diagnostic is not null)
            fmt.WriteLine($"Note:       {info.Diagnostic}");
        fmt.WriteLine("");

        fmt.WriteLine($"Symbols ({info.Symbols.Count}):");
        fmt.WriteTable(
            ["Address", "Size", "Kind", "Name", "Source"],
            info.Symbols.Select(s => new[]
            {
                $"0x{s.VirtualAddress:X}",
                s.Size.ToString(),
                s.Kind.ToString(),
                s.ManagedName ?? s.Name,
                s.SourceFile is not null ? $"{s.SourceFile}:{s.Line}" : ""
            }));

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

    private static int PrintWhy(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        if (a.BinaryKind != BinaryKind.NativeAot || a.Mstat is not { } mstat)
        {
            Console.Error.WriteLine(
                "Error: --why requires a Native AOT binary with an mstat sidecar — beside the binary "
                + "or in the build tree (obj\\<cfg>\\<tfm>\\<rid>\\native) — publish with IlcGenerateMstatFile");
            return 1;
        }

        if (a.Dgml is not { } dgml)
        {
            Console.Error.WriteLine(
                "Error: --why requires a DGML sidecar — beside the binary or in the build tree "
                + "(obj\\<cfg>\\<tfm>\\<rid>\\native) — publish with IlcGenerateDgmlFile");
            return 1;
        }

        // Exact display-name match wins; otherwise fall back to a case-insensitive
        // substring search and require it to be unambiguous.
        var candidates = mstat.Methods
            .Where(m => m.NodeName is not null)
            .Select(m => (Display: $"{m.DeclaringType}::{m.Name}", NodeName: m.NodeName!))
            .Concat(mstat.Types
                .Where(t => t.NodeName is not null)
                .Select(t => (Display: t.Name, NodeName: t.NodeName!)))
            .ToList();

        var matches = candidates
            .Where(c => string.Equals(c.Display, target, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
        {
            matches = [.. candidates
                .Where(c => c.Display.Contains(target, StringComparison.OrdinalIgnoreCase))];
        }

        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"Error: no compiled type or method matches '{target}'");
            return 1;
        }

        if (matches.Count > 1)
        {
            Console.Error.WriteLine($"Error: '{target}' is ambiguous ({matches.Count} matches):");
            foreach (var (Display, NodeName) in matches.Take(10))
                Console.Error.WriteLine($"  {Display}");
            if (matches.Count > 10)
                Console.Error.WriteLine($"  ... and {matches.Count - 10} more");
            return 1;
        }

        var (display, nodeName) = matches[0];
        var chain = dgml.PathToRoot(nodeName);
        if (chain.Count == 0)
        {
            Console.Error.WriteLine($"Error: '{display}' is not present in the DGML dependency graph");
            return 1;
        }

        if (fmt.JsonMode)
        {
            fmt.WriteJson(new { Target = display, NodeName = nodeName, Chain = chain });
            return 0;
        }

        fmt.WriteLine($"Why is {display} in the binary? (root first)");
        fmt.WriteLine("");
        for (var i = 0; i < chain.Count; i++)
        {
            fmt.WriteLine($"{i + 1,3}. {chain[i].Label}");
            if (chain[i].Reason is { } reason)
                fmt.WriteLine($"     reason: {reason}");
        }

        return 0;
    }

    /// <summary>
    /// The cheap probe summary — origin, sidecar paths, and reference counts — printed for a
    /// Native AOT binary without attaching. Local reference paths are listed as positive
    /// evidence; package and other references are summarized as counts, never dumped.
    /// </summary>
    private static void WritePreIlcProbeSummary(PreIlcSidecars s, OutputFormatter fmt)
    {
        if (s.ManagedAssemblyPath is { } managed)
        {
            var refParts = new List<string>();
            if (s.LocalReferencePaths.Count > 0) refParts.Add($"{s.LocalReferencePaths.Count} local");
            if (s.PackageReferenceCount > 0) refParts.Add($"{s.PackageReferenceCount} package");
            if (s.OtherReferenceCount > 0) refParts.Add($"{s.OtherReferenceCount} other");
            var refs = refParts.Count > 0 ? $" (+{string.Join(", ", refParts)} refs)" : "";
            fmt.WriteLine($"Pre-ILC:    {Path.GetFileName(managed)}{refs}");
            fmt.WriteLine($"  Origin:   {s.Origin}");
            fmt.WriteLine($"  Pdb:      {s.PdbStatus}");
        }
        else
        {
            fmt.WriteLine("Pre-ILC:    sidecars only (no attachable managed assembly)");
        }

        if (s.MstatPath is not null) fmt.WriteLine("  mstat:    present");
        if ((s.CodegenDgmlPath ?? s.ScanDgmlPath) is not null) fmt.WriteLine("  dgml:     present");
    }

    /// <summary>
    /// Builds the JSON <c>preIlc</c> probe object: origin, sidecar paths, local reference
    /// paths, and package/other reference counts. Returns null when no sidecars were found.
    /// </summary>
    private static object? BuildPreIlcProbeJson(AssemblyAnalyzer a)
    {
        if (a.PreIlcSidecars is not { } s)
            return null;

        return new
        {
            s.ManagedAssemblyPath,
            Origin = s.Origin.ToString(),
            s.ManagedPdbPath,
            PdbStatus = s.PdbStatus.ToString(),
            s.MstatPath,
            s.CodegenDgmlPath,
            s.ScanDgmlPath,
            s.IlcResponseFilePath,
            s.LocalReferencePaths,
            s.PackageReferenceCount,
            s.OtherReferenceCount,
            s.UnresolvedReferencePaths,
            s.HasAttachableCompanion,
            s.Details
        };
    }

    /// <summary>
    /// Builds the JSON <c>readyToRun</c> summary: status, version, composite/component flags,
    /// architecture, and precompiled-method counts. Returns null when the image is not ReadyToRun.
    /// </summary>
    private static object? BuildReadyToRunJson(AssemblyAnalyzer a)
    {
        if (a.ReadyToRunInfo is not { } info)
            return null;

        return new
        {
            Status = info.Status.ToString(),
            info.MajorVersion,
            info.MinorVersion,
            info.IsComposite,
            info.IsComponent,
            info.IsPartialImage,
            Architecture = info.Architecture.ToString(),
            info.OwnerCompositeExecutable,
            PrecompiledMethods = a.ReadyToRunIndex?.Methods.Count ?? 0,
            InstantiationCount = a.ReadyToRunIndex?.InstantiationCount ?? 0,
            TotalCodeSize = a.ReadyToRunIndex?.TotalCodeSize ?? 0,
            info.Diagnostic
        };
    }

    /// <summary>
    /// The <c>--correlate</c> action. Bare (<paramref name="target"/> null) attaches, builds the
    /// index, and prints correlation counts. With a value it resolves one method (by name or
    /// <c>0x</c> address) and prints its status, sizes, IL, and native code. An ambiguous name
    /// lists every candidate and exits non-zero — the ambiguity is never resolved by guessing.
    /// </summary>
    private static int PrintCorrelate(AssemblyAnalyzer a, string? target, OutputFormatter fmt)
    {
        if (a.BinaryKind != BinaryKind.NativeAot)
        {
            OutputFormatter.WriteError("Error: --correlate requires a Native AOT binary");
            return 1;
        }

        var companions = a.PreIlcCompanions ?? a.AttachPreIlcCompanions();
        if (companions is null)
        {
            OutputFormatter.WriteError(a.PreIlcSidecars is { HasAttachableCompanion: true }
                ? "Error: correlation unavailable: pre-ILC companion assembly could not be opened"
                : "Error: correlation unavailable: no pre-ILC managed assembly was found next to the binary "
                    + "(publish leaves it in obj\\<cfg>\\<tfm>\\<rid>)");
            return 1;
        }

        if (a.ManagedNativeIndex is not { } index)
        {
            OutputFormatter.WriteError("Error: correlation unavailable: the correlation index could not be built");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(target))
            return PrintCorrelateCounts(index, companions, fmt);

        return PrintCorrelateMethod(a, target, fmt);
    }

    private static int PrintCorrelateCounts(
        ManagedNativeIndex index, PreIlcCompanionSet companions, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(new
            {
                RootAssembly = companions.Root.AssemblyName,
                LocalReferenceCount = companions.LocalReferences.Count,
                index.ExactCount,
                index.AmbiguousCount,
                index.MstatOnlyCount,
                index.NotInImageCount,
                TotalMethods = index.Methods.Count,
                index.TotalCorrelatedSize
            });
            return 0;
        }

        fmt.WriteLine($"Root:       {companions.Root.AssemblyName ?? "(unknown)"}");
        if (companions.LocalReferences.Count > 0)
            fmt.WriteLine($"Local refs: {companions.LocalReferences.Count}");
        fmt.WriteLine("");
        fmt.WriteLine($"Correlation: {index.ExactCount} exact, {index.AmbiguousCount} ambiguous, "
            + $"{index.MstatOnlyCount} size-only, {index.NotInImageCount} trimmed/inlined "
            + $"of {index.Methods.Count} methods");
        fmt.WriteLine($"Correlated:  {DotsiderState.FormatSize(index.TotalCorrelatedSize)}");
        return 0;
    }

    private static int PrintCorrelateMethod(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        var result = CorrelationQuery.Resolve(a, target, CancellationToken.None);
        switch (result.Outcome)
        {
            case CorrelationQueryOutcome.Unavailable:
                OutputFormatter.WriteError($"Error: correlation unavailable: {result.Message}");
                return 1;

            case CorrelationQueryOutcome.NotFound:
                OutputFormatter.WriteError($"Error: {result.Message}");
                return 1;

            case CorrelationQueryOutcome.Ambiguous:
                if (fmt.JsonMode)
                {
                    fmt.WriteJson(new { Ambiguous = true, result.Message, result.Candidates });
                }
                else
                {
                    OutputFormatter.WriteError($"Error: {result.Message}:");
                    foreach (var c in result.Candidates)
                        OutputFormatter.WriteError(
                            $"  {c.AssemblyName}  {c.DeclaringType}::{c.Name}  token 0x{c.Token:X8}"
                            + (c.VirtualAddress is { } va ? $"  @ 0x{va:X}" : ""));
                }

                return 2;

            default:
                return PrintCorrelateReport(result.Report!, fmt);
        }
    }

    private static int PrintCorrelateReport(CorrelationReport report, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(report);
            return 0;
        }

        fmt.WriteLine($"Method:     {report.Method}");
        fmt.WriteLine($"Assembly:   {report.Assembly}");
        fmt.WriteLine($"Token:      0x{report.Token:X8}");
        fmt.WriteLine($"Status:     {report.Status}");
        if (report.Symbols.Count > 0)
        {
            fmt.WriteLine($"Symbols ({report.Symbols.Count}):");
            foreach (var s in report.Symbols)
                fmt.WriteLine($"  0x{s.VirtualAddress:X}  {DotsiderState.FormatSize(s.Size)}  {s.Name}");
        }

        if (report.NativeSize > 0)
            fmt.WriteLine($"Owned size: {DotsiderState.FormatSize(report.NativeSize)}");
        if (report.SharedCandidateSize > 0)
            fmt.WriteLine($"Shared size: {DotsiderState.FormatSize(report.SharedCandidateSize)} (shared with overloads)");
        if (report.MstatSize > 0)
            fmt.WriteLine($"mstat size: {DotsiderState.FormatSize(report.MstatSize)}");

        if (report.Il is { } il)
        {
            fmt.WriteLine("");
            fmt.WriteLine("--- IL (pre-ILC) ---");
            fmt.WriteLine(il);
        }

        if (report.NativeDisassembly is { } native)
        {
            fmt.WriteLine("");
            fmt.WriteLine("--- Native ---");
            fmt.WriteLine(native);
        }
        else if (report.Symbols.Count == 0)
        {
            fmt.WriteLine("");
            fmt.WriteLine(report.MstatSize > 0
                ? "(size only from mstat; no native symbol to disassemble)"
                : "(not in native image — trimmed or inlined)");
        }

        return 0;
    }

    /// <summary>
    /// The <c>--r2r-correlate</c> action. Bare prints the ReadyToRun stats; with a value it
    /// resolves one method (by name, <c>0x</c> token, or <c>0x</c> address) and prints its IL and
    /// precompiled native code. An ambiguous query lists candidates and exits non-zero.
    /// </summary>
    private static int PrintR2rCorrelate(AssemblyAnalyzer a, string? target, OutputFormatter fmt)
    {
        if (a.BinaryKind != BinaryKind.ReadyToRun)
        {
            OutputFormatter.WriteError("Error: --r2r-correlate requires a ReadyToRun image");
            return 1;
        }

        return string.IsNullOrWhiteSpace(target) ? PrintR2rStats(a, fmt) : PrintR2rMethod(a, target, fmt);
    }

    private static int PrintR2rStats(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        var info = a.ReadyToRunInfo!;
        var index = a.ReadyToRunIndex;
        if (fmt.JsonMode)
        {
            fmt.WriteJson(new
            {
                Status = info.Status.ToString(),
                info.MajorVersion,
                info.MinorVersion,
                info.IsComposite,
                info.IsComponent,
                info.IsPartialImage,
                Architecture = info.Architecture.ToString(),
                info.OwnerCompositeExecutable,
                PrecompiledMethods = index?.Methods.Count ?? 0,
                InstantiationCount = index?.InstantiationCount ?? 0,
                TotalCodeSize = index?.TotalCodeSize ?? 0
            });
            return 0;
        }

        fmt.WriteLine($"ReadyToRun: v{info.MajorVersion}.{info.MinorVersion} ({info.Status})");
        fmt.WriteLine($"Arch:       {info.Architecture}");
        fmt.WriteLine($"Composite:  {info.IsComposite}{(info.IsComponent ? " (component)" : "")}{(info.IsPartialImage ? ", partial image" : "")}");
        if (info.OwnerCompositeExecutable is { } owner)
            fmt.WriteLine($"Owner:      {owner}");
        fmt.WriteLine($"Precompiled: {index?.Methods.Count ?? 0} methods "
            + $"({index?.InstantiationCount ?? 0} instantiations), "
            + $"{DotsiderState.FormatSize(index?.TotalCodeSize ?? 0)}");
        if (info.Diagnostic is { } diagnostic)
            fmt.WriteLine($"Note:       {diagnostic}");
        return 0;
    }

    private static int PrintR2rMethod(AssemblyAnalyzer a, string target, OutputFormatter fmt)
    {
        var result = ReadyToRunCorrelationQuery.Resolve(a, target, CancellationToken.None);
        switch (result.Outcome)
        {
            case ReadyToRunQueryOutcome.Unavailable:
                OutputFormatter.WriteError($"Error: {result.Message}");
                return 1;

            case ReadyToRunQueryOutcome.NotFound:
                OutputFormatter.WriteError($"Error: {result.Message}");
                return 1;

            case ReadyToRunQueryOutcome.Ambiguous:
                if (fmt.JsonMode)
                {
                    fmt.WriteJson(new { Ambiguous = true, result.Message, result.Candidates });
                }
                else
                {
                    OutputFormatter.WriteError($"Error: {result.Message}:");
                    foreach (var c in result.Candidates)
                        OutputFormatter.WriteError(
                            $"  {c.AssemblyName}  {c.DeclaringType}::{c.Name}  token 0x{c.Token:X8}"
                            + (c.VirtualAddress is { } va ? $"  @ 0x{va:X}" : ""));
                }

                return 2;

            default:
                return PrintR2rReport(result.Report!, fmt);
        }
    }

    private static int PrintR2rReport(ReadyToRunMethodReport report, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(report);
            return 0;
        }

        fmt.WriteLine($"Method:     {report.Method}");
        fmt.WriteLine($"Assembly:   {report.Assembly}"
            + (report.IsComposite ? $" (composite; owner {report.OwnerComponent})" : ""));
        fmt.WriteLine($"Token:      0x{report.Token:X8}");
        fmt.WriteLine($"Native:     {report.Availability}"
            + (report.Diagnostic is { } d ? $" — {d}" : ""));
        if (report.Ranges.Count > 0)
        {
            fmt.WriteLine($"Ranges ({report.Ranges.Count}), {DotsiderState.FormatSize(report.NativeSize)}:");
            foreach (var r in report.Ranges)
                fmt.WriteLine($"  {r.Name,-9} 0x{r.VirtualAddress:X}  {DotsiderState.FormatSize(r.Size)}");
        }

        if (report.Il is { } il)
        {
            fmt.WriteLine("");
            fmt.WriteLine("--- IL ---");
            fmt.WriteLine(il);
        }

        if (report.NativeText is { } native)
        {
            fmt.WriteLine("");
            fmt.WriteLine("--- Native ---");
            fmt.WriteLine(native);
        }

        return 0;
    }

    private static int PrintFields(AssemblyAnalyzer a, OutputFormatter fmt)
    {
        if (fmt.JsonMode)
        {
            fmt.WriteJson(a.FieldDefs);
            return 0;
        }

        fmt.WriteTable(
            ["Namespace", "Type", "Name", "Signature"],
            a.FieldDefs.Select(f =>
            {
                var lastDot = f.DeclaringType.LastIndexOf('.');
                var ns = lastDot >= 0 ? f.DeclaringType[..lastDot] : "";
                var typeName = lastDot >= 0 ? f.DeclaringType[(lastDot + 1)..] : f.DeclaringType;
                return new[] { ns, typeName, f.Name, f.Signature };
            }));

        return 0;
    }

    private static int PrintBundle(string filePath, OutputFormatter fmt)
    {
        if (!SingleFileBundleReader.IsBundle(filePath, out var offset))
        {
            OutputFormatter.WriteError("Error: File is not a single-file bundle");
            return 1;
        }

        BundleManifest manifest;
        try
        {
            manifest = SingleFileBundleReader.ReadManifest(filePath, offset);
        }
        catch (InvalidDataException)
        {
            OutputFormatter.WriteError("Error: Invalid single-file bundle manifest");
            return 1;
        }

        if (fmt.JsonMode)
        {
            fmt.WriteJson(manifest);
            return 0;
        }

        fmt.WriteLine($"Bundle version: {manifest.MajorVersion}.{manifest.MinorVersion}");
        fmt.WriteLine($"Entries: {manifest.FileCount}");
        fmt.WriteLine("");
        fmt.WriteTable(
            ["Name", "Type", "Size", "Compressed"],
            manifest.Entries.Select(e => new[]
            {
                e.RelativePath,
                e.Type.ToString(),
                DotsiderState.FormatSize(e.Size),
                e.CompressedSize > 0 ? DotsiderState.FormatSize(e.CompressedSize) : "-"
            }));

        return 0;
    }
}
