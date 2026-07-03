---
title: "MstatManifestResource"
description: "One embedded manifest resource from an ILC size report (format 2.1+). For back-compat these bytes are also summed into the ResourceData blob entry."
slug: api/dotsider.core.analysis.models.mstatmanifestresource
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One embedded manifest resource from an ILC size report (format 2.1+). For back-compat
these bytes are also summed into the `ResourceData` blob entry.

```csharp
public sealed record MstatManifestResource : IEquatable<MstatManifestResource>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatManifestResource**

## Implements

- [IEquatable\<MstatManifestResource\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatManifestResource(string, string, int)

One embedded manifest resource from an ILC size report (format 2.1+). For back-compat
these bytes are also summed into the `ResourceData` blob entry.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly the resource was embedded in.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The resource name.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The resource size in bytes.

```csharp
public MstatManifestResource(string AssemblyName, string Name, int Size)
```

## Properties

### AssemblyName

The simple name of the assembly the resource was embedded in.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Name

The resource name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

The resource size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

