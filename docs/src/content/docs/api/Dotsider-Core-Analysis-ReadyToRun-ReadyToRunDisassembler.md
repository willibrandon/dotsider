---
title: "ReadyToRunDisassembler"
description: "Disassembles a precompiled ReadyToRun method by walking its code ranges and slicing each from the code image — which for a composite component is a different file than the metadata. Each range (hot entry, funclets, cold) is rendered as its own block, so a method with funclets or split hot/cold code shows every block rather than a single slice."
slug: api/dotsider.core.analysis.readytorun.readytorundisassembler
sidebar:
  order: 3
---

**Namespace:** `Dotsider.Core.Analysis.ReadyToRun`

**Assembly:** Dotsider.Core.dll

Disassembles a precompiled ReadyToRun method by walking its code ranges and slicing each from
the code image — which for a composite component is a different file than the metadata.
Each range (hot entry, funclets, cold) is rendered as its own block, so a method with funclets
or split hot/cold code shows every block rather than a single slice.

```csharp
public static class ReadyToRunDisassembler
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunDisassembler**

## Methods

### DisassembleMethod(AssemblyAnalyzer, ReadyToRunMethodEntry, Func\<ulong, string?\>?)

Disassembles entry's ranges from codeImage. Returns
null when no range is disassemblable, such as an unknown architecture or no file-backed
bytes, so callers can distinguish that state from "not precompiled".

**Parameters:**

- `codeImage` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer whose bytes hold the native code (self, or the owner composite).
- `entry` ([ReadyToRunMethodEntry](/api/dotsider.core.analysis.models.readytorunmethodentry/)): The method whose ranges to disassemble.
- `managedNameResolver` ([Func\<UInt64, String\>](https://learn.microsoft.com/dotnet/api/system.func-2)): Resolves a call/branch target VA to a managed name, or null.

**Returns:** [Nullable\<MethodDisassembly\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public static ReadyToRunDisassembler.MethodDisassembly? DisassembleMethod(AssemblyAnalyzer codeImage, ReadyToRunMethodEntry entry, Func<ulong, string?>? managedNameResolver)
```
