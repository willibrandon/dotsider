---
title: "BindingPolicy"
description: "Aggregated .NET Framework binding policy assembled from framework unification, machine.config, publisher-policy assemblies, and the application configuration file. Layers are stored in document order with first-match semantics — the same model the CLR applies — and later layers (machine.config &gt; publisher &gt; app &gt; framework unification) override earlier ones when they target the same identity."
slug: api/dotsider.core.analysis.models.bindingpolicy
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Aggregated .NET Framework binding policy assembled from framework unification, machine.config,
publisher-policy assemblies, and the application configuration file. Layers are stored in
document order with first-match semantics — the same model the CLR applies — and later layers
(machine.config &gt; publisher &gt; app &gt; framework unification) override earlier ones when
they target the same identity.

```csharp
public sealed record BindingPolicy : IEquatable<BindingPolicy>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BindingPolicy**

## Implements

- [IEquatable\<BindingPolicy\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BindingPolicy(IReadOnlyList\<BindingRedirect\>, IReadOnlyList\<BindingRedirect\>, IReadOnlyList\<BindingRedirect\>, IReadOnlyList\<BindingRedirect\>, IReadOnlyList\<CodeBaseEntry\>, IReadOnlyCollection\<(string Name, string? PublicKeyToken, string Culture)\>, bool, IReadOnlyDictionary\<(string Name, string PublicKeyToken), Version\>?)

Aggregated .NET Framework binding policy assembled from framework unification, machine.config,
publisher-policy assemblies, and the application configuration file. Layers are stored in
document order with first-match semantics — the same model the CLR applies — and later layers
(machine.config &gt; publisher &gt; app &gt; framework unification) override earlier ones when
they target the same identity.

**Parameters:**

- `AppConfigRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Redirects parsed from `*.exe.config`/`*.dll.config`.
- `PublisherPolicyRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Redirects parsed from `policy.&lt;major&gt;.&lt;minor&gt;.&lt;simpleName&gt;` assemblies in the GAC.
- `MachineConfigRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Redirects parsed from the architecture-correct `machine.config`.
- `FrameworkUnificationRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Redirects produced by the CLR's built-in unification of well-known framework PKTs.
- `CodeBases` ([IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): CodeBase entries from any policy layer, ordered by precedence (machine &gt; publisher &gt; app).
- `PublisherPolicyDisabledFor` ([IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3)): Identities for which the application configuration set
`&lt;publisherPolicy apply="no"/&gt;` on a specific
`&lt;dependentAssembly&gt;`. Bypasses the publisher-policy layer for those identities.
- `PublisherPolicyDisabledGlobally` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): true when the application configuration set runtime-scoped
`&lt;publisherPolicy apply="no"/&gt;`, suppressing publisher policy for every bind in
the app domain — including identities that have no `&lt;dependentAssembly&gt;` block.
- `FrameworkUnificationTable` ([IReadOnlyDictionary\<String, String\>, Version\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-3)): Per-identity unification table built by scanning `Framework[64]\v4.0.30319` at policy
load time: maps `(Name, PublicKeyToken)` for in-box framework assemblies (PKT in
FrameworkUnificationPublicKeyTokens) to the version actually
shipped in the runtime directory. NetFxArchitecture) consults this map first; references
at versions less than or equal to the table version unify to the table version, so a
subsequent GAC lookup finds the file at its real GAC location instead of falling through to
a post-hoc framework-directory match.

```csharp
public BindingPolicy(IReadOnlyList<BindingRedirect> AppConfigRedirects, IReadOnlyList<BindingRedirect> PublisherPolicyRedirects, IReadOnlyList<BindingRedirect> MachineConfigRedirects, IReadOnlyList<BindingRedirect> FrameworkUnificationRedirects, IReadOnlyList<CodeBaseEntry> CodeBases, IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> PublisherPolicyDisabledFor, bool PublisherPolicyDisabledGlobally = false, IReadOnlyDictionary<(string Name, string PublicKeyToken), Version>? FrameworkUnificationTable = null)
```

## Properties

### AppConfigRedirects

Redirects parsed from `*.exe.config`/`*.dll.config`.

**Returns:** [IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BindingRedirect> AppConfigRedirects { get; init; }
```

