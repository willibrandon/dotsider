---
title: "ImplementationAssemblyResolver"
description: "Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation assemblies (e.g., System.Private.CoreLib) by probing for type forwarding."
slug: api/dotsider.core.analysis.implementationassemblyresolver
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation
assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.

```csharp
public static class ImplementationAssemblyResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ImplementationAssemblyResolver**

## Methods

### Resolve(string, string)

Resolves an assembly name to a path, falling back to the implementation assembly
if the reference assembly has no IL.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the assembly that references the target.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly name to resolve.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The resolved path, or null if not found.

```csharp
public static string? Resolve(string referencingAssemblyPath, string assemblyName)
```

