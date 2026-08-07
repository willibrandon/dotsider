---
title: "WebcilInfo"
description: "Parsed provenance for a Webcil managed assembly, including whether it was wrapped inside a WebAssembly module. Webcil is a .NET metadata container used by browser-wasm publishes, so dotsider routes it through the managed metadata and IL experience."
slug: api/dotsider.core.analysis.models.webcilinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Parsed provenance for a Webcil managed assembly, including whether it was wrapped inside
a WebAssembly module. Webcil is a .NET metadata container used by browser-wasm publishes,
so dotsider routes it through the managed metadata and IL experience.

```csharp
public sealed record WebcilInfo : IEquatable<WebcilInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WebcilInfo**

## Implements

- [IEquatable\<WebcilInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WebcilInfo(int, int, bool, long, int, int, int)

Parsed provenance for a Webcil managed assembly, including whether it was wrapped inside
a WebAssembly module. Webcil is a .NET metadata container used by browser-wasm publishes,
so dotsider routes it through the managed metadata and IL experience.

**Parameters:**

- `VersionMajor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The Webcil major format version.
- `VersionMinor` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The Webcil minor format version.
- `IsWasmWrapped` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True when the Webcil payload was found inside a Wasm wrapper module.
- `PayloadOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The file offset of the Webcil payload in the opened file.
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of Webcil section records.
- `MetadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The size of the ECMA-335 metadata blob.
- `DebugDirectorySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The size of the Webcil debug directory, when present.

```csharp
public WebcilInfo(int VersionMajor, int VersionMinor, bool IsWasmWrapped, long PayloadOffset, int SectionCount, int MetadataSize, int DebugDirectorySize)
```

## Properties

### DebugDirectorySize

The size of the Webcil debug directory, when present.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DebugDirectorySize { get; init; }
```

### IsWasmWrapped

True when the Webcil payload was found inside a Wasm wrapper module.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsWasmWrapped { get; init; }
```

### MetadataSize

The size of the ECMA-335 metadata blob.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataSize { get; init; }
```

### PayloadOffset

The file offset of the Webcil payload in the opened file.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long PayloadOffset { get; init; }
```

### SectionCount

The number of Webcil section records.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

### VersionMajor

The Webcil major format version.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int VersionMajor { get; init; }
```

### VersionMinor

The Webcil minor format version.

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

### Equals(WebcilInfo?)

**Parameters:**

- `other` ([WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WebcilInfo? other)
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

### operator !=(WebcilInfo?, WebcilInfo?)

**Parameters:**

- `left` ([WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/))
- `right` ([WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WebcilInfo? left, WebcilInfo? right)
```

### operator ==(WebcilInfo?, WebcilInfo?)

**Parameters:**

- `left` ([WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/))
- `right` ([WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WebcilInfo? left, WebcilInfo? right)
```
