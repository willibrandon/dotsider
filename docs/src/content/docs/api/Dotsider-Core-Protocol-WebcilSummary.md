---
title: "WebcilSummary"
description: "Compact Webcil container facts. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.webcilsummary
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Compact Webcil container facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WebcilSummary : IEquatable<WebcilSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WebcilSummary**

## Implements

- [IEquatable\<WebcilSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WebcilSummary(int, int, bool, long, int, int, int)

Compact Webcil container facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `VersionMajor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `VersionMinor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `IsWasmWrapped` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PayloadOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MetadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DebugDirectorySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public WebcilSummary(int VersionMajor, int VersionMinor, bool IsWasmWrapped, long PayloadOffset, int SectionCount, int MetadataSize, int DebugDirectorySize)
```

## Properties

### DebugDirectorySize

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DebugDirectorySize { get; init; }
```

### IsWasmWrapped

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsWasmWrapped { get; init; }
```

### MetadataSize

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataSize { get; init; }
```

### PayloadOffset

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long PayloadOffset { get; init; }
```

### SectionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

### VersionMajor

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int VersionMajor { get; init; }
```

### VersionMinor

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int VersionMinor { get; init; }
```

## Methods

### Deconstruct(out int, out int, out bool, out long, out int, out int, out int)

**Parameters:**

- `VersionMajor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `VersionMinor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `IsWasmWrapped` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PayloadOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MetadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DebugDirectorySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out int VersionMajor, out int VersionMinor, out bool IsWasmWrapped, out long PayloadOffset, out int SectionCount, out int MetadataSize, out int DebugDirectorySize)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WebcilSummary?)

**Parameters:**

- `other` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WebcilSummary? other)
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

### operator !=(WebcilSummary?, WebcilSummary?)

**Parameters:**

- `left` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))
- `right` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WebcilSummary? left, WebcilSummary? right)
```

### operator ==(WebcilSummary?, WebcilSummary?)

**Parameters:**

- `left` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))
- `right` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WebcilSummary? left, WebcilSummary? right)
```
