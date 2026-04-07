---
title: "BundleEntry"
description: "Describes a single file entry within a .NET single-file bundle."
slug: api/dotsider.core.analysis.models.bundleentry
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Describes a single file entry within a .NET single-file bundle.

```csharp
public sealed record BundleEntry : IEquatable<BundleEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BundleEntry**

## Implements

- [IEquatable\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleEntry(long, long, long, BundleFileType, string)

Describes a single file entry within a .NET single-file bundle.

**Parameters:**

- `Offset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Byte offset of the entry within the bundle file.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Uncompressed size in bytes.
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Compressed size in bytes, or 0 if not compressed.
- `Type` ([BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)): The type of bundled file.
- `RelativePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path of the embedded file, relative to the bundle source directory.

```csharp
public BundleEntry(long Offset, long Size, long CompressedSize, BundleFileType Type, string RelativePath)
```

## Properties

### CompressedSize

Compressed size in bytes, or 0 if not compressed.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CompressedSize { get; init; }
```

### Offset

Byte offset of the entry within the bundle file.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Offset { get; init; }
```

### RelativePath

Path of the embedded file, relative to the bundle source directory.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RelativePath { get; init; }
```

### Size

Uncompressed size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### Type

The type of bundled file.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
public BundleFileType Type { get; init; }
```

