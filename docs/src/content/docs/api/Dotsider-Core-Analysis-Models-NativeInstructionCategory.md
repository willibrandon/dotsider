---
title: "NativeInstructionCategory"
description: "A coarse classification of a decoded instruction by function, for grouping, coloring, and summaries without inspecting the mnemonic string."
slug: api/dotsider.core.analysis.models.nativeinstructioncategory
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A coarse classification of a decoded instruction by function, for grouping, coloring, and
summaries without inspecting the mnemonic string.

```csharp
public enum NativeInstructionCategory
```

## Fields

### Control

Control flow (call, jmp, jcc, ret, branch).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Control = 1
```

### Crypto

Cryptographic / hash (AES, PCLMUL, SHA, CRC32).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Crypto = 5
```

### Float

Scalar floating point (x87, scalar SSE/AVX float, arm64 FP).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Float = 3
```

### Integer

Scalar integer / general-purpose data-processing (mov, add, cmp, lea, …).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Integer = 0
```

### System

System / privileged / runtime (nop, int3, ud2, fences, cpuid, barriers, mrs/msr).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
System = 4
```

### Unknown

A safety-net fallback (`.byte`/`.word`) for undefined or corrupt bytes.

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Unknown = 6
```

### Vector

Vector / SIMD (SSE–AVX-512, AdvSIMD, SVE).

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
Vector = 2
```

