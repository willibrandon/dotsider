---
title: "NetFxBindingContext"
description: "Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per analyzed root via AssemblyAnalyzer); carried alongside the analyzer through every resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase) so that every code path produces the same answer for any .NET Framework reference."
slug: api/dotsider.core.analysis.models.netfxbindingcontext
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
analyzed root via [AssemblyAnalyzer)](/api/dotsider.core.analysis.models.netfxbindingcontext.trybuild(dotsider.core.analysis.assemblyanalyzer)/); carried alongside the analyzer through every
resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
so that every code path produces the same answer for any .NET Framework reference.

```csharp
public sealed record NetFxBindingContext : IEquatable<NetFxBindingContext>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NetFxBindingContext**

## Implements

- [IEquatable\<NetFxBindingContext\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NetFxBindingContext(string, string, string?, string?, NetFxArchitecture, BindingPolicy, IReadOnlyList\<string\>, IReadOnlyList\<string\>, NetFxRuntimeVersion, bool)

Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
analyzed root via [AssemblyAnalyzer)](/api/dotsider.core.analysis.models.netfxbindingcontext.trybuild(dotsider.core.analysis.assemblyanalyzer)/); carried alongside the analyzer through every
resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
so that every code path produces the same answer for any .NET Framework reference.

**Parameters:**

- `EntryAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the root EXE/DLL.
- `AppBaseDirectory` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Application base — the directory containing the entry assembly.
- `ConfigPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Adjacent `*.exe.config`/`*.dll.config`, or null.
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker (e.g. `.NETFramework,Version=v4.8`), or null
when the root assembly carries no `TargetFrameworkAttribute` — typical for CLR 2 roots
(.NET Framework 2.0 / 3.0 / 3.5), where the runtime version is inferred from the
`mscorlib` assembly reference instead.
- `EffectiveArchitecture` ([NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)): Runtime process bitness for the root.
- `Policy` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)): Layered binding policy (framework unification + machine + publisher + app).
- `PrivatePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): `&lt;probing privatePath&gt;` entries from the app config, rooted at
AppBaseDirectory.
- `GacRoots` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Roots to scan when probing the GAC. Defaults to `[%WINDIR%\Microsoft.NET\assembly]` for
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) and `[%WINDIR%\assembly]` for
[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/); tests may inject additional roots to exercise
publisher-policy discovery without touching the system GAC.
- `RuntimeVersion` ([NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)): CLR generation the root targets. Drives every per-runtime difference: GAC layout, GAC token
format, machine.config path, framework runtime directory, reference-assemblies tree, and
`appliesTo` filtering.
- `IsRuntimeVersionInferred` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): true when [RuntimeVersion](/api/dotsider.core.analysis.models.netfxbindingcontext.runtimeversion/) was determined from the
`mscorlib` assembly reference rather than from a `TargetFrameworkAttribute` whose
value pinned the runtime. Stays true for a CLR 2 root that carries a real
`.NETFramework,Version=v3.5` TFM (TFM is read but the gate fires on mscorlib v2).

```csharp
public NetFxBindingContext(string EntryAssemblyPath, string AppBaseDirectory, string? ConfigPath, string? TargetFramework, NetFxArchitecture EffectiveArchitecture, BindingPolicy Policy, IReadOnlyList<string> PrivatePaths, IReadOnlyList<string> GacRoots, NetFxRuntimeVersion RuntimeVersion = NetFxRuntimeVersion.Clr4, bool IsRuntimeVersionInferred = false)
```

## Properties

### AppBaseDirectory

Application base — the directory containing the entry assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AppBaseDirectory { get; init; }
```

### ConfigPath

Adjacent `*.exe.config`/`*.dll.config`, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ConfigPath { get; init; }
```

### EffectiveArchitecture

Runtime process bitness for the root.

**Returns:** [NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)

```csharp
public NetFxArchitecture EffectiveArchitecture { get; init; }
```

### EntryAssemblyPath

