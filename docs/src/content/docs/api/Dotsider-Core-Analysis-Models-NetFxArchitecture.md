---
title: "NetFxArchitecture"
description: "Effective process bitness for a .NET Framework root assembly. Models actual runtime architecture, not the PE's compile-time descriptor — AnyCPU is a compile-time attribute that resolves to host bitness at load time, so there is no MSIL runtime arch."
slug: api/dotsider.core.analysis.models.netfxarchitecture
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Effective process bitness for a .NET Framework root assembly. Models actual runtime
architecture, not the PE's compile-time descriptor — AnyCPU is a compile-time attribute
that resolves to host bitness at load time, so there is no `MSIL` runtime arch.

```csharp
public enum NetFxArchitecture
```

## Fields

### Amd64

64-bit (amd64) process.

**Returns:** [NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)

```csharp
Amd64 = 1
```

### X86

32-bit (x86) process.

**Returns:** [NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)

```csharp
X86 = 0
```

