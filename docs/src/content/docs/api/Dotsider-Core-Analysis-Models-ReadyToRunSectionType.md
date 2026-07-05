---
title: "ReadyToRunSectionType"
description: "The ReadyToRunSectionType ids from a crossgen2 image's READYTORUN_SECTION table (readytorun.h). Distinct from the Native AOT module-section ids (200–399) that RtrSection names; a classic R2R section table uses these 100-range ids with a 12-byte {Type, RVA, Size} row layout."
slug: api/dotsider.core.analysis.models.readytorunsectiontype
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The `ReadyToRunSectionType` ids from a crossgen2 image's `READYTORUN_SECTION` table
(`readytorun.h`). Distinct from the Native AOT module-section ids (200–399) that
[RtrSection](/api/dotsider.core.analysis.models.rtrsection/) names; a classic R2R section table uses these 100-range ids with a
12-byte `{Type, RVA, Size}` row layout.

```csharp
public enum ReadyToRunSectionType
```

## Fields

### AttributePresence

Custom-attribute presence bitmap.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
AttributePresence = 113
```

### AvailableTypes

A NativeHashtable of the types available in this image.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
AvailableTypes = 108
```

### CompilerIdentifier

Compiler identifier string.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
CompilerIdentifier = 100
```

### ComponentAssemblies

The composite image's component-assembly table.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ComponentAssemblies = 115
```

### CrossModuleInlineInfo

Cross-module inline info.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
CrossModuleInlineInfo = 119
```

### DebugInfo

Per-method debug info (bounds and variable locations).

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
DebugInfo = 105
```

### DelayLoadMethodCallThunks

Delay-load method call thunks.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
DelayLoadMethodCallThunks = 106
```

### EnclosingTypeMap

Map of enclosing types.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
EnclosingTypeMap = 122
```

### ExceptionInfo

Per-method exception-handling clause info.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ExceptionInfo = 104
```

### HotColdMap

Hot/cold runtime-function pairs for split method bodies.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
HotColdMap = 120
```

### ImportSections

Import sections describing lazily-resolved fixup cells.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ImportSections = 101
```

### InliningInfo

Inlining info (deprecated form).

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
InliningInfo = 110
```

### InliningInfo2

Inlining info (current form).

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
InliningInfo2 = 114
```

### InstanceMethodEntryPoints

A NativeHashtable mapping instantiated generic methods to runtime functions.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
InstanceMethodEntryPoints = 109
```

### ManifestAssemblyMvids

MVIDs for the manifest assemblies, used to validate component identity.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ManifestAssemblyMvids = 118
```

### ManifestMetadata

Manifest metadata blob listing the version-bubble assembly references.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ManifestMetadata = 112
```

### MethodDefEntryPoints

A NativeArray mapping each MethodDef rid to its first runtime function.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
MethodDefEntryPoints = 103
```

### MethodIsGenericMap

Map of which methods are generic.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
MethodIsGenericMap = 121
```

### OwnerCompositeExecutable

The filename of the owner composite executable that holds a component's code.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
OwnerCompositeExecutable = 116
```

### PgoInstrumentationData

PGO instrumentation data.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
PgoInstrumentationData = 117
```

### ProfileDataInfo

Profile (PGO) data info.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
ProfileDataInfo = 111
```

### RuntimeFunctions

The runtime-function (pdata-style) table of precompiled code ranges.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
RuntimeFunctions = 102
```

### TypeGenericInfoMap

Map of per-type generic info.

**Returns:** [ReadyToRunSectionType](/api/dotsider.core.analysis.models.readytorunsectiontype/)

```csharp
TypeGenericInfoMap = 123
```