### CodeBases

CodeBase entries from any policy layer, ordered by precedence (machine &gt; publisher &gt; app).

**Returns:** [IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CodeBaseEntry> CodeBases { get; init; }
```

### Empty

An empty policy — no redirects, no codeBase, no publisher-policy bypasses.

**Returns:** [BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)

```csharp
public static BindingPolicy Empty { get; }
```

### FrameworkUnificationRedirects

Redirects produced by the CLR's built-in unification of well-known framework PKTs.

**Returns:** [IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BindingRedirect> FrameworkUnificationRedirects { get; init; }
```

### FrameworkUnificationTable

Per-identity unification table built by scanning `Framework[64]\v4.0.30319` at policy
load time: maps `(Name, PublicKeyToken)` for in-box framework assemblies (PKT in
FrameworkUnificationPublicKeyTokens) to the version actually
shipped in the runtime directory. NetFxArchitecture) consults this map first; references
at versions less than or equal to the table version unify to the table version, so a
subsequent GAC lookup finds the file at its real GAC location instead of falling through to
a post-hoc framework-directory match.

**Returns:** [IReadOnlyDictionary\<String, String\>, Version\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-3)

```csharp
public IReadOnlyDictionary<(string Name, string PublicKeyToken), Version>? FrameworkUnificationTable { get; init; }
```

### MachineConfigRedirects

Redirects parsed from the architecture-correct `machine.config`.

