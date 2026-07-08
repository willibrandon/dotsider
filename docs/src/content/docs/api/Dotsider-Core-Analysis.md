---
title: "Dotsider.Core.Analysis"
slug: api/dotsider.core.analysis
sidebar:
  order: 0
  attrs:
    data-api-namespace: "true"
---

## Classes

### [ApphostDetector](/api/dotsider.core.analysis.apphostdetector/)

Detects .NET apphost executables and locates their companion managed assemblies.

```csharp
public static class ApphostDetector
```

### [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

Core analyzer that reads .NET assemblies, Webcil app assemblies, native binaries, and raw Wasm
modules. It uses BCL metadata/PE readers where possible and routes runtime-native formats
through dotsider's format readers for IL, strings, symbols, disassembly, and size data.

```csharp
public sealed class AssemblyAnalyzer : IDisposable
```

### [AssemblyDiffer](/api/dotsider.core.analysis.assemblydiffer/)

Compares two assemblies and produces a detailed diff result.
Uses dictionary-based O(n) matching by name.

```csharp
public static class AssemblyDiffer
```

### [AssemblyIdentityFormat](/api/dotsider.core.analysis.assemblyidentityformat/)

Formats an assembly's full identity into a stable opaque string used as a graph node
identifier and as a key for grouping [TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/) entries by the
full identity of their resolution scope.

```csharp
public static class AssemblyIdentityFormat
```

### [AssemblyLoader](/api/dotsider.core.analysis.assemblyloader/)

Shared factory for opening assembly files. Handles apphosts (companion .dll redirect),
single-file bundles (entry assembly extraction), Native AOT binaries, raw Wasm modules, and direct
.dll/.exe loading. Returns an [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) that preserves the
distinction so callers can decide how to present each case (e.g. showing an apphost dialog).

```csharp
public static class AssemblyLoader
```

### [CorrelationQuery](/api/dotsider.core.analysis.correlationquery/)

Resolves a "method or address" query against an AOT binary's pre-ILC companion set and
correlation index, producing the one [CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/) the CLI, session, and
MCP surfaces all render. Attaches the companions on demand and builds the index once;
ambiguity is surfaced as candidates, never resolved by picking the first match.

```csharp
public static class CorrelationQuery
```

### [DependencyGraphBuilder](/api/dotsider.core.analysis.dependencygraphbuilder/)

Builds the full transitive assembly dependency graph rooted at an analyzed assembly.
Performs a breadth-first walk through each assembly's [AssemblyRefs](/api/dotsider.core.analysis.assemblyanalyzer.assemblyrefs/),
resolving children by full identity, deduping on [Id](/api/dotsider.core.analysis.models.graphnode.id/), preserving edges
for cycles and diamonds, and classifying unresolvable and identity-mismatched references as
non-expanding leaf nodes. For .NET Framework roots the resolution routes through
[NetFxBinder](/api/dotsider.core.analysis.netfxbinder/) so that nodes are keyed on the *bound* identity (post-redirect),
collapsing two distinct requested versions onto a single graph node when policy redirects them
to the same loaded version. Produces a [DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/) containing the
public topology plus internal navigation metadata consumed only by the TUI.

```csharp
public static class DependencyGraphBuilder
```

### [DgmlReader](/api/dotsider.core.analysis.dgmlreader/)

Reads an ILC dependency-graph DGML file, emitted when publishing a Native AOT project with
`IlcGenerateDgmlFile`. The format is a `DirectedGraph` document of nodes (id and
label) and links (source depends on target, with a reason). Node labels equal the node
names an mstat size report stores ([MstatReader](/api/dotsider.core.analysis.mstatreader/)), which is how sizes join to
dependency chains.

Parsing streams the XML — the graphs run to hundreds of thousands of links — and never
throws: unreadable files return null, and malformed nodes or links are skipped.

```csharp
public static class DgmlReader
```

### [DotNetRuntimeLocator](/api/dotsider.core.analysis.dotnetruntimelocator/)

Discovers system .NET installations and resolves shared framework assembly paths.

```csharp
public static class DotNetRuntimeLocator
```

### [IlDisassembler](/api/dotsider.core.analysis.ildisassembler/)

Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences.

```csharp
public sealed class IlDisassembler
```

### [IlNavigationResolver](/api/dotsider.core.analysis.ilnavigationresolver/)

Resolves a metadata token from an IL instruction to an [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/)
describing what the token points to and where it lives.

```csharp
public static class IlNavigationResolver
```

### [ImplementationAssemblyResolver](/api/dotsider.core.analysis.implementationassemblyresolver/)

Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation
assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.

```csharp
public static class ImplementationAssemblyResolver
```

### [ManagedNativeIndex](/api/dotsider.core.analysis.managednativeindex/)

