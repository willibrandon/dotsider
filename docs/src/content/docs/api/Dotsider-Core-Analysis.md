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

### [AssemblyLoader](/api/dotsider.core.analysis.assemblyloader/)

Shared factory for opening assembly files. Handles apphosts (companion .dll redirect),
single-file bundles (entry assembly extraction), and direct .dll/.exe loading.
Returns an [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) that preserves the distinction so callers
can decide how to present each case (e.g. showing an apphost dialog).

```csharp
public static class AssemblyLoader
```

### [DependencyGraphBuilder](/api/dotsider.core.analysis.dependencygraphbuilder/)

Builds a dependency graph from an assembly's references and type refs.
Uses a hierarchical tree layout with the root assembly at top center.

```csharp
public static class DependencyGraphBuilder
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
for treemap visualization.

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

