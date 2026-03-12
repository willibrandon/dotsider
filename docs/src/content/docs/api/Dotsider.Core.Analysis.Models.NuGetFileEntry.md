---
title: "NuGetFileEntry"
description: "Represents a file entry within a NuGet package (.nupkg)."
slug: api/dotsider.core.analysis.models.nugetfileentry
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

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): File name without directory path.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path of the entry within the package archive.
- `Directory` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Directory portion of the entry path.
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Compressed size in bytes inside the .nupkg.
- `UncompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Uncompressed size in bytes.
- `IsDll` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the entry is a .NET assembly (.dll).

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

Directory portion of the entry path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Directory { get; init; }
```

### FullPath

Full path of the entry within the package archive.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### IsDll

Whether the entry is a .NET assembly (.dll).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsDll { get; init; }
```

### Name

File name without directory path.

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