Joins pre-ILC managed methods to the native evidence of the AOT image they were
compiled into: native symbols (via IlcNameDemangler, keyed from real
companion metadata instead of the binary's reduced recovered types) and mstat size
rows. Built once, queried per-frame — every lookup is a dictionary hit.

```csharp
public sealed class ManagedNativeIndex
```

### [MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/)

Compares two ILC size reports and explains where the bytes went: a hierarchical delta tree
(assembly → namespace → type → method, beside the binary's data categories), flat top
contributors, and per-assembly / per-namespace aggregate deltas. Entries are matched by the
build-stable identity keys of [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/), so overloads, folded
MethodTables, and owner-grouped frozen objects compare correctly across builds.

```csharp
public static class MstatDiffer
```

### [MstatLocator](/api/dotsider.core.analysis.mstatlocator/)

Resolves a size-comparison input to its mstat report. A bare `.mstat` file is read
directly (detected by extension or by [String)](/api/dotsider.core.analysis.mstatreader.probe(system.string)/) — an mstat is
itself a valid ECMA-335 assembly, so probing must come before any managed-assembly
interpretation); a Native AOT binary resolves through its sidecar discovery
(`app.mstat` beside the binary, or the ILC intermediate output tree). Anything else —
a managed assembly, a native binary without a size report — resolves to null.

```csharp
public static class MstatLocator
```

### [MstatReader](/api/dotsider.core.analysis.mstatreader/)

Reads an ILC size report (`.mstat`), the file `IlcGenerateMstatFile` emits when
publishing a Native AOT project. The report is itself a valid ECMA-335 assembly: its
assembly version carries the format version, and its data is encoded as IL instruction
streams in global methods named `Methods`, `Types`, `Blobs`, and (in newer
formats) `RvaFields`, `FrozenObjects`, `ManifestResources`, and
`DeduplicatedMethods`. Format 2.0+ also stores each entry's dependency-graph node name
in a custom `.names` PE section; those names equal the node labels in the DGML graphs
`IlcGenerateDgmlFile` emits, which is how sizes join to dependency chains.

Malformed input never throws: unreadable files return null, and a truncated IL stream
yields the entries parsed before the damage.

```csharp
public static class MstatReader
```

### [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/)

The normalized view of an ILC size report that every size consumer shares: raw rows
aggregated under build-stable identity keys, one double-count policy for the 2.1+ detail
sections, owner-based attribution for frozen objects, and per-assembly / per-namespace byte
totals. [SizeAnalyzer](/api/dotsider.core.analysis.sizeanalyzer/) builds the Size Map from it, [MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/)
compares two of them, and budget evaluation reads its aggregates — so a total shown in one
place always equals the same total shown in another.

```csharp
public sealed class MstatSizeIndex
```

### [NativeAotDetector](/api/dotsider.core.analysis.nativeaotdetector/)

Detects Native AOT compiled .NET binaries by locating and validating the embedded
ReadyToRun header. Every Native AOT image carries this header (signature "RTR\0")
so the runtime can find its module sections, but the signature bytes also occur as
code immediates, so each candidate is validated against the field ranges the ILC
toolchain actually emits before it is accepted.

```csharp
public static class NativeAotDetector
```

### [NativeSymbolReader](/api/dotsider.core.analysis.nativesymbolreader/)

Reads a native binary's symbols — function names, addresses, and sizes — from its debug
information, demangling ILC names back to managed names and merging the overlapping records
that different symbol sources produce. Windows native PDBs, Linux DWARF, and macOS dSYM/nlist
each feed the same merge and demangle pipeline through NativeSourceMap); when no symbols
exist, unwind data still yields function boundaries at lower fidelity. The public entry points
that dispatch on image format are added as each reader lands.

```csharp
public static class NativeSymbolReader
```

### [NetFxBinder](/api/dotsider.core.analysis.netfxbinder/)

CLR-accurate .NET Framework assembly binder for both CLR generations. Consumes a
[NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/) and produces a [NetFxBindResult](/api/dotsider.core.analysis.models.netfxbindresult/) matching
what the actual .NET Framework binder would do at runtime: framework unification +
machine.config + publisher policy + app config (in CLR walk order, with later layers
overriding earlier ones), then locate against the GAC (architecture-prioritized,
strong-named only), then the framework runtime directory, then configured codeBase href
(fail-fast), then the application base + private paths with culture-aware probing.

```csharp
public static class NetFxBinder
```

### [NuGetDepsJsonResolver](/api/dotsider.core.analysis.nugetdepsjsonresolver/)

Resolves assembly references by consulting the referencing assembly's `.deps.json`
file to locate its NuGet dependencies in the NuGet global packages folder. This is the
probe step that makes library projects work — `dotnet build` does not copy NuGet
package assemblies next to a library's `bin` output, but the `.deps.json`
manifest records the exact resolved package version and runtime asset path, matching
what the .NET host uses at runtime.

```csharp
public static class NuGetDepsJsonResolver
```

### [NuGetPackageAnalyzer](/api/dotsider.core.analysis.nugetpackageanalyzer/)

Opens and analyzes a NuGet package (.nupkg) file.
Reads package metadata from .nuspec and lists all contents.

```csharp
public sealed class NuGetPackageAnalyzer : IDisposable
```

