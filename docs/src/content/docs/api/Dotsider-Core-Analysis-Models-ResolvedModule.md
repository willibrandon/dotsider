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

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
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

## Methods

### Deconstruct(out ImmutableArray\<byte\>, out string, out string, out string?, out string?)

**Parameters:**

- `Bytes` ([ImmutableArray\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.immutable.immutablearray-1))
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ManifestPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PreferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out ImmutableArray<byte> Bytes, out string Path, out string ManifestPath, out string? TargetFramework, out string? PreferredRuntimePack)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ResolvedAssembly?)

**Parameters:**

- `other` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(ResolvedAssembly? other)
```

### Equals(ResolvedModule?)

**Parameters:**

- `other` ([ResolvedModule](/api/dotsider.core.analysis.models.resolvedmodule/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ResolvedModule? other)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### PrintMembers(StringBuilder)

**Parameters:**

- `builder` ([StringBuilder](https://learn.microsoft.com/dotnet/api/system.text.stringbuilder))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
protected override bool PrintMembers(StringBuilder builder)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(ResolvedModule?, ResolvedModule?)

**Parameters:**

- `left` ([ResolvedModule](/api/dotsider.core.analysis.models.resolvedmodule/))
- `right` ([ResolvedModule](/api/dotsider.core.analysis.models.resolvedmodule/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ResolvedModule? left, ResolvedModule? right)
```

### operator ==(ResolvedModule?, ResolvedModule?)

**Parameters:**

- `left` ([ResolvedModule](/api/dotsider.core.analysis.models.resolvedmodule/))
- `right` ([ResolvedModule](/api/dotsider.core.analysis.models.resolvedmodule/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ResolvedModule? left, ResolvedModule? right)
```
