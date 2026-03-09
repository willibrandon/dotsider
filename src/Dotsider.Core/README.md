# Dotsider.Core

Core library for .NET assembly analysis. Provides analyzers, models, and the diagnostics protocol used by the dotsider TUI, CLI, and MCP server.

## Analyzers

| Class | Description |
|-------|-------------|
| `AssemblyAnalyzer` | Loads a PE file and exposes types, methods, references, PE headers, CLR header, sections, custom attributes, and resources |
| `IlDisassembler` | Disassembles method bodies into IL instruction sequences |
| `StringExtractor` | Extracts user strings, metadata strings, and raw binary strings from an assembly |
| `SizeAnalyzer` | Builds a hierarchical size tree (namespace → type → method) by IL byte size |
| `DependencyGraphBuilder` | Generates a dependency graph (nodes and edges) from assembly references |
| `AssemblyDiffer` | Compares two assemblies and reports added, removed, and changed types, methods, and references |
| `RuntimeTracer` | Launches a .NET process with EventPipe tracing for JIT, GC, exception, and counter events |
| `NuGetPackageAnalyzer` | Reads `.nupkg` files for package metadata and DLL listing |
| `TreemapLayout` | Computes squarified treemap rectangles for size visualization |

## Models

All models live in `Analysis/Models/` and are plain records or classes suitable for JSON serialization:

- `TypeDefInfo`, `MethodDefInfo`, `MemberRefInfo`, `TypeRefInfo`
- `AssemblyRefInfo`, `CustomAttributeInfo`, `ResourceInfo`
- `PeHeaders`, `ClrHeader`, `SectionInfo`
- `IlInstruction`, `SizeNode`, `StringEntry`, `StringSource`
- `DiffModels` (AssemblyDiff, TypeDiff, MethodDiff)
- `TraceEventEntry`, `TraceEventCategory`, `CounterSnapshot`, `TraceSummary`, `TraceProcessState`
- `OutputLine`, `NuGetFileEntry`, `DependencyGraph`

## Protocol

The diagnostics protocol enables communication between the dotsider TUI and external clients (CLI, MCP server) over Unix domain sockets.

| Class | Description |
|-------|-------------|
| `DotsiderRequest` | JSON request with `Method`, plus optional parameters (`AssemblyPath`, `TypeName`, `TabId`, etc.) |
| `DotsiderResponse` | JSON response with `Success`, optional `Error`, and `Data` payload |
| `DotsiderJsonOptions` | Shared serialization settings (camelCase, ignore nulls, case-insensitive reads) |

### Protocol Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `assembly-info` | — | Assembly metadata (name, version, framework, architecture) |
| `list-types` | TypeName?, MaxResults? | Type definitions with optional filter |
| `list-methods` | TypeName?, MethodName?, MaxResults? | Method definitions with optional filter |
| `find-members` | Query | Search types, methods, and member refs |
| `get-pe-headers` | — | PE machine type, subsystem, characteristics |
| `get-clr-header` | — | Runtime version, flags, entry point token |
| `get-sections` | — | PE section table |
| `get-custom-attributes` | — | Custom attributes |
| `get-resources` | — | Manifest resources |
| `resolve-token` | Token | Resolve a metadata token |
| `disassemble` | TypeName, MethodName | IL bytecode for a method |
| `search-il-opcodes` | Query | Find methods containing an opcode |
| `get-size-breakdown` | — | Hierarchical size tree |
| `get-largest-methods` | MaxResults? | Top methods by IL size |
| `extract-strings` | MinLength? | User, metadata, and binary strings |
| `get-assembly-refs` | — | Assembly references |
| `get-dependency-graph` | — | Dependency graph nodes and edges |
| `get-type-refs` | — | Type references |
| `diff-assemblies` | LeftPath, RightPath | Assembly comparison |
| `get-current-view` | — | Active tab and view state |
| `navigate` | TabId | Switch to a tab |
| `get-trace-events` | CategoryFilter?, MaxResults? | Trace events |
| `get-trace-counters` | — | Performance counter snapshot |
| `get-process-output` | — | Traced process stdout/stderr |
| `start-trace` | Arguments? | Launch and trace the assembly |
| `stop-trace` | — | Stop the active trace |
