---
title: "ReadyToRunSummary"
description: "Compact ReadyToRun image facts returned by assembly inspection. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.readytorunsummary
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Compact ReadyToRun image facts returned by assembly inspection.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ReadyToRunSummary : IEquatable<ReadyToRunSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunSummary**

## Implements

- [IEquatable\<ReadyToRunSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunSummary(string, int, int, bool, bool, bool, string, string?, int, int, long)

Compact ReadyToRun image facts returned by assembly inspection.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MajorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MinorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `IsComposite` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsComponent` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsPartialImage` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `OwnerCompositeExecutable` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PrecompiledMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `InstantiationCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TotalCodeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public ReadyToRunSummary(string Status, int MajorVersion, int MinorVersion, bool IsComposite, bool IsComponent, bool IsPartialImage, string Architecture, string? OwnerCompositeExecutable, int PrecompiledMethods, int InstantiationCount, long TotalCodeSize)
```

## Properties

### Architecture

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; init; }
```

### InstantiationCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int InstantiationCount { get; init; }
```

### IsComponent

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsComponent { get; init; }
```

### IsComposite

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsComposite { get; init; }
```

### IsPartialImage

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsPartialImage { get; init; }
```

### MajorVersion

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MajorVersion { get; init; }
```

### MinorVersion

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MinorVersion { get; init; }
```

### OwnerCompositeExecutable

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwnerCompositeExecutable { get; init; }
```

### PrecompiledMethods

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int PrecompiledMethods { get; init; }
```

### Status

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Status { get; init; }
```

### TotalCodeSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalCodeSize { get; init; }
```

## Methods

### Deconstruct(out string, out int, out int, out bool, out bool, out bool, out string, out string?, out int, out int, out long)

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MajorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MinorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `IsComposite` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsComponent` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsPartialImage` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `OwnerCompositeExecutable` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PrecompiledMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `InstantiationCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TotalCodeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out string Status, out int MajorVersion, out int MinorVersion, out bool IsComposite, out bool IsComponent, out bool IsPartialImage, out string Architecture, out string? OwnerCompositeExecutable, out int PrecompiledMethods, out int InstantiationCount, out long TotalCodeSize)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ReadyToRunSummary?)

**Parameters:**

- `other` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ReadyToRunSummary? other)
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

### operator !=(ReadyToRunSummary?, ReadyToRunSummary?)

**Parameters:**

- `left` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))
- `right` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ReadyToRunSummary? left, ReadyToRunSummary? right)
```

### operator ==(ReadyToRunSummary?, ReadyToRunSummary?)

**Parameters:**

- `left` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))
- `right` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ReadyToRunSummary? left, ReadyToRunSummary? right)
```
