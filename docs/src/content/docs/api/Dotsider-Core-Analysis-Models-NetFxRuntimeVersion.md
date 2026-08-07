---
title: "NetFxRuntimeVersion"
description: ".NET Framework CLR version a NetFxBindingContext targets. The CLR version (not the product TFM) drives the binding pipeline because the GAC layout, machine.config path, framework runtime directory, reference-assemblies tree, and appliesTo filter all switch on the CLR generation: Clr2 covers .NET Framework 2.0 / 3.0 / 3.5 SP1 (process runs on v2.0.50727); Clr4 covers .NET Framework 4.0 through 4.8.x (process runs on v4.0.30319)."
slug: api/dotsider.core.analysis.models.netfxruntimeversion
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

.NET Framework CLR version a [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/) targets. The CLR version (not
the product TFM) drives the binding pipeline because the GAC layout, machine.config path,
framework runtime directory, reference-assemblies tree, and `appliesTo` filter all switch
on the CLR generation: [Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) covers .NET Framework 2.0 / 3.0 / 3.5 SP1 (process
runs on `v2.0.50727`); [Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) covers .NET Framework 4.0 through 4.8.x
(process runs on `v4.0.30319`).

```csharp
public enum NetFxRuntimeVersion
```

## Fields

### Clr2

CLR 2.0 generation — .NET Framework 2.0, 3.0, 3.5 SP1. Binds out of
`%WINDIR%\assembly\GAC*` with token format `&lt;version&gt;__&lt;pkt&gt;` and
reads `%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727`.

**Returns:** [NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)

```csharp
Clr2 = 0
```

### Clr4

CLR 4 generation — .NET Framework 4.0 through 4.8.x. Binds out of
`%WINDIR%\Microsoft.NET\assembly\GAC_*` with token format
`v4.0_&lt;version&gt;__&lt;pkt&gt;` and reads
`%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319`.

**Returns:** [NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)

```csharp
Clr4 = 1
```
