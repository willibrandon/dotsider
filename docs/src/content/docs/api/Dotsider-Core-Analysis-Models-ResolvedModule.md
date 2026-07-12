---
title: "ResolvedModule"
description: "Represents a metadata-bearing sibling module whose bytes were read and authenticated while resolving the manifest assembly's File table entry."
slug: api/dotsider.core.analysis.models.resolvedmodule
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Represents a metadata-bearing sibling module whose bytes were read and authenticated while
resolving the manifest assembly's File table entry.

```csharp
public sealed record ResolvedModule : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedModule>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/) → **ResolvedModule**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ResolvedModule\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ResolvedModule(ImmutableArray\<byte\>, string, string, string?, string?)

Represents a metadata-bearing sibling module whose bytes were read and authenticated while
resolving the manifest assembly's File table entry.

**Parameters:**

- `Bytes` ([ImmutableArray\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray-1)): The authenticated module bytes.
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The module's same-directory path beside its manifest assembly.
- `ManifestPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The manifest assembly path that authenticated the module.
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The manifest assembly's target-framework context.
- `PreferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The manifest assembly's preferred runtime-pack context.

```csharp
public ResolvedModule(ImmutableArray<byte> Bytes, string Path, string ManifestPath, string? TargetFramework, string? PreferredRuntimePack)
```

## Properties

### Bytes

The authenticated module bytes.

**Returns:** [ImmutableArray\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray-1)

```csharp
public ImmutableArray<byte> Bytes { get; init; }
```

### ManifestPath

The manifest assembly path that authenticated the module.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ManifestPath { get; init; }
```

### Path

The module's same-directory path beside its manifest assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Path { get; init; }
```

### PreferredRuntimePack

The manifest assembly's preferred runtime-pack context.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PreferredRuntimePack { get; init; }
```

### TargetFramework

The manifest assembly's target-framework context.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetFramework { get; init; }
```
