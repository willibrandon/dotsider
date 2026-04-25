---
title: "NetFxBinder"
description: "CLR-accurate .NET Framework 4.x assembly binder. Consumes a NetFxBindingContext and produces a NetFxBindResult matching what the actual .NET Framework binder would do at runtime: framework unification + machine.config + publisher policy + app config (in CLR walk order, with later layers overriding earlier ones), then locate against the GAC (architecture-prioritized, strong-named only), then the Framework[64] runtime directory, then configured codeBase href (fail-fast), then the application base + private paths with culture-aware probing. .NET Core / .NET 5+ roots never construct a binding context, so this type is never invoked for them and their probe chain is unchanged."
slug: api/dotsider.core.analysis.netfxbinder
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

CLR-accurate .NET Framework 4.x assembly binder. Consumes a [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)
and produces a [NetFxBindResult](/api/dotsider.core.analysis.models.netfxbindresult/) matching what the actual .NET Framework binder
would do at runtime: framework unification + machine.config + publisher policy + app config
(in CLR walk order, with later layers overriding earlier ones), then locate against the GAC
(architecture-prioritized, strong-named only), then the Framework[64] runtime directory, then
configured codeBase href (fail-fast), then the application base + private paths with
culture-aware probing. .NET Core / .NET 5+ roots never construct a binding context, so this
type is never invoked for them and their probe chain is unchanged.

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

