---
title: "Dotsider.Core.Analysis.ReadyToRun"
slug: api/dotsider.core.analysis.readytorun
sidebar:
  order: 3
  attrs:
    data-api-namespace: "true"
---

## Classes

### [ReadyToRunDisassembler](/api/dotsider.core.analysis.readytorun.readytorundisassembler/)

Disassembles a precompiled ReadyToRun method by walking its code ranges and slicing each from
the code image — which for a composite component is a different file than the metadata.
Each range (hot entry, funclets, cold) is rendered as its own block, so a method with funclets
or split hot/cold code shows every block rather than a single slice.

```csharp
public static class ReadyToRunDisassembler
```

## Structs

### [ReadyToRunDisassembler.MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/)

The result of disassembling a method across its ranges.

```csharp
public readonly record struct ReadyToRunDisassembler.MethodDisassembly : IEquatable<ReadyToRunDisassembler.MethodDisassembly>
```

