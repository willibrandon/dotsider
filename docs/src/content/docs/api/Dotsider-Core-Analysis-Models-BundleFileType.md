---
title: "BundleFileType"
description: "Identifies the type of file embedded in a .NET single-file bundle."
slug: api/dotsider.core.analysis.models.bundlefiletype
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Identifies the type of file embedded in a .NET single-file bundle.

```csharp
public enum BundleFileType : byte
```

## Fields

### Assembly

IL and R2R assemblies.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
Assembly = 1
```

### DepsJson

The .deps.json configuration file.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
DepsJson = 3
```

### NativeBinary

Native binaries.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
NativeBinary = 2
```

### RuntimeConfigJson

The .runtimeconfig.json configuration file.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
RuntimeConfigJson = 4
```

### Symbols

PDB symbol files.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
Symbols = 5
```

### Unknown

Type not determined.

**Returns:** [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

```csharp
Unknown = 0
```

