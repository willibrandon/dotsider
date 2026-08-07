---
title: "MstatSourceSummaryPayload"
description: "Summary of an mstat source and its matching binary. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatsourcesummarypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Summary of an mstat source and its matching binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatSourceSummaryPayload : IEquatable<MstatSourceSummaryPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatSourceSummaryPayload**

## Implements

- [IEquatable\<MstatSourceSummaryPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatSourceSummaryPayload(string, string?, long?, string, string?, string, long, long?, int)

Summary of an mstat source and its matching binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryFileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Format` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MstatTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public MstatSourceSummaryPayload(string Target, string? BinaryPath, long? BinaryFileSize, string MstatPath, string? DgmlPath, string Format, long MstatTotal, long? FileSize, int EntryCount)
```

## Properties

### BinaryFileSize

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? BinaryFileSize { get; init; }
```

### BinaryPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BinaryPath { get; init; }
```

### DgmlPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? DgmlPath { get; init; }
```

### EntryCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryCount { get; init; }
```

### FileSize

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? FileSize { get; init; }
```

### Format

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Format { get; init; }
```

### MstatPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string MstatPath { get; init; }
```

### MstatTotal

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long MstatTotal { get; init; }
```

### Target

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Target { get; init; }
```

## Methods

### Deconstruct(out string, out string?, out long?, out string, out string?, out string, out long, out long?, out int)

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryFileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Format` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MstatTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out string Target, out string? BinaryPath, out long? BinaryFileSize, out string MstatPath, out string? DgmlPath, out string Format, out long MstatTotal, out long? FileSize, out int EntryCount)
```

### Equals(MstatSourceSummaryPayload?)

**Parameters:**

- `other` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatSourceSummaryPayload? other)
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

### operator !=(MstatSourceSummaryPayload?, MstatSourceSummaryPayload?)

**Parameters:**

- `left` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `right` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatSourceSummaryPayload? left, MstatSourceSummaryPayload? right)
```

### operator ==(MstatSourceSummaryPayload?, MstatSourceSummaryPayload?)

**Parameters:**

- `left` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `right` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatSourceSummaryPayload? left, MstatSourceSummaryPayload? right)
```
