---
title: "NativeAotInfoPayload"
description: "Native AOT identity and sidecar facts. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativeaotinfopayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Native AOT identity and sidecar facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotInfoPayload : IEquatable<NativeAotInfoPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotInfoPayload**

## Implements

- [IEquatable\<NativeAotInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAotInfoPayload(string, string, long, string, BinaryKind, NativeAotInfo?, int, int, int, int, int, NativeSymbolSource?, NativeSymbolStatus?, string?, bool, string?, string?, bool, PreIlcSummary?)

Native AOT identity and sidecar facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryKind` ([BinaryKind](/api/dotsider.core.analysis.models.binarykind/))
- `NativeAotInfo` ([NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/))
- `ReadyToRunSections` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredTypes` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FrozenStrings` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolSource` ([Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `NativeSymbolStatus` ([Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMstat` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `MstatFormat` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasDgml` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PreIlc` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))

```csharp
public NativeAotInfoPayload(string FilePath, string FileName, long FileSize, string Architecture, BinaryKind BinaryKind, NativeAotInfo? NativeAotInfo, int ReadyToRunSections, int RecoveredTypes, int RecoveredMethods, int FrozenStrings, int NativeSymbolCount, NativeSymbolSource? NativeSymbolSource, NativeSymbolStatus? NativeSymbolStatus, string? MstatPath, bool HasMstat, string? MstatFormat, string? DgmlPath, bool HasDgml, PreIlcSummary? PreIlc)
```

## Properties

### Architecture

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; init; }
```

### BinaryKind

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
public BinaryKind BinaryKind { get; init; }
```

### DgmlPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? DgmlPath { get; init; }
```

### FileName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FileName { get; init; }
```

### FilePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; init; }
```

### FileSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileSize { get; init; }
```

### FrozenStrings

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FrozenStrings { get; init; }
```

### HasDgml

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasDgml { get; init; }
```

### HasMstat

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasMstat { get; init; }
```

### MstatFormat

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MstatFormat { get; init; }
```

### MstatPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MstatPath { get; init; }
```

### NativeAotInfo

**Returns:** [NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/)

```csharp
public NativeAotInfo? NativeAotInfo { get; init; }
```

### NativeSymbolCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int NativeSymbolCount { get; init; }
```

### NativeSymbolSource

**Returns:** [Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public NativeSymbolSource? NativeSymbolSource { get; init; }
```

### NativeSymbolStatus

**Returns:** [Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public NativeSymbolStatus? NativeSymbolStatus { get; init; }
```

### PreIlc

**Returns:** [PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/)

```csharp
public PreIlcSummary? PreIlc { get; init; }
```

### ReadyToRunSections

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ReadyToRunSections { get; init; }
```

### RecoveredMethods

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RecoveredMethods { get; init; }
```

### RecoveredTypes

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RecoveredTypes { get; init; }
```

## Methods

### Deconstruct(out string, out string, out long, out string, out BinaryKind, out NativeAotInfo?, out int, out int, out int, out int, out int, out NativeSymbolSource?, out NativeSymbolStatus?, out string?, out bool, out string?, out string?, out bool, out PreIlcSummary?)

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryKind` ([BinaryKind](/api/dotsider.core.analysis.models.binarykind/))
- `NativeAotInfo` ([NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/))
- `ReadyToRunSections` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredTypes` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FrozenStrings` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolSource` ([Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `NativeSymbolStatus` ([Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMstat` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `MstatFormat` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasDgml` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PreIlc` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))

```csharp
public void Deconstruct(out string FilePath, out string FileName, out long FileSize, out string Architecture, out BinaryKind BinaryKind, out NativeAotInfo? NativeAotInfo, out int ReadyToRunSections, out int RecoveredTypes, out int RecoveredMethods, out int FrozenStrings, out int NativeSymbolCount, out NativeSymbolSource? NativeSymbolSource, out NativeSymbolStatus? NativeSymbolStatus, out string? MstatPath, out bool HasMstat, out string? MstatFormat, out string? DgmlPath, out bool HasDgml, out PreIlcSummary? PreIlc)
```

### Equals(NativeAotInfoPayload?)

**Parameters:**

- `other` ([NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeAotInfoPayload? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
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

### operator !=(NativeAotInfoPayload?, NativeAotInfoPayload?)

**Parameters:**

- `left` ([NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/))
- `right` ([NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeAotInfoPayload? left, NativeAotInfoPayload? right)
```

### operator ==(NativeAotInfoPayload?, NativeAotInfoPayload?)

**Parameters:**

- `left` ([NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/))
- `right` ([NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeAotInfoPayload? left, NativeAotInfoPayload? right)
```
