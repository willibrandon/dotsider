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

### Resolve(string, string, string?, string?, string?, string?)

Resolves an assembly name to a path or bundle entry, falling back to the
implementation assembly if the reference assembly has no IL.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the assembly that references the target.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly name to resolve.
- `declaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Optional declaring type for type-aware resolution (needed for mscorlib).
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker for shared framework probing.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Preferred runtime pack to probe first.
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): If the referencing assembly came from a bundle, the bundle path.

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

The resolved assembly, or null if not found.

```csharp
public static ResolvedAssembly? Resolve(string referencingAssemblyPath, string assemblyName, string? declaringType = null, string? targetFramework = null, string? preferredRuntimePack = null, string? sourceBundlePath = null)
```

