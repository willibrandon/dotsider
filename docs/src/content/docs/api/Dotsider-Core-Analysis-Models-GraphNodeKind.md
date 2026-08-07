---
title: "GraphNodeKind"
description: "What a dependency-graph node represents. Managed graphs contain only assemblies; the Native AOT graph adds the binary's native import modules."
slug: api/dotsider.core.analysis.models.graphnodekind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a dependency-graph node represents. Managed graphs contain only assemblies; the
Native AOT graph adds the binary's native import modules.

```csharp
public enum GraphNodeKind
```

## Fields

### Assembly

A managed assembly, identified by its full assembly identity.

**Returns:** [GraphNodeKind](/api/dotsider.core.analysis.models.graphnodekind/)

```csharp
Assembly = 0
```

### NativeImport

A native module the binary imports (for example `kernel32.dll`).

**Returns:** [GraphNodeKind](/api/dotsider.core.analysis.models.graphnodekind/)

```csharp
NativeImport = 1
```
