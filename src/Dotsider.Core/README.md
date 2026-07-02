# Dotsider.Core

Core library for .NET assembly analysis. Provides analyzers, models, and the diagnostics protocol used by the dotsider TUI, CLI, and MCP server.

## Analyzers

| Class | Description |
|-------|-------------|
| `AssemblyAnalyzer` | Loads a PE file and exposes types, methods, references, PE headers, CLR header, sections, custom attributes, and resources |
| `IlDisassembler` | Disassembles method bodies into IL instruction sequences |
| `IlNavigationResolver` | Resolves metadata tokens from IL instructions to `IlNavigationTarget` records (file path, type/method, member kind) for go-to-definition |
| `StringExtractor` | Extracts user strings, metadata strings, and raw binary strings from an assembly |
| `SizeAnalyzer` | Builds a hierarchical size tree (namespace → type → method) by IL byte size; for Native AOT binaries with an mstat sidecar the tree comes from the compiler's size report instead, with per-assembly subtrees and data categories |
| `DependencyGraphBuilder` | Generates a dependency graph (nodes and edges) from assembly references; routes through `NetFxBinder` for .NET Framework roots so nodes are keyed on the *bound* identity (post-redirect). Native AOT binaries graph the compiled-in assemblies (mstat × DGML join) plus native import modules |
| `MstatReader` | Decodes an ILC size report (`.mstat`) — a valid ECMA-335 assembly whose data lives in IL streams — into per-method, per-type, blob, frozen object, RVA field, and resource sizes with assembly attribution and dependency-graph node names |
| `DgmlReader` | Streams an ILC dependency-graph DGML file into a `DgmlGraph` whose `PathToRoot` answers "why is this in my binary" — node labels equal mstat node names, joining the two files |
| `AssemblyDiffer` | Compares two assemblies and reports added, removed, and changed types, methods, and references. Method body comparison uses normalized IL instruction walks with semantic token resolution, deep local signature decoding, and exception region analysis |
| `RuntimeTracer` | Launches a .NET process with EventPipe tracing for JIT, GC, exception, and counter events |
| `NuGetPackageAnalyzer` | Reads `.nupkg` files for package metadata and DLL listing |
| `NuGetDepsJsonResolver` | Resolves assembly references by consulting the referencing assembly's `.deps.json` to locate NuGet dependencies in the global packages folder |
| `SingleFileBundleReader` | Detects and reads .NET single-file bundles — parses the manifest and extracts individual entries |
| `DotNetRuntimeLocator` | Discovers system .NET installations and resolves shared framework assembly paths |
| `AssemblyLoader` | Opens assemblies with automatic apphost companion redirect and single-file bundle entry extraction |
| `ApphostDetector` | Detects .NET apphost executables, locates companion managed assemblies, and identifies single-file bundles |
| `ImplementationAssemblyResolver` | Maps reference assemblies to their implementations via known mappings, type forwarders, bundles, and shared framework probing |
| `NetFxBinder` | CLR-accurate .NET Framework binder. Walks the real fusion order — framework unification + machine.config + publisher policy + app config, then GAC + framework runtime directory + `<codeBase>` + app base + `<probing privatePath>` — so the dep graph reflects what the actual CLR loads. Switches probe locations and GAC token format on `NetFxRuntimeVersion`: CLR 4 (`%WINDIR%\Microsoft.NET\assembly`, `v4.0_…` tokens, `v4.0.30319` runtime) and CLR 2 (`%WINDIR%\assembly`, no-prefix tokens, `v2.0.50727` runtime) |
| `AssemblyIdentityFormat` | Formats an assembly's full identity into a stable opaque key (`Name|Version|Culture|PublicKeyToken`) used as a graph node identifier and a `TypeRefInfo` resolution-scope grouping key |

## Models

All models live in `Analysis/Models/` and are plain records or enums suitable for JSON serialization.

**Metadata:** `TypeDefInfo`, `MethodDefInfo`, `MemberRefInfo`, `MemberRefKind`, `TypeRefInfo`, `FieldDefInfo`, `AssemblyRefInfo`, `CustomAttributeInfo`, `ResourceInfo`

**PE / CLR:** `PeHeaders`, `ClrHeader`, `SectionInfo`, `DebugDirectoryInfo`

**IL:** `IlInstruction`, `IlNavigationTarget`, `MethodDebugInfo`, `SequencePointInfo`, `LocalSlotInfo`

**Portable PDB:** `PdbProvenance`, `PdbProvenanceKind`, `SourceLinkInfo`, `SourceLinkMapping`, `EmbeddedSourceInfo`

**Size:** `SizeNode`, `SizeNodeKind`

**Native AOT sidecars:** `MstatData`, `MstatMethod`, `MstatType`, `MstatBlob`, `MstatRvaField`, `MstatFrozenObject`, `MstatManifestResource`, `MstatDeduplicatedMethod`, `DgmlGraph`, `DgmlNode`, `DgmlLink`, `DgmlPathStep`

**Strings:** `StringEntry`, `StringSource`

**Diff:** `AssemblyDiffResult`, `DiffEntry`, `DiffKind`, `DiffSummary`

