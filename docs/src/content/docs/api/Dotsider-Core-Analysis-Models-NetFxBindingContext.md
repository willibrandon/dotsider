---
title: "NetFxBindingContext"
description: "Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per analyzed root via AssemblyAnalyzer); carried alongside the analyzer through every resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase) so that every code path produces the same answer for any net48 reference."
slug: api/dotsider.core.analysis.models.netfxbindingcontext
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
analyzed root via [AssemblyAnalyzer)](/api/dotsider.core.analysis.models.netfxbindingcontext.trybuild(dotsider.core.analysis.assemblyanalyzer)/); carried alongside the analyzer through every
resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
so that every code path produces the same answer for any net48 reference.

```csharp
public sealed record NetFxBindingContext : IEquatable<NetFxBindingContext>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NetFxBindingContext**

## Implements

- [IEquatable\<NetFxBindingContext\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NetFxBindingContext(string, string, string?, string, NetFxArchitecture, BindingPolicy, IReadOnlyList\<string\>, IReadOnlyList\<string\>)

Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
analyzed root via [AssemblyAnalyzer)](/api/dotsider.core.analysis.models.netfxbindingcontext.trybuild(dotsider.core.analysis.assemblyanalyzer)/); carried alongside the analyzer through every
resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
so that every code path produces the same answer for any net48 reference.

**Parameters:**

- `EntryAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the root EXE/DLL.
- `AppBaseDirectory` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Application base — the directory containing the entry assembly.
- `ConfigPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Adjacent `*.exe.config`/`*.dll.config`, or null.
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker (e.g. `.NETFramework,Version=v4.8`).
- `EffectiveArchitecture` ([NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)): Runtime process bitness for the root.
- `Policy` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)): Layered binding policy (framework unification + machine + publisher + app).
- `PrivatePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): `&lt;probing privatePath&gt;` entries from the app config, rooted at
AppBaseDirectory.
- `GacRoots` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Roots to scan when probing the GAC. Defaults to `[%WINDIR%\Microsoft.NET\assembly]`;
tests may inject additional roots to exercise publisher-policy discovery without touching the
system GAC.

```csharp
public NetFxBindingContext(string EntryAssemblyPath, string AppBaseDirectory, string? ConfigPath, string TargetFramework, NetFxArchitecture EffectiveArchitecture, BindingPolicy Policy, IReadOnlyList<string> PrivatePaths, IReadOnlyList<string> GacRoots)
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

Roots to scan when probing the GAC. Defaults to `[%WINDIR%\Microsoft.NET\assembly]`;
tests may inject additional roots to exercise publisher-policy discovery without touching the
system GAC.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> GacRoots { get; init; }
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

### TargetFramework

Target framework moniker (e.g. `.NETFramework,Version=v4.8`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TargetFramework { get; init; }
```

## Methods

### FrameworkRuntimeDirectory()

Returns the architecture-correct .NET Framework runtime directory:
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319` for Amd64,
`%WINDIR%\Microsoft.NET\Framework\v4.0.30319` for X86.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The directory if it exists on disk, otherwise null.

```csharp
public string? FrameworkRuntimeDirectory()
```

### GacScanList()

Returns the GAC sub-directories in the architecture-prioritized scan list:
`GAC_MSIL` + `GAC_64` for Amd64, `GAC_MSIL` + `GAC_32` for X86.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

Absolute paths to the GAC sub-directories the binder should scan, in order.

```csharp
public IReadOnlyList<string> GacScanList()
```

### LegacyGacScanList()

Returns the legacy CLR 2.0 GAC sub-directories — `%WINDIR%\assembly\GAC_MSIL`,
the architecture-matching `GAC_64` or `GAC_32`, and the original
`GAC` (CLR 1.x). Net4 fusion still consults this cache for COM PIAs and other
2.0-registered assemblies (e.g. `stdole 7.0.3300.0`), so the binder probes
these locations after the .NET 4 GAC scan misses. Token format here is
`&lt;version&gt;__&lt;pkt&gt;` with no `v4.0_` prefix.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

Absolute paths to scan, in order; empty when not on Windows.

```csharp
public IReadOnlyList<string> LegacyGacScanList()
```

### TryBuild(AssemblyAnalyzer)

Builds a context for a .NET Framework root, or returns null for any
other target framework. .NET Core / .NET 5+ analyzers always receive a null
context and fall back to the existing probe chain unchanged.

**Parameters:**

- `rootAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The root assembly analyzer.

**Returns:** [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)

A populated context, or null for non-net48 roots.

```csharp
public static NetFxBindingContext? TryBuild(AssemblyAnalyzer rootAnalyzer)
```

