---
title: "BinaryKind"
description: "Coarse classification of an analyzed binary."
slug: api/dotsider.core.analysis.models.binarykind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Coarse classification of an analyzed binary.

```csharp
public enum BinaryKind
```

## Fields

### Managed

A managed assembly with ECMA-335 metadata.

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
Managed = 0
```

### Native

A native binary with no CLR metadata and no ReadyToRun header (apphost, unknown format).

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
Native = 3
```

### NativeAot

A Native AOT compiled .NET binary: a native executable with no CLR metadata
whose image embeds a validated ReadyToRun header.

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
NativeAot = 2
```

### ReadyToRun

A crossgen2 ReadyToRun image: full ECMA-335 metadata plus precompiled native method
bodies (non-composite, composite, or a composite component). Every managed tab works;
the native bodies are additionally correlated to their managed methods.

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
ReadyToRun = 1
```

