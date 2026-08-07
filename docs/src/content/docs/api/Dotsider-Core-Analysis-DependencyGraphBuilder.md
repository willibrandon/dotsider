---
title: "DependencyGraphBuilder"
description: "Builds the full transitive assembly dependency graph rooted at an analyzed assembly. Performs a breadth-first walk through each assembly's AssemblyRefs, resolving children by full identity, deduping on Id, preserving edges for cycles and diamonds, and classifying unresolvable and identity-mismatched references as non-expanding leaf nodes. For .NET Framework roots the resolution routes through NetFxBinder so that nodes are keyed on the *bound* identity (post-redirect), collapsing two distinct requested versions onto a single graph node when policy redirects them to the same loaded version. Produces a DependencyGraphResult containing the public topology plus internal navigation metadata consumed only by the TUI."
slug: api/dotsider.core.analysis.dependencygraphbuilder
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Builds the full transitive assembly dependency graph rooted at an analyzed assembly.
Performs a breadth-first walk through each assembly's [AssemblyRefs](/api/dotsider.core.analysis.assemblyanalyzer.assemblyrefs/),
resolving children by full identity, deduping on [Id](/api/dotsider.core.analysis.models.graphnode.id/), preserving edges
for cycles and diamonds, and classifying unresolvable and identity-mismatched references as
non-expanding leaf nodes. For .NET Framework roots the resolution routes through
[NetFxBinder](/api/dotsider.core.analysis.netfxbinder/) so that nodes are keyed on the *bound* identity (post-redirect),
collapsing two distinct requested versions onto a single graph node when policy redirects them
to the same loaded version. Produces a [DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/) containing the
public topology plus internal navigation metadata consumed only by the TUI.

```csharp
public static class DependencyGraphBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DependencyGraphBuilder**

## Methods

### Build(AssemblyAnalyzer)

Builds the transitive dependency graph rooted at analyzer.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The root assembly analyzer. The caller retains ownership and disposal
            responsibility; the builder does not dispose it.

**Returns:** [DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/)

The computed nodes, edges, and per-node navigation metadata.

```csharp
public static DependencyGraphResult Build(AssemblyAnalyzer analyzer)
```