Path to the root EXE/DLL.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string EntryAssemblyPath { get; init; }
```

### GacRoots

Roots to scan when probing the GAC. Defaults to `[%WINDIR%\Microsoft.NET\assembly]` for
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) and `[%WINDIR%\assembly]` for
[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/); tests may inject additional roots to exercise
publisher-policy discovery without touching the system GAC.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> GacRoots { get; init; }
```

### IsRuntimeVersionInferred

true when [RuntimeVersion](/api/dotsider.core.analysis.models.netfxbindingcontext.runtimeversion/) was determined from the
`mscorlib` assembly reference rather than from a `TargetFrameworkAttribute` whose
value pinned the runtime. Stays true for a CLR 2 root that carries a real
`.NETFramework,Version=v3.5` TFM (TFM is read but the gate fires on mscorlib v2).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsRuntimeVersionInferred { get; init; }
```

### Policy

Layered binding policy (framework unification + machine + publisher + app).

**Returns:** [BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)

```csharp
public BindingPolicy Policy { get; init; }
```

### PrivatePaths

`&lt;probing privatePath&gt;` entries from the app config, rooted at
AppBaseDirectory.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> PrivatePaths { get; init; }
```

### RuntimeVersion

CLR generation the root targets. Drives every per-runtime difference: GAC layout, GAC token
format, machine.config path, framework runtime directory, reference-assemblies tree, and
`appliesTo` filtering.

**Returns:** [NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)

```csharp
public NetFxRuntimeVersion RuntimeVersion { get; init; }
```

### TargetFramework

Target framework moniker (e.g. `.NETFramework,Version=v4.8`), or null
when the root assembly carries no `TargetFrameworkAttribute` — typical for CLR 2 roots
(.NET Framework 2.0 / 3.0 / 3.5), where the runtime version is inferred from the
`mscorlib` assembly reference instead.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetFramework { get; init; }
```

## Methods

### FrameworkRuntimeDirectory()

Returns the architecture-correct .NET Framework runtime directory. For
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/): `%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319`.
For [Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/):
`%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The directory if it exists on disk, otherwise null.

```csharp
public string? FrameworkRuntimeDirectory()
```

### GacScanList()

Returns the GAC sub-directories in the architecture-prioritized scan list. The shape
differs by CLR:
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/): `GAC_MSIL` + `GAC_64` for Amd64,
    `GAC_MSIL` + `GAC_32` for X86. The bare `GAC` bucket is reached via
    [LegacyGacScanList](/api/dotsider.core.analysis.models.netfxbindingcontext.legacygacscanlist/) for the COM-PIA fallback.[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/): `GAC_MSIL` + arch + bare `GAC`
    (CLR 1.x carryover, still consulted by CLR2 fusion). All three are scanned with the
    legacy `&lt;version&gt;__&lt;pkt&gt;` token format.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

Absolute paths to the GAC sub-directories the binder should scan, in order.

```csharp
public IReadOnlyList<string> GacScanList()
```

### LegacyGacScanList()

Returns the legacy CLR 2.0 GAC sub-directories under `%WINDIR%\assembly`. For
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) this is the COM-PIA fallback path probed after
the .NET 4 GAC scan misses (e.g. `stdole 7.0.3300.0`). For
[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) this returns empty — the primary
[GacScanList](/api/dotsider.core.analysis.models.netfxbindingcontext.gacscanlist/) already covers `%WINDIR%\assembly` directly.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

Absolute paths to scan, in order; empty when not on Windows or for Clr2.

```csharp
public IReadOnlyList<string> LegacyGacScanList()
```

### TryBuild(AssemblyAnalyzer)

Builds a context for a .NET Framework root, or returns null for any
other target. .NET Core / .NET 5+ analyzers always receive a null
context and fall back to the existing probe chain unchanged.

**Parameters:**

- `rootAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The root assembly analyzer.

**Returns:** [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)

A populated context, or null for non-.NET-Framework roots.

```csharp
public static NetFxBindingContext? TryBuild(AssemblyAnalyzer rootAnalyzer)
```

