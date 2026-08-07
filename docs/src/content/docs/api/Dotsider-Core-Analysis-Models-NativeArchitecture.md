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

### Arm32

ARM 32-bit (Thumb-2). Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Arm32 = 4
```

### Arm64

AArch64 (ARM64). Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Arm64 = 2
```

### LoongArch64

LoongArch 64-bit. Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
LoongArch64 = 6
```

### RiscV64

RISC-V 64-bit. Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
RiscV64 = 5
```

### Unknown

The architecture could not be determined (managed or unrecognized image).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Unknown = 0
```

### Wasm32

WebAssembly 32-bit. Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
Wasm32 = 7
```

### X64

x86-64 (AMD64 / Intel 64). Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
X64 = 1
```

### X86

x86 (32-bit). Disassembly supported.

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
X86 = 3
```
