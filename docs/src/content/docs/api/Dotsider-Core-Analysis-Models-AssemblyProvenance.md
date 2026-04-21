---
title: "AssemblyProvenance"
description: "Describes how an assembly in the dependency graph was located — or why it could not be."
slug: api/dotsider.core.analysis.models.assemblyprovenance
sidebar:
  order: 1
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
runtime asset path.

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

