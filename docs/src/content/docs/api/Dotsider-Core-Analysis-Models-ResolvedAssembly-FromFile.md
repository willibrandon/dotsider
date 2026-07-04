---
title: "ResolvedAssembly.FromFile"
description: "The assembly was found as a file on disk."
slug: api/dotsider.core.analysis.models.resolvedassembly.fromfile
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The assembly was found as a file on disk.

```csharp
public sealed record ResolvedAssembly.FromFile : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromFile>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/) → **ResolvedAssembly.FromFile**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<FromFile\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FromFile(string)

The assembly was found as a file on disk.

**Parameters:**

- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the assembly file.

```csharp
public FromFile(string Path)
```

## Properties

### Path

Full path to the assembly file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Path { get; init; }
```

