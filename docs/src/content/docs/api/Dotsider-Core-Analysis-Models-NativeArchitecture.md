---
title: "NativeArchitecture"
description: "The instruction-set architecture a native code window is decoded as. Carried on NativeSymbolInfo from the real image (or the selected fat-Mach-O slice) so the disassembler never has to guess from an ambiguous machine string."
slug: api/dotsider.core.analysis.models.nativearchitecture
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The instruction-set architecture a native code window is decoded as. Carried on
[NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/) from the real image (or the selected fat-Mach-O slice) so the
disassembler never has to guess from an ambiguous machine string.

```csharp
public enum NativeArchitecture
```

## Fields

### Arm64

AArch64 (ARM64).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Arm64 = 2
```

### Unknown

The architecture could not be determined (managed or unrecognized image).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Unknown = 0
```

### X64

x86-64 (AMD64 / Intel 64).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
X64 = 1
```

