---
title: "ResolvedAssemblyInfo"
description: "Serialization-safe representation of an assembly resolution result. Used in protocol and MCP responses where ResolvedAssembly cannot be serialized directly because bundle and module results contain raw bytes."
slug: api/dotsider.core.protocol.resolvedassemblyinfo
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Serialization-safe representation of an assembly resolution result.
Used in protocol and MCP responses where [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)
cannot be serialized directly because bundle and module results contain raw bytes.

```csharp
public sealed record ResolvedAssemblyInfo : IEquatable<ResolvedAssemblyInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ResolvedAssemblyInfo**

## Implements

- [IEquatable\<ResolvedAssemblyInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ResolvedAssemblyInfo(string, string?, string?, string?)

Serialization-safe representation of an assembly resolution result.
Used in protocol and MCP responses where [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)
cannot be serialized directly because bundle and module results contain raw bytes.

**Parameters:**

- `Kind` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Resolution kind: "file", "bundle", or "module".
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full file path for file- and module-backed results, or null for bundle-backed.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Entry name for bundle-backed results (e.g. "System.Runtime.dll"), or null.
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the containing bundle for bundle-backed results, or null.

```csharp
public ResolvedAssemblyInfo(string Kind, string? Path, string? Name, string? BundlePath)
```

## Properties

### BundlePath

Path to the containing bundle for bundle-backed results, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BundlePath { get; init; }
```

### Kind

Resolution kind: "file", "bundle", or "module".

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Kind { get; init; }
```

### Name

Entry name for bundle-backed results (e.g. "System.Runtime.dll"), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Name { get; init; }
```

### Path

Full file path for file- and module-backed results, or null for bundle-backed.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Path { get; init; }
```

## Methods

### Deconstruct(out string, out string?, out string?, out string?)

**Parameters:**

- `Kind` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Kind, out string? Path, out string? Name, out string? BundlePath)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ResolvedAssemblyInfo?)

**Parameters:**

- `other` ([ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ResolvedAssemblyInfo? other)
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

### operator !=(ResolvedAssemblyInfo?, ResolvedAssemblyInfo?)

**Parameters:**

- `left` ([ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/))
- `right` ([ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ResolvedAssemblyInfo? left, ResolvedAssemblyInfo? right)
```

### operator ==(ResolvedAssemblyInfo?, ResolvedAssemblyInfo?)

**Parameters:**

- `left` ([ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/))
- `right` ([ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ResolvedAssemblyInfo? left, ResolvedAssemblyInfo? right)
```
