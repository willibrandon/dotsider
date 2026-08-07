---
title: "BundleInfoPayload"
description: "Single-file bundle identity and total content size. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.bundleinfopayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Single-file bundle identity and total content size.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleInfoPayload : IEquatable<BundleInfoPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BundleInfoPayload**

## Implements

- [IEquatable\<BundleInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleInfoPayload(bool, int?, int?, int?, string?, long?, string?)

Single-file bundle identity and total content size.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `IsBundle` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `MajorVersion` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MinorVersion` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `FileCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BundleId` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TotalSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public BundleInfoPayload(bool IsBundle, int? MajorVersion = null, int? MinorVersion = null, int? FileCount = null, string? BundleId = null, long? TotalSize = null, string? Error = null)
```

## Properties

### BundleId

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BundleId { get; init; }
```

### Error

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Error { get; init; }
```

### FileCount

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? FileCount { get; init; }
```

### IsBundle

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsBundle { get; init; }
```

### MajorVersion

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MajorVersion { get; init; }
```

### MinorVersion

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MinorVersion { get; init; }
```

### TotalSize

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? TotalSize { get; init; }
```

## Methods

### Deconstruct(out bool, out int?, out int?, out int?, out string?, out long?, out string?)

**Parameters:**

- `IsBundle` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `MajorVersion` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MinorVersion` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `FileCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BundleId` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TotalSize` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out bool IsBundle, out int? MajorVersion, out int? MinorVersion, out int? FileCount, out string? BundleId, out long? TotalSize, out string? Error)
```

### Equals(BundleInfoPayload?)

**Parameters:**

- `other` ([BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(BundleInfoPayload? other)
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

### operator !=(BundleInfoPayload?, BundleInfoPayload?)

**Parameters:**

- `left` ([BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/))
- `right` ([BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(BundleInfoPayload? left, BundleInfoPayload? right)
```

### operator ==(BundleInfoPayload?, BundleInfoPayload?)

**Parameters:**

- `left` ([BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/))
- `right` ([BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(BundleInfoPayload? left, BundleInfoPayload? right)
```
