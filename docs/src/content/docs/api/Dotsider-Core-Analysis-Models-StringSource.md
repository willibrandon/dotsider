---
title: "StringSource"
description: "Identifies the source from which a string was extracted."
slug: api/dotsider.core.analysis.models.stringsource
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Identifies the source from which a string was extracted.

```csharp
public enum StringSource
```

## Fields

### FrozenObject

A frozen [String](https://learn.microsoft.com/dotnet/api/system.string) object recovered from a Native AOT binary's frozen
object region — the AOT counterpart of the #US heap.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
FrozenObject = 4
```

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

### RawBinaryUtf16

UTF-16LE printable character sequences extracted directly from the binary.
This is how managed string literals freeze in Native AOT images.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
RawBinaryUtf16 = 3
```

### UserStrings

The #US (User Strings) metadata heap, containing string literals used in IL code.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
UserStrings = 0
```
