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

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `Directory` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `CompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `UncompressedSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `IsDll` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): 

```csharp
public NuGetFileEntry(string Name, string FullPath, string Directory, long CompressedSize, long UncompressedSize, bool IsDll)
```

## Properties

### CompressedSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CompressedSize { get; init; }
```

### Directory

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Directory { get; init; }
```

### FullPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### IsDll

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsDll { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### UncompressedSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long UncompressedSize { get; init; }
```

