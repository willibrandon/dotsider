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

### Resolve(string, string, string?, string?, string?, string?, NetFxBindingContext?, AssemblyAnalyzer?)

Resolves an assembly name to an assembly file, bundle entry, or authenticated sibling
module, falling back to the implementation assembly if the reference assembly has no IL.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the assembly that references the target.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly name to resolve.
- `declaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Optional declaring type for type-aware resolution (needed for mscorlib).
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker for shared framework probing.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Preferred runtime pack to probe first.
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): If the referencing assembly came from a bundle, the bundle path.
- `netFxBindingContext` ([NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)): Per-root .NET Framework binding context, or null for non-net48 roots.
When supplied alongside referencingAnalyzer, the resolver looks up the
matching [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/) in the referencing analyzer's metadata and routes
the bind through [NetFxBinder](/api/dotsider.core.analysis.netfxbinder/) for CLR-accurate framework probing. .NET Core
/ .NET 5+ callers pass null here and behavior is unchanged.
- `referencingAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the assembly that references the target, when available. Used together
with netFxBindingContext to recover the requested AssemblyRef's full
identity (version + culture + PKT) for the binder.

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

The resolved assembly or module, or null if not found.

```csharp
public static ResolvedAssembly? Resolve(string referencingAssemblyPath, string assemblyName, string? declaringType = null, string? targetFramework = null, string? preferredRuntimePack = null, string? sourceBundlePath = null, NetFxBindingContext? netFxBindingContext = null, AssemblyAnalyzer? referencingAnalyzer = null)
```
