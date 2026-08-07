---
title: "BundleEntryPayload"
description: "One single-file bundle manifest entry. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.bundleentrypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

One single-file bundle manifest entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleEntryPayload : IEquatable<BundleEntryPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BundleEntryPayload**

## Implements

- [IEquatable\<BundleEntryPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleEntryPayload(string, string, long, long)

One single-file bundle manifest entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `RelativePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Type` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public BundleEntryPayload(string RelativePath, string Type, long Size, long CompressedSize)
```

## Properties

### CompressedSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CompressedSize { get; init; }
```

### RelativePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RelativePath { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### Type

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Type { get; init; }
```

## Methods

### Deconstruct(out string, out string, out long, out long)

**Parameters:**

- `RelativePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Type` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out string RelativePath, out string Type, out long Size, out long CompressedSize)
```

### Equals(BundleEntryPayload?)

**Parameters:**

- `other` ([BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(BundleEntryPayload? other)
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

### operator !=(BundleEntryPayload?, BundleEntryPayload?)

**Parameters:**

- `left` ([BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/))
- `right` ([BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(BundleEntryPayload? left, BundleEntryPayload? right)
```

### operator ==(BundleEntryPayload?, BundleEntryPayload?)

**Parameters:**

- `left` ([BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/))
- `right` ([BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(BundleEntryPayload? left, BundleEntryPayload? right)
```
