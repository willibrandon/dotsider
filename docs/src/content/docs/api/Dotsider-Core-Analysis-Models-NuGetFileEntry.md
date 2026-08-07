---
title: "NuGetFileEntry"
description: "Represents a file entry within a NuGet package (.nupkg)."
slug: api/dotsider.core.analysis.models.nugetfileentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Represents a file entry within a NuGet package (.nupkg).

```csharp
public sealed record NuGetFileEntry : IEquatable<NuGetFileEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NuGetFileEntry**

## Implements

- [IEquatable\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NuGetFileEntry(string, string, string, long, long, bool)

Represents a file entry within a NuGet package (.nupkg).

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Raw, untrusted file-name portion of the archive entry path. This value is not display-safe.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Raw, untrusted path of the entry within the package archive. This value is not a filesystem
path and must be validated before extraction.
- `Directory` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Raw, untrusted directory portion of the archive entry path, normalized only to use forward
slashes. This value is not a filesystem-safe path or display-safe text.
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Compressed size in bytes inside the .nupkg.
- `UncompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Uncompressed size in bytes.
- `IsDll` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the archive entry name has a .dll file extension.

```csharp
public NuGetFileEntry(string Name, string FullPath, string Directory, long CompressedSize, long UncompressedSize, bool IsDll)
```

## Properties

### CompressedSize

Compressed size in bytes inside the .nupkg.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CompressedSize { get; init; }
```

### Directory

Raw, untrusted directory portion of the archive entry path, normalized only to use forward
slashes. This value is not a filesystem-safe path or display-safe text.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Directory { get; init; }
```

### FullPath

Raw, untrusted path of the entry within the package archive. This value is not a filesystem
path and must be validated before extraction.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### IsDll

Whether the archive entry name has a .dll file extension.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsDll { get; init; }
```

### Name

Raw, untrusted file-name portion of the archive entry path. This value is not display-safe.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### UncompressedSize

Uncompressed size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long UncompressedSize { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out long, out long, out bool)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Directory` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `UncompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `IsDll` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out string Name, out string FullPath, out string Directory, out long CompressedSize, out long UncompressedSize, out bool IsDll)
```

### Equals(NuGetFileEntry?)

**Parameters:**

- `other` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NuGetFileEntry? other)
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

### operator !=(NuGetFileEntry?, NuGetFileEntry?)

**Parameters:**

- `left` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/))
- `right` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NuGetFileEntry? left, NuGetFileEntry? right)
```

### operator ==(NuGetFileEntry?, NuGetFileEntry?)

**Parameters:**

- `left` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/))
- `right` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NuGetFileEntry? left, NuGetFileEntry? right)
```
