---
title: "FrameworkAssemblyInfo"
description: "Result of resolving an assembly from the system .NET shared framework. Includes the full path and the runtime pack that provided it."
slug: api/dotsider.core.protocol.frameworkassemblyinfo
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Result of resolving an assembly from the system .NET shared framework.
Includes the full path and the runtime pack that provided it.

```csharp
public sealed record FrameworkAssemblyInfo : IEquatable<FrameworkAssemblyInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **FrameworkAssemblyInfo**

## Implements

- [IEquatable\<FrameworkAssemblyInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FrameworkAssemblyInfo(string, string)

Result of resolving an assembly from the system .NET shared framework.
Includes the full path and the runtime pack that provided it.

**Parameters:**

- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the resolved assembly file.
- `RuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The shared framework pack the assembly was found in (e.g. "Microsoft.NETCore.App").

```csharp
public FrameworkAssemblyInfo(string Path, string RuntimePack)
```

## Properties

### Path

Full path to the resolved assembly file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Path { get; init; }
```

### RuntimePack

The shared framework pack the assembly was found in (e.g. "Microsoft.NETCore.App").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RuntimePack { get; init; }
```

