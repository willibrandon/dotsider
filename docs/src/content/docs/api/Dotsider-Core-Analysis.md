---
title: "Dotsider.Core.Analysis"
slug: api/dotsider.core.analysis
sidebar:
  order: 0
---

## Classes

### [ApphostDetector](/api/dotsider.core.analysis.apphostdetector/)

Detects .NET apphost executables and locates their companion managed assemblies.

```csharp
public static class ApphostDetector
```

### [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

Core analyzer that reads a .NET assembly and extracts PE, metadata, IL, and string information.
Uses [PEReader](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.pereader) and [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader) from the BCL.

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
single-file bundles (entry assembly extraction), Native AOT binaries, and direct
.dll/.exe loading. Returns an [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) that preserves the
distinction so callers can decide how to present each case (e.g. showing an apphost dialog).

```csharp
public static class AssemblyLoader
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

### [StringExtractor](/api/dotsider.core.analysis.stringextractor/)

Extracts strings from .NET assemblies across three sources:
the #US heap (user string literals), the #Strings heap (metadata identifiers),
and raw printable character sequences from the binary.

```csharp
public sealed class StringExtractor
```

