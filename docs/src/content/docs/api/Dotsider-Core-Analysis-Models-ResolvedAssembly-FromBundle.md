---
title: "ResolvedAssembly.FromBundle"
description: "The assembly was found inside a single-file bundle."
slug: api/dotsider.core.analysis.models.resolvedassembly.frombundle
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The assembly was found inside a single-file bundle.

```csharp
public sealed record ResolvedAssembly.FromBundle : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromBundle>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/) → **ResolvedAssembly.FromBundle**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<FromBundle\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FromBundle(byte[], string, string)

The assembly was found inside a single-file bundle.

**Parameters:**

- `Bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): The raw assembly bytes extracted from the bundle.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly file name (e.g. "System.Runtime.dll").
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the bundle file that contains this assembly.

```csharp
public FromBundle(byte[] Bytes, string Name, string BundlePath)
```

## Properties

### BundlePath

Full path to the bundle file that contains this assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string BundlePath { get; init; }
```

### Bytes

The raw assembly bytes extracted from the bundle.

**Returns:** [Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])

```csharp
public byte[] Bytes { get; init; }
```

### Name

The assembly file name (e.g. "System.Runtime.dll").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

