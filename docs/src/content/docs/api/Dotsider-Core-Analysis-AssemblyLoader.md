---
title: "AssemblyLoader"
description: "Shared factory for opening assembly files. Handles apphosts (companion .dll redirect), single-file bundles (entry assembly extraction), Native AOT binaries, raw Wasm modules, and direct .dll/.exe loading. Returns an AssemblyOpenResult that preserves the distinction so callers can decide how to present each case (e.g. showing an apphost dialog)."
slug: api/dotsider.core.analysis.assemblyloader
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Shared factory for opening assembly files. Handles apphosts (companion .dll redirect),
single-file bundles (entry assembly extraction), Native AOT binaries, raw Wasm modules, and direct
.dll/.exe loading. Returns an [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) that preserves the
distinction so callers can decide how to present each case (e.g. showing an apphost dialog).

```csharp
public static class AssemblyLoader
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyLoader**

## Methods

### Open(string)

Opens an assembly from the given path, detecting apphosts and single-file bundles.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the file to open.

**Returns:** [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/)

An [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) describing the result:
[Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/) for regular assemblies,
[ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/) for native apphosts with a companion .dll,
[BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/) for single-file bundles,
[NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/) for Native AOT compiled binaries,
or [Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/) for raw Wasm modules.

```csharp
public static AssemblyOpenResult Open(string filePath)
```
