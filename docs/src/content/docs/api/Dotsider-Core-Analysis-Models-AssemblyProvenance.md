---
title: "AssemblyProvenance"
description: "Describes how an assembly in the dependency graph was located — or why it could not be."
slug: api/dotsider.core.analysis.models.assemblyprovenance
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Describes how an assembly in the dependency graph was located — or why it could not be.

```csharp
public enum AssemblyProvenance
```

## Fields

### AdjacentBundle

Extracted from a single-file bundle adjacent to the referencing assembly.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
AdjacentBundle = 7
```

### AppLocal

Resolved from the referencing assembly's directory on disk.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
AppLocal = 1
```

### CodeBase

Resolved by following a configured `&lt;codeBase href&gt;` entry from the .NET
Framework binding policy chain (app config, publisher policy, or machine.config).

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
CodeBase = 12
```

### CodeBaseMissing

A `&lt;codeBase&gt;` entry for the effective identity was present in the binding
policy chain but its href pointed at a path that does not exist on disk. Reported as
fail-fast (the CLR does not fall back to probing in this case), distinct from generic
[Unresolved](/api/dotsider.core.analysis.models.assemblyprovenance.unresolved/) so the UI can surface the configured href to the user.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
CodeBaseMissing = 13
```

### CompiledIntoNativeImage

Compiled into a Native AOT image: the node comes from the binary's mstat size report
or native import table rather than an on-disk assembly, so there is no file to open.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
CompiledIntoNativeImage = 14
```

### FrameworkRuntimeDirectory

Resolved from the .NET Framework runtime directory at
`%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319`. Distinct from
[RuntimeDirectory](/api/dotsider.core.analysis.models.assemblyprovenance.runtimedirectory/), which references the active .NET (Core) host directory
the analyzer process is itself running on.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
FrameworkRuntimeDirectory = 11
```

### Gac

Resolved from the .NET Framework Global Assembly Cache at
`%WINDIR%\Microsoft.NET\assembly\GAC_*`. Only produced for .NET Framework roots.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
Gac = 10
```

### HostBundle

Extracted from the host process bundle (when dotsider itself is bundled).

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
HostBundle = 6
```

### IdentityMismatch

A probe produced a file whose simple name matched, but whose manifest identity
(version, culture, or public key token) did not match the requested reference.
The graph does not expand from such candidates — the node is left as an unresolved leaf.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
IdentityMismatch = 9
```

### NuGetPackageCache

Resolved from the NuGet global packages folder by consulting the referencing
assembly's `.deps.json` manifest for the exact resolved package version and
runtime asset path after both paths are contained beneath the selected package.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
NuGetPackageCache = 3
```

### Root

The analyzed assembly itself (the graph root).

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
Root = 0
```

### RuntimeDirectory

Resolved from the active .NET runtime directory.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
RuntimeDirectory = 2
```

### SharedFramework

Resolved through the shared-framework discovery for the target framework.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
SharedFramework = 4
```

### SourceBundle

Extracted from the single-file bundle that produced the referencing assembly.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
SourceBundle = 5
```

### Unresolved

No probe produced any candidate file for the referenced simple name.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
Unresolved = 8
```

## Remarks

The order of enum members is not significant; callers should compare by member name.
