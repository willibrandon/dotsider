---
title: "NetFxBinder"
description: "CLR-accurate .NET Framework assembly binder for both CLR generations. Consumes a NetFxBindingContext and produces a NetFxBindResult matching what the actual .NET Framework binder would do at runtime: framework unification + machine.config + publisher policy + app config (in CLR walk order, with later layers overriding earlier ones), then locate against the GAC (architecture-prioritized, strong-named only), then the framework runtime directory, then configured codeBase href (fail-fast), then the application base + private paths with culture-aware probing."
slug: api/dotsider.core.analysis.netfxbinder
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

CLR-accurate .NET Framework assembly binder for both CLR generations. Consumes a
[NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/) and produces a [NetFxBindResult](/api/dotsider.core.analysis.models.netfxbindresult/) matching
what the actual .NET Framework binder would do at runtime: framework unification +
machine.config + publisher policy + app config (in CLR walk order, with later layers
overriding earlier ones), then locate against the GAC (architecture-prioritized,
strong-named only), then the framework runtime directory, then configured codeBase href
(fail-fast), then the application base + private paths with culture-aware probing.

```csharp
public static class NetFxBinder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NetFxBinder**

## Methods

### Bind(AssemblyRefInfo, NetFxBindingContext)

Binds the requested assembly identity through the supplied .NET Framework binding policy
and locates the file the CLR would actually load.

**Parameters:**

- `requested` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The identity exactly as named by the metadata reference.
- `ctx` ([NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)): The binding context built from the analyzed root.

**Returns:** [NetFxBindResult](/api/dotsider.core.analysis.models.netfxbindresult/)

The bind outcome.

```csharp
public static NetFxBindResult Bind(AssemblyRefInfo requested, NetFxBindingContext ctx)
```

### ClearCaches(NetFxBindingContext)

Clears all per-context caches (RequestedBindCache, LoadedAssemblyCache, probe counter).
Test-only diagnostic for resetting state between assertions.

**Parameters:**

- `ctx` ([NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)): The binding context whose caches to clear.

```csharp
public static void ClearCaches(NetFxBindingContext ctx)
```

### GetProbeCount(NetFxBindingContext)

Filesystem probe count for the supplied context. Test-only diagnostic that proves
repeated NetFxBindingContext) calls hit the cache without re-walking the filesystem.

**Parameters:**

- `ctx` ([NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)): The binding context to inspect.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

The number of filesystem probes performed for ctx.

```csharp
public static int GetProbeCount(NetFxBindingContext ctx)
```

## Remarks

The probe locations and GAC token format switch on [NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/):

[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) (.NET Framework 4.0 – 4.8.x) — GAC at
    `%WINDIR%\Microsoft.NET\assembly\GAC_*` with `v4.0_&lt;version&gt;__&lt;pkt&gt;`
    tokens; runtime directory `v4.0.30319`. The bare `%WINDIR%\assembly\GAC` is
    consulted as a COM-PIA fallback after the .NET 4 GAC misses.[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) (.NET Framework 2.0 / 3.0 / 3.5) — GAC at
    `%WINDIR%\assembly\{GAC_MSIL, GAC_&lt;arch&gt;, GAC}` with no-prefix
    `&lt;version&gt;__&lt;pkt&gt;` tokens; runtime directory `v2.0.50727`.

.NET Core / .NET 5+ roots never construct a binding context, so this type is never invoked
for them and their probe chain is unchanged.
