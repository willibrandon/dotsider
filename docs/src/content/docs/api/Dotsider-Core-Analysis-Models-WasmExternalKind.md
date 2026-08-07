---
title: "WasmExternalKind"
description: "The external item kind used by WebAssembly import and export sections."
slug: api/dotsider.core.analysis.models.wasmexternalkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The external item kind used by WebAssembly import and export sections.

```csharp
public enum WasmExternalKind
```

## Fields

### Function

A function index.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Function = 0
```

### Global

A global index.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Global = 3
```

### Memory

A memory index.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Memory = 2
```

### Table

A table index.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Table = 1
```

### Tag

An exception tag index.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Tag = 4
```

### Unknown

An unrecognized external kind.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
Unknown = 5
```
