---
title: "NuGetPackageAnalyzer"
description: "Opens and analyzes a NuGet package (.nupkg) file. Reads package metadata from .nuspec and lists all contents."
slug: api/dotsider.core.analysis.nugetpackageanalyzer
sidebar:
  order: 0
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

The raw, untrusted package authors from the .nuspec manifest, or null. This value is not
display-safe.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Authors { get; }
```

### Description

The raw, untrusted package description from the .nuspec manifest, or null. This value is
not display-safe.

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

The raw, untrusted NuGet package ID from the .nuspec manifest, or null. This value is not
display-safe.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageId { get; }
```

### PackageVersion

The raw, untrusted package version from the .nuspec manifest, or null. This value is not
display-safe.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageVersion { get; }
```

## Methods

### Dispose()

```csharp
public void Dispose()
```

### OpenDll(NuGetFileEntry)

Extracts a DLL from the package into a private temporary directory and creates an analyzer.

**Parameters:**

- `entry` ([NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/)): The exact [NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/) instance returned by this analyzer.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

An analyzer for the selected DLL. Dispose it before disposing this package analyzer.

**Exceptions:**

- [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception): entry is null.
- [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception): entry was not returned by this analyzer or does not represent a DLL.
- [UnsafePackageEntryException](/api/dotsider.core.analysis.unsafepackageentryexception/): The package entry has an unsafe or ambiguous extraction path.
- [ObjectDisposedException](https://learn.microsoft.com/dotnet/api/system.objectdisposedexception): This analyzer has been disposed.
- [IOException](https://learn.microsoft.com/dotnet/api/system.io.ioexception): The DLL could not be extracted or read.
- [BadImageFormatException](https://learn.microsoft.com/dotnet/api/system.badimageformatexception): The extracted file has an invalid format.

```csharp
public AssemblyAnalyzer OpenDll(NuGetFileEntry entry)
```
