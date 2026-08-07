---
title: "PreIlcSidecars"
description: "The pre-ILC build outputs found for a Native AOT binary: the managed input assembly ILC compiled, its portable PDB, and any mstat/DGML sidecars discovered in the build's intermediate tree. A result exists whenever anything was found — mstat/DGML-only results feed silent fallbacks, while the attach/offer flow gates on HasAttachableCompanion."
slug: api/dotsider.core.analysis.models.preilcsidecars
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The pre-ILC build outputs found for a Native AOT binary: the managed input assembly
ILC compiled, its portable PDB, and any mstat/DGML sidecars discovered in the build's
intermediate tree. A result exists whenever anything was found — mstat/DGML-only
results feed silent fallbacks, while the attach/offer flow gates on
[HasAttachableCompanion](/api/dotsider.core.analysis.models.preilcsidecars.hasattachablecompanion/).

```csharp
public sealed record PreIlcSidecars : IEquatable<PreIlcSidecars>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PreIlcSidecars**

## Implements

- [IEquatable\<PreIlcSidecars\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### PreIlcSidecars(string?, PreIlcAssemblyOrigin, string?, PreIlcPdbStatus, string?, string?, string?, string?, IReadOnlyList\<string\>, int, int, IReadOnlyList\<string\>, string?)

The pre-ILC build outputs found for a Native AOT binary: the managed input assembly
ILC compiled, its portable PDB, and any mstat/DGML sidecars discovered in the build's
intermediate tree. A result exists whenever anything was found — mstat/DGML-only
results feed silent fallbacks, while the attach/offer flow gates on
[HasAttachableCompanion](/api/dotsider.core.analysis.models.preilcsidecars.hasattachablecompanion/).

**Parameters:**

- `ManagedAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The validated pre-ILC managed assembly, or null when none was found.
- `Origin` ([PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)): How ManagedAssemblyPath was located.
- `ManagedPdbPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The sidecar portable PDB probed beside the managed assembly, when one exists (kept even when mismatched, for diagnostics).
- `PdbStatus` ([PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)): The portable-PDB situation of the managed assembly.
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): An mstat sidecar found in the intermediate tree, or null.
- `CodegenDgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The codegen dependency graph found in the intermediate tree, or null. Its node names match the mstat's exactly.
- `ScanDgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The scan dependency graph found in the intermediate tree, or null.
- `IlcResponseFilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The ILC response file that was parsed, or null.
- `LocalReferencePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Reference assemblies with positive local/project evidence (under the project tree or a build-output-shaped path outside any package store), metadata-validated.
- `PackageReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): References resolved from a package store (runtime pack, NuGet cache, SDK packs) — summarized, never enumerated.
- `OtherReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): References that exist but carry no positive local evidence — summarized and listed in Details, never classified local.
- `UnresolvedReferencePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Reference paths that do not exist locally (copied build trees, foreign machines) — recorded verbatim, never treated as local.
- `Details` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Diagnostic notes: skipped candidates, fall-through reasons, staleness, unclassified references.

```csharp
public PreIlcSidecars(string? ManagedAssemblyPath, PreIlcAssemblyOrigin Origin, string? ManagedPdbPath, PreIlcPdbStatus PdbStatus, string? MstatPath, string? CodegenDgmlPath, string? ScanDgmlPath, string? IlcResponseFilePath, IReadOnlyList<string> LocalReferencePaths, int PackageReferenceCount, int OtherReferenceCount, IReadOnlyList<string> UnresolvedReferencePaths, string? Details)
```

## Properties

### CodegenDgmlPath

The codegen dependency graph found in the intermediate tree, or null. Its node names match the mstat's exactly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CodegenDgmlPath { get; init; }
```

### Details

Diagnostic notes: skipped candidates, fall-through reasons, staleness, unclassified references.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Details { get; init; }
```

### HasAttachableCompanion

Whether a validated managed input exists to offer as an attachable companion.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasAttachableCompanion { get; }
```

### IlcResponseFilePath

The ILC response file that was parsed, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? IlcResponseFilePath { get; init; }
```

### LocalReferencePaths

Reference assemblies with positive local/project evidence (under the project tree or a build-output-shaped path outside any package store), metadata-validated.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> LocalReferencePaths { get; init; }
```

### ManagedAssemblyPath

The validated pre-ILC managed assembly, or null when none was found.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ManagedAssemblyPath { get; init; }
```

### ManagedPdbPath

The sidecar portable PDB probed beside the managed assembly, when one exists (kept even when mismatched, for diagnostics).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ManagedPdbPath { get; init; }
```

### MstatPath

An mstat sidecar found in the intermediate tree, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MstatPath { get; init; }
```

### Origin

How ManagedAssemblyPath was located.

**Returns:** [PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)

```csharp
public PreIlcAssemblyOrigin Origin { get; init; }
```

### OtherReferenceCount

References that exist but carry no positive local evidence — summarized and listed in Details, never classified local.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int OtherReferenceCount { get; init; }
```

### PackageReferenceCount

References resolved from a package store (runtime pack, NuGet cache, SDK packs) — summarized, never enumerated.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int PackageReferenceCount { get; init; }
```

### PdbStatus

The portable-PDB situation of the managed assembly.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
public PreIlcPdbStatus PdbStatus { get; init; }
```

### ScanDgmlPath

The scan dependency graph found in the intermediate tree, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ScanDgmlPath { get; init; }
```

### UnresolvedReferencePaths

Reference paths that do not exist locally (copied build trees, foreign machines) — recorded verbatim, never treated as local.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> UnresolvedReferencePaths { get; init; }
```

## Methods

### Deconstruct(out string?, out PreIlcAssemblyOrigin, out string?, out PreIlcPdbStatus, out string?, out string?, out string?, out string?, out IReadOnlyList\<string\>, out int, out int, out IReadOnlyList\<string\>, out string?)

**Parameters:**

- `ManagedAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Origin` ([PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/))
- `ManagedPdbPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PdbStatus` ([PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CodegenDgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ScanDgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IlcResponseFilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `LocalReferencePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `PackageReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `OtherReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `UnresolvedReferencePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Details` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string? ManagedAssemblyPath, out PreIlcAssemblyOrigin Origin, out string? ManagedPdbPath, out PreIlcPdbStatus PdbStatus, out string? MstatPath, out string? CodegenDgmlPath, out string? ScanDgmlPath, out string? IlcResponseFilePath, out IReadOnlyList<string> LocalReferencePaths, out int PackageReferenceCount, out int OtherReferenceCount, out IReadOnlyList<string> UnresolvedReferencePaths, out string? Details)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(PreIlcSidecars?)

**Parameters:**

- `other` ([PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(PreIlcSidecars? other)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(PreIlcSidecars?, PreIlcSidecars?)

**Parameters:**

- `left` ([PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/))
- `right` ([PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(PreIlcSidecars? left, PreIlcSidecars? right)
```

### operator ==(PreIlcSidecars?, PreIlcSidecars?)

**Parameters:**

- `left` ([PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/))
- `right` ([PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(PreIlcSidecars? left, PreIlcSidecars? right)
```