**Returns:** [IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BindingRedirect> MachineConfigRedirects { get; init; }
```

### PublisherPolicyDisabledFor

Identities for which the application configuration set
`&lt;publisherPolicy apply="no"/&gt;` on a specific
`&lt;dependentAssembly&gt;`. Bypasses the publisher-policy layer for those identities.

**Returns:** [IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3)

```csharp
public IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> PublisherPolicyDisabledFor { get; init; }
```

### PublisherPolicyDisabledGlobally

true when the application configuration set runtime-scoped
`&lt;publisherPolicy apply="no"/&gt;`, suppressing publisher policy for every bind in
the app domain — including identities that have no `&lt;dependentAssembly&gt;` block.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool PublisherPolicyDisabledGlobally { get; init; }
```

### PublisherPolicyRedirects

Redirects parsed from `policy.&lt;major&gt;.&lt;minor&gt;.&lt;simpleName&gt;` assemblies in the GAC.

**Returns:** [IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BindingRedirect> PublisherPolicyRedirects { get; init; }
```

## Methods

### Apply(AssemblyRefInfo, NetFxArchitecture)

Resolves the effective identity for the requested reference by walking the policy layers
in CLR walk order — app config first, then publisher policy (skipped if bypassed for this
identity), then machine.config — with later layers overriding earlier ones. Framework
unification supplies the baseline mapping when no later layer rewrites the identity. The
returned tuple includes which layer produced the rewrite, so callers can attach an
[AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/) to the resolution result.

**Parameters:**

- `requested` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The identity exactly as named by the metadata reference.
- `architecture` ([NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)): Effective process bitness, used to filter `processorArchitecture` entries.

**Returns:** [ValueTuple\<AssemblyRefInfo, AppliedPolicy\>](https://learn.microsoft.com/dotnet/api/system.valuetuple-2)

The effective identity and the policy layer that produced any rewrite.
[AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/) is null when no layer rewrote the identity.

```csharp
public (AssemblyRefInfo Effective, AppliedPolicy? Applied) Apply(AssemblyRefInfo requested, NetFxArchitecture architecture)
```

### Deconstruct(out IReadOnlyList\<BindingRedirect\>, out IReadOnlyList\<BindingRedirect\>, out IReadOnlyList\<BindingRedirect\>, out IReadOnlyList\<BindingRedirect\>, out IReadOnlyList\<CodeBaseEntry\>, out IReadOnlyCollection\<(string Name, string? PublicKeyToken, string Culture)\>, out bool, out IReadOnlyDictionary\<(string Name, string PublicKeyToken), Version\>?)

**Parameters:**

- `AppConfigRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `PublisherPolicyRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MachineConfigRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `FrameworkUnificationRedirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `CodeBases` ([IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `PublisherPolicyDisabledFor` ([IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3))
- `PublisherPolicyDisabledGlobally` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `FrameworkUnificationTable` ([IReadOnlyDictionary\<String, String\>, Version\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-3))

```csharp
public void Deconstruct(out IReadOnlyList<BindingRedirect> AppConfigRedirects, out IReadOnlyList<BindingRedirect> PublisherPolicyRedirects, out IReadOnlyList<BindingRedirect> MachineConfigRedirects, out IReadOnlyList<BindingRedirect> FrameworkUnificationRedirects, out IReadOnlyList<CodeBaseEntry> CodeBases, out IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> PublisherPolicyDisabledFor, out bool PublisherPolicyDisabledGlobally, out IReadOnlyDictionary<(string Name, string PublicKeyToken), Version>? FrameworkUnificationTable)
```

### Equals(BindingPolicy?)

**Parameters:**

- `other` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(BindingPolicy? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### FindCodeBaseFor(AssemblyRefInfo)

Returns the `&lt;codeBase&gt;` entry that anchors the supplied effective identity, or
null when no codeBase is configured for that identity.

**Parameters:**

- `effective` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The post-policy identity to look up.

**Returns:** [CodeBaseEntry](/api/dotsider.core.analysis.models.codebaseentry/)

A matching [CodeBaseEntry](/api/dotsider.core.analysis.models.codebaseentry/), or null.

```csharp
public CodeBaseEntry? FindCodeBaseFor(AssemblyRefInfo effective)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### LoadFrom(string?, NetFxArchitecture, IReadOnlyList\<string\>, NetFxRuntimeVersion)

Loads policy from the analyzer's app/exe config plus machine.config and any publisher-policy
assemblies discovered in the supplied GAC roots. Errors are handled per CLR semantics:
malformed XML at the document level yields an empty policy for that source; individual
invalid `&lt;dependentAssembly&gt;`/`&lt;bindingRedirect&gt;` sections are silently
dropped and the rest of the file continues to apply.

**Parameters:**

- `appConfigPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the application configuration file, or null.
- `architecture` ([NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)): Effective process bitness, controls which `machine.config` to read.
- `gacRoots` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): GAC root directories to scan for publisher-policy assemblies.
- `runtimeVersion` ([NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)): CLR generation the policy targets. Switches the machine.config path, GAC token format,
reference-assemblies tree, and `appliesTo` filter between
[Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) and [Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/).

**Returns:** [BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)

A populated [BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/).

```csharp
public static BindingPolicy LoadFrom(string? appConfigPath, NetFxArchitecture architecture, IReadOnlyList<string> gacRoots, NetFxRuntimeVersion runtimeVersion = NetFxRuntimeVersion.Clr4)
```

### ParseConfigFile(string?, PolicyLayer, NetFxRuntimeVersion)

Parses a single configuration file (app config, machine.config, or a publisher-policy
assembly's embedded XML resource) into a [BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/).
Exposed so callers that already have the file path can avoid re-parsing.

**Parameters:**

- `path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the configuration file, or null.
- `source` ([PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)): Policy layer to attribute parsed entries to.
- `runtimeVersion` ([NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)): CLR generation the parse targets. Filters `&lt;assemblyBinding appliesTo="..."&gt;`
blocks: [Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) accepts `v2`/`v2.0`/`v2.0.50727`;
[Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) accepts `v4`/`v4.*`; an empty
`appliesTo` matches both.

**Returns:** [BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/)

The parsed result; an empty result on missing file or malformed XML.

```csharp
public static BindingPolicyParseResult ParseConfigFile(string? path, PolicyLayer source, NetFxRuntimeVersion runtimeVersion = NetFxRuntimeVersion.Clr4)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(BindingPolicy?, BindingPolicy?)

**Parameters:**

- `left` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/))
- `right` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(BindingPolicy? left, BindingPolicy? right)
```

### operator ==(BindingPolicy?, BindingPolicy?)

**Parameters:**

- `left` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/))
- `right` ([BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(BindingPolicy? left, BindingPolicy? right)
```