**Tracing:** `TraceEventEntry`, `TraceEventCategory`, `CounterSnapshot`, `TraceSummary`, `TraceProcessState`, `OutputLine`

**Dependency graph:** `DependencyGraphResult`, `GraphNode`, `GraphEdge`, `GraphNavigationContext`

**Bundles:** `BundleManifest`, `BundleEntry`, `BundleFileType`

**Resolution discriminated unions:** `ResolvedAssembly` (`FromFile`, `FromBundle`), `AssemblyOpenResult` (`Direct`, `ApphostWithCompanion`, `BundleEntry`), `AssemblyResolution` (carries the resolved file/bundle plus provenance, applied policy, and bound identity), `AssemblyProvenance`

**NuGet:** `NuGetFileEntry`

**.NET Framework binder:** `NetFxBindingContext` (per-root binding context the binder threads through every resolution surface), `NetFxRuntimeVersion` (`Clr2` / `Clr4`), `NetFxArchitecture` (`X86` / `Amd64`), `NetFxBindResult`, `BindingPolicy` (layered framework unification + machine.config + publisher policy + app config), `BindingPolicyParseResult`, `BindingRedirect`, `CodeBaseEntry`, `PolicyLayer`, `AppliedPolicy`, `LoadedAssemblyEntry`

## Protocol

The diagnostics protocol enables communication between the dotsider TUI and external clients (CLI, MCP server) over Unix domain sockets.

| Class | Description |
|-------|-------------|
| `DotsiderProtocol` | Protocol-version constant. Bumped on breaking changes; adding optional fields does not bump |
| `DotsiderRequest` | JSON request with `Method`, plus optional parameters (`AssemblyPath`, `TypeName`, `TabId`, etc.) |
| `DotsiderResponse` | JSON response with `Success`, optional `Error`, and `Data` payload |
| `DotsiderJsonOptions` | Shared serialization settings (camelCase, ignore nulls, case-insensitive reads) |
| `ResolvedAssemblyInfo` | Serialization-safe DTO for an assembly resolution result (file path, bundle entry name, bundle path) |
| `FrameworkAssemblyInfo` | Result of resolving an assembly from a system shared framework — full path plus the runtime pack that provided it |

### Protocol Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `assembly-info` | — | Assembly metadata (name, version, framework, architecture) |
| `list-types` | Query?, MaxResults? | Type definitions with optional filter |
| `list-methods` | TypeName?, Query?, MaxResults? | Method definitions with optional filter |
| `find-members` | Query, MaxResults? | Search types, methods, and member refs |
| `list-fields` | TypeName?, Query?, MaxResults? | Field definitions with optional filter |
| `get-pe-headers` | — | PE machine type, subsystem, characteristics |
| `get-clr-header` | — | Runtime version, flags, entry point token |
| `get-sections` | — | PE section table |
| `get-custom-attributes` | — | Custom attributes |
| `get-resources` | — | Manifest resources |
| `resolve-token` | Token | Resolve a metadata token |
| `read-bytes` | Offset, Length | Raw bytes from the loaded image |
| `disassemble` | TypeName, MethodName, IncludeDebugInfo? | IL bytecode for a method |
| `get-method-debug-info` | TypeName, MethodName | Portable PDB sequence points and local names for a method |
| `get-source-link` | — | Source Link mappings decoded from the portable PDB |
| `search-il-opcodes` | Query, MaxResults? | Find methods containing an opcode |
| `get-size-tree` | — | Hierarchical size tree (AOT-aware: mstat-backed for Native AOT binaries) |
| `get-largest-methods` | MaxResults? | Top methods by IL size |
| `get-strings` | Query?, MinLength?, MaxResults? | User, metadata, and binary strings |
| `get-assembly-refs` | — | Assembly references |
| `get-dependency-graph` | — | Dependency graph nodes and edges (AOT-aware: compiled-in assemblies and native imports) |
| `get-type-refs` | — | Type references |
| `diff` | LeftPath, RightPath | Assembly comparison |
| `is-bundle` | AssemblyPath | Check if a file is a single-file bundle |
| `get-bundle-manifest` | AssemblyPath | Bundle manifest with entry list |
| `analyze-nupkg` | AssemblyPath | NuGet package metadata and DLL listing |
| `resolve-assembly` | AssemblyName | Resolve a dependency using the standard resolution chain |
| `navigate-to-il-definition` | Token | Go-to-definition by metadata token |
| `navigate-back` | — | Back navigation (IL back, cross-view, assembly pop) |
| `push-assembly` | AssemblyPath? or AssemblyName? | Open a dependency assembly |
| `get-current-view` | — | Active tab, view state, and trace eligibility |
| `navigate` | TabId | Switch to a tab |
| `search` | Query, MaxResults? | Cross-view search |
| `get-trace-events` | CategoryFilter?, MaxResults? | Trace events |
| `get-trace-counters` | — | Performance counter snapshot |
| `get-trace-summary` | — | Aggregated trace summary (durations, counts, exceptions) |
| `get-process-output` | — | Traced process stdout/stderr |
| `start-trace` | Arguments? | Launch and trace the assembly |
| `stop-trace` | — | Stop the active trace |
