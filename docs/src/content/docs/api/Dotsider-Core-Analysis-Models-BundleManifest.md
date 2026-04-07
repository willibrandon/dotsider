---
title: "BundleManifest"
description: "The parsed manifest header of a .NET single-file bundle."
slug: api/dotsider.core.analysis.models.bundlemanifest
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The parsed manifest header of a .NET single-file bundle.

```csharp
public sealed record BundleManifest : IEquatable<BundleManifest>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BundleManifest**

## Implements

- [IEquatable\<BundleManifest\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleManifest(uint, uint, int, string, IReadOnlyList\<BundleEntry\>)

The parsed manifest header of a .NET single-file bundle.

**Parameters:**

- `MajorVersion` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): Bundle format major version (1-6).
- `MinorVersion` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): Bundle format minor version.
- `FileCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of files embedded in the bundle.
- `BundleId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Unique identifier for this bundle.
- `Entries` ([IReadOnlyList\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The list of file entries in the bundle.

```csharp
public BundleManifest(uint MajorVersion, uint MinorVersion, int FileCount, string BundleId, IReadOnlyList<BundleEntry> Entries)
```

## Properties

### BundleId

Unique identifier for this bundle.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string BundleId { get; init; }
```

### Entries

The list of file entries in the bundle.

**Returns:** [IReadOnlyList\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BundleEntry> Entries { get; init; }
```

### FileCount

Number of files embedded in the bundle.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FileCount { get; init; }
```

### MajorVersion

Bundle format major version (1-6).

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint MajorVersion { get; init; }
```

### MinorVersion

Bundle format minor version.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint MinorVersion { get; init; }
```

