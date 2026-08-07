---
title: "MstatSource"
description: "A resolved mstat input: the decoded report plus where it came from. Produced by MstatLocator from either a bare .mstat file or a Native AOT binary with a size-report sidecar."
slug: api/dotsider.core.analysis.models.mstatsource
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A resolved mstat input: the decoded report plus where it came from. Produced by
[MstatLocator](/api/dotsider.core.analysis.mstatlocator/) from either a bare `.mstat` file or
a Native AOT binary with a size-report sidecar.

```csharp
public sealed record MstatSource : IEquatable<MstatSource>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatSource**

## Implements

- [IEquatable\<MstatSource\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatSource(MstatData, string, string?, long?, string?)

A resolved mstat input: the decoded report plus where it came from. Produced by
[MstatLocator](/api/dotsider.core.analysis.mstatlocator/) from either a bare `.mstat` file or
a Native AOT binary with a size-report sidecar.

**Parameters:**

- `Data` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The decoded size report.
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the `.mstat` file the report was read from.
- `BinaryPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The Native AOT binary the report describes, or null when the input was a bare `.mstat`.
- `BinaryFileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The binary's size on disk in bytes, or null when the input was a bare `.mstat`.
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The ILC dependency graph (DGML) found beside the input, or null when none exists — "why is this in my binary" needs it.

```csharp
public MstatSource(MstatData Data, string MstatPath, string? BinaryPath, long? BinaryFileSize, string? DgmlPath)
```

## Properties

### BinaryFileSize

The binary's size on disk in bytes, or null when the input was a bare `.mstat`.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? BinaryFileSize { get; init; }
```

### BinaryPath

The Native AOT binary the report describes, or null when the input was a bare `.mstat`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BinaryPath { get; init; }
```

### Data

The decoded size report.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

```csharp
public MstatData Data { get; init; }
```

### DgmlPath

The ILC dependency graph (DGML) found beside the input, or null when none exists — "why is this in my binary" needs it.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? DgmlPath { get; init; }
```

### MstatPath

The path of the `.mstat` file the report was read from.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string MstatPath { get; init; }
```

## Methods

### Deconstruct(out MstatData, out string, out string?, out long?, out string?)

**Parameters:**

- `Data` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))
- `MstatPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BinaryFileSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `DgmlPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out MstatData Data, out string MstatPath, out string? BinaryPath, out long? BinaryFileSize, out string? DgmlPath)
```

### Equals(MstatSource?)

**Parameters:**

- `other` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatSource? other)
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

### operator !=(MstatSource?, MstatSource?)

**Parameters:**

- `left` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))
- `right` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatSource? left, MstatSource? right)
```

### operator ==(MstatSource?, MstatSource?)

**Parameters:**

- `left` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))
- `right` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatSource? left, MstatSource? right)
```
