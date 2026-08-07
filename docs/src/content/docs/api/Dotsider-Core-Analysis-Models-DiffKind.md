---
title: "DiffKind"
description: "Describes the kind of difference detected between two assembly elements."
slug: api/dotsider.core.analysis.models.diffkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Describes the kind of difference detected between two assembly elements.

```csharp
public enum DiffKind
```

## Fields

### Added

The element exists only in the right (newer) assembly.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
Added = 0
```

### Changed

The element exists in both assemblies but has been modified.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
Changed = 2
```

### Removed

The element exists only in the left (older) assembly.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
Removed = 1
```

### Unchanged

The element is identical in both assemblies.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
Unchanged = 3
```
