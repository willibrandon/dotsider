---
title: "WasmSymbolMapStatus"
description: "The outcome of probing a WebAssembly module's dotnet.native.js.symbols sidecar."
slug: api/dotsider.core.analysis.models.wasmsymbolmapstatus
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of probing a WebAssembly module's `dotnet.native.js.symbols` sidecar.

```csharp
public enum WasmSymbolMapStatus
```

## Fields

### Corrupt

The sidecar was found but no valid entries could be read.

**Returns:** [WasmSymbolMapStatus](/api/dotsider.core.analysis.models.wasmsymbolmapstatus/)

```csharp
Corrupt = 2
```

### Loaded

The sidecar was found and parsed.

**Returns:** [WasmSymbolMapStatus](/api/dotsider.core.analysis.models.wasmsymbolmapstatus/)

```csharp
Loaded = 1
```

### Missing

No sidecar was expected or found.

**Returns:** [WasmSymbolMapStatus](/api/dotsider.core.analysis.models.wasmsymbolmapstatus/)

```csharp
Missing = 0
```
