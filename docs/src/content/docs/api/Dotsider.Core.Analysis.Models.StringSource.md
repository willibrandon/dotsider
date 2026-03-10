---
title: "StringSource"
description: "Identifies the source from which a string was extracted."
slug: api/dotsider.core.analysis.models.stringsource
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Identifies the source from which a string was extracted.

```csharp
public enum StringSource
```

## Fields

### MetadataStrings

The #Strings metadata heap, containing identifier names used in metadata tables.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
MetadataStrings = 1
```

### RawBinary

Raw printable character sequences extracted directly from the binary.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
RawBinary = 2
```

### UserStrings

The #US (User Strings) metadata heap, containing string literals used in IL code.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
UserStrings = 0
```