### [PreIlcSidecarDetector](/api/dotsider.core.analysis.preilcsidecardetector/)

Locates the pre-ILC build outputs of a Native AOT binary: the managed input assembly
the compiler consumed, its portable PDB, and the mstat/DGML sidecars in the build's
intermediate tree.

```csharp
public static class PreIlcSidecarDetector
```

### [ReadyToRunCorrelationQuery](/api/dotsider.core.analysis.readytoruncorrelationquery/)

Resolves a "method or address" query against a ReadyToRun image and builds the one
[ReadyToRunMethodReport](/api/dotsider.core.analysis.models.readytorunmethodreport/) the CLI, MCP, and session surfaces all render. A method
name, a `0x06…` token, or a `0x…` native address all resolve here; a value that is
both a valid token and a covered address is reported ambiguous rather than guessed. Methods
present in metadata but not precompiled resolve as IL-only rather than "not found".

```csharp
public static class ReadyToRunCorrelationQuery
```

### [ReadyToRunIndex](/api/dotsider.core.analysis.readytorunindex/)

Queryable view of a ReadyToRun image's precompiled methods: managed-method lookup by owning
assembly identity and token, and reverse lookup by native address over the methods' disjoint
code ranges. The token is qualified by assembly name because a composite spans several
assemblies whose tokens collide. Built once, every lookup a dictionary or binary-search hit.

```csharp
public sealed class ReadyToRunIndex
```

### [RuntimeTracer](/api/dotsider.core.analysis.runtimetracer/)

Manages launching a .NET assembly as a child process and collecting
runtime events via EventPipe diagnostics (PID-based connect with retry).

```csharp
public sealed class RuntimeTracer : IDisposable
```

### [SingleFileBundleReader](/api/dotsider.core.analysis.singlefilebundlereader/)

Reads .NET single-file bundles — detects the bundle signature, parses the
manifest header, and extracts individual entries.

```csharp
public static class SingleFileBundleReader
```

### [SizeAnalyzer](/api/dotsider.core.analysis.sizeanalyzer/)

Computes IL code size per method and builds a hierarchical size tree
for treemap visualization. For a Native AOT binary with an mstat sidecar the tree is
built from the compiler's size report instead: native code and MethodTable bytes per
assembly, namespace, type, and method, plus the binary's data categories. Without an
mstat, the binary's merged native symbols carry the tree.

```csharp
public static class SizeAnalyzer
```

### [SizeBasisResolver](/api/dotsider.core.analysis.sizebasisresolver/)

Resolves the total-size basis for a comparison of mstat inputs. The rule is shared by the
CLI, the MCP server, and the session protocol so a size figure never changes meaning
between surfaces: binaries measure file size on disk; a bare `.mstat` anywhere forces
mstat totals for both sides.

```csharp
public static class SizeBasisResolver
```

### [SizeBudgetEvaluator](/api/dotsider.core.analysis.sizebudgetevaluator/)

Evaluates size budgets against a size diff. Total budgets measure the caller's
basis-resolved totals (file size for binaries, mstat total for bare reports); namespace and
assembly budgets always measure the diff's mstat aggregates, with namespace targets
covering their sub-namespaces. Each breach carries the scope's top positive regressions —
the rows that explain the growth.

```csharp
public static class SizeBudgetEvaluator
```

### [SizeBudgetFile](/api/dotsider.core.analysis.sizebudgetfile/)

Reads a size-budget document: `{ "budgets": [ ... ] }` where each entry is either a
spec string in the [SizeBudgetParser](/api/dotsider.core.analysis.sizebudgetparser/) grammar or an object
`{ "name", "description", "scope", "max", "growth", "severity", "topN" }` — the object
form is how a team names its budgets, downgrades one to a warning, or pins a per-budget
contributor count. Both forms mix freely in one document. The CLI's `--budget-file`
and the MCP server's inline budget JSON share this one parser.

```csharp
public static class SizeBudgetFile
```

### [SizeBudgetParser](/api/dotsider.core.analysis.sizebudgetparser/)

Parses size-budget spec strings. The grammar is
`[scope:]limit(,limit)*` where scope is `total` (the default), `ns=NAME`, or
`asm=NAME`, and each limit is `max=SIZE` or `growth=SIZE|PERCENT`. Sizes
accept `b`, `kb`, `mb`, and `gb` suffixes (1 kb = 1024 bytes; a bare
number is bytes); percentages (`growth=1%`) apply to growth only. Examples:
`max=25mb` · `growth=1%` · `total:max=25mb,growth=50kb` ·
`ns=System.Text.Json:growth=10kb` · `asm=MyApp:max=2mb`.

```csharp
public static class SizeBudgetParser
```

### [StringExtractor](/api/dotsider.core.analysis.stringextractor/)

Extracts strings from .NET assemblies across three sources:
the #US heap (user string literals), the #Strings heap (metadata identifiers),
and raw printable character sequences from the binary.

```csharp
public sealed class StringExtractor
```

