---
title: "NuGetPackageAnalyzer"
description: "Opens and analyzes a NuGet package (.nupkg) file. Reads package metadata from .nuspec and lists all contents."
slug: api/dotsider.core.analysis.nugetpackageanalyzer
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Opens and analyzes a NuGet package (.nupkg) file.
Reads package metadata from .nuspec and lists all contents.

```csharp
public sealed class NuGetPackageAnalyzer : IDisposable
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NuGetPackageAnalyzer**

## Implements

- [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

## Constructors

### NuGetPackageAnalyzer(string)

Opens and analyzes the specified NuGet package file.

**Parameters:**

- `nupkgPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the .nupkg file.

```csharp
public NuGetPackageAnalyzer(string nupkgPath)
```

## Properties

### Authors

The package authors from the .nuspec manifest, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Authors { get; }
```

### Description

The package description from the .nuspec manifest, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Description { get; }
```

### DllFiles

Only the DLL files in the package.

**Returns:** [IReadOnlyList\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NuGetFileEntry> DllFiles { get; }
```

### FileName

The file name of the .nupkg file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FileName { get; }
```

### FilePath

The full path to the .nupkg file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; }
```

### Files

All files in the package.

**Returns:** [IReadOnlyList\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NuGetFileEntry> Files { get; }
```

### PackageId

The NuGet package ID from the .nuspec manifest, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageId { get; }
```

### PackageVersion

The package version from the .nuspec manifest, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageVersion { get; }
```

## Methods

### Dispose()

Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

```csharp
public void Dispose()
```

### OpenDll(NuGetFileEntry)

Extracts a DLL from the package to a temp file and creates an AssemblyAnalyzer.

**Parameters:**

- `entry` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/)): 

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer OpenDll(NuGetFileEntry entry)
```

