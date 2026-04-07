---
title: "DotNetRuntimeLocator"
description: "Discovers system .NET installations and resolves shared framework assembly paths."
slug: api/dotsider.core.analysis.dotnetruntimelocator
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Discovers system .NET installations and resolves shared framework assembly paths.

```csharp
public static class DotNetRuntimeLocator
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotNetRuntimeLocator**

## Methods

### FindAssemblyInSharedFramework(string, string?, string?)

Finds an assembly in the system .NET shared framework installation,
matching the closest runtime version to the target framework.

**Parameters:**

- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly name without extension (e.g. "System.Runtime").
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker (e.g. ".NETCoreApp,Version=v10.0"). Used for version matching.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): If specified, this runtime pack is probed first (e.g. "Microsoft.AspNetCore.App").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

Full path to the assembly, or `null` if not found.

```csharp
public static string? FindAssemblyInSharedFramework(string assemblyName, string? targetFramework, string? preferredRuntimePack = null)
```

