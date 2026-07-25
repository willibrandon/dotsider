---
title: "NuGetDepsJsonResolver"
description: "Resolves assembly references by consulting the referencing assembly's .deps.json file to locate its NuGet dependencies in the NuGet global packages folder. This is the probe step that makes library projects work — dotnet build does not copy NuGet package assemblies next to a library's bin output, but the .deps.json manifest records the exact resolved package version and runtime asset path, matching what the .NET host uses at runtime. Manifest paths are treated as untrusted and must remain inside the selected package in the configured global packages folder."
slug: api/dotsider.core.analysis.nugetdepsjsonresolver
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves assembly references by consulting the referencing assembly's `.deps.json`
file to locate its NuGet dependencies in the NuGet global packages folder. This is the
probe step that makes library projects work — `dotnet build` does not copy NuGet
package assemblies next to a library's `bin` output, but the `.deps.json`
manifest records the exact resolved package version and runtime asset path, matching
what the .NET host uses at runtime. Manifest paths are treated as untrusted and must
remain inside the selected package in the configured global packages folder.

```csharp
public static class NuGetDepsJsonResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NuGetDepsJsonResolver**

## Methods

### TryResolve(string, string)

Attempts to locate assemblyName in the referencing assembly's
`.deps.json` manifest and resolve it against the NuGet global packages folder.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path of the assembly whose `.deps.json` is consulted.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Simple name of the assembly to locate (e.g. `Newtonsoft.Json`).

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

A [FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/) pointing at a contained packaged DLL, or
null when the dependency is absent or its manifest path is unsafe.

```csharp
public static ResolvedAssembly? TryResolve(string referencingAssemblyPath, string assemblyName)
```

