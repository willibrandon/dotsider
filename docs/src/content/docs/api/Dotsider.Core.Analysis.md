---
title: "Dotsider.Core.Analysis"
slug: api/dotsider.core.analysis
---

## Classes

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

### [DependencyGraphBuilder](/api/dotsider.core.analysis.dependencygraphbuilder/)

Builds a dependency graph from an assembly's references and type refs.
Uses a hierarchical tree layout with the root assembly at top center.

```csharp
public static class DependencyGraphBuilder
```

### [IlDisassembler](/api/dotsider.core.analysis.ildisassembler/)

Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences.

```csharp
public sealed class IlDisassembler
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

