---
title: "GraphNavigationContext"
description: "Internal per-node metadata describing how a dependency graph node was resolved and the context under which it was reached. Used by the TUI for Enter-to-open navigation and framework filtering. Never serialized — this data must not leak through CLI, diagnostics, or MCP surfaces that publish graph topology."
slug: api/dotsider.core.analysis.models.graphnavigationcontext
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Internal per-node metadata describing how a dependency graph node was resolved and the
context under which it was reached. Used by the TUI for Enter-to-open navigation and
framework filtering. Never serialized — this data must not leak through CLI, diagnostics,
or MCP surfaces that publish graph topology.

```csharp
public sealed record GraphNavigationContext : IEquatable<GraphNavigationContext>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **GraphNavigationContext**

## Implements

- [IEquatable\<GraphNavigationContext\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GraphNavigationContext(ResolvedAssembly?, string?, string?, string?, string?, AssemblyProvenance, bool, string?)

Internal per-node metadata describing how a dependency graph node was resolved and the
context under which it was reached. Used by the TUI for Enter-to-open navigation and
framework filtering. Never serialized — this data must not leak through CLI, diagnostics,
or MCP surfaces that publish graph topology.

**Parameters:**

- `Resolved` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)): The resolved assembly location, or null when the node is unresolved or
the provenance is [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/).
- `ReferencingFilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The file path of the analyzer that first caused this node to be visited.
- `ReferencingBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The bundle path associated with the referencing analyzer, when applicable.
- `ReferencingTargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The target framework of the referencing analyzer, used for shared-framework probing.
- `ReferencingPreferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The preferred runtime pack of the referencing analyzer.
- `Provenance` ([AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)): Classification of how the node was located.
- `IsFrameworkAssembly` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the node represents a .NET framework assembly, classified independently of its
provenance so that framework assemblies shipped inside a self-contained publish or single-file
bundle are still identified correctly.
- `CandidateProbePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The file path of a simple-name match whose identity did not match the requested reference,
populated only when [Provenance](/api/dotsider.core.analysis.models.graphnavigationcontext.provenance/) is [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/).

```csharp
public GraphNavigationContext(ResolvedAssembly? Resolved, string? ReferencingFilePath, string? ReferencingBundlePath, string? ReferencingTargetFramework, string? ReferencingPreferredRuntimePack, AssemblyProvenance Provenance, bool IsFrameworkAssembly, string? CandidateProbePath)
```

## Properties

### CandidateProbePath

The file path of a simple-name match whose identity did not match the requested reference,
populated only when [Provenance](/api/dotsider.core.analysis.models.graphnavigationcontext.provenance/) is [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CandidateProbePath { get; init; }
```

### IsFrameworkAssembly

Whether the node represents a .NET framework assembly, classified independently of its
provenance so that framework assemblies shipped inside a self-contained publish or single-file
bundle are still identified correctly.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsFrameworkAssembly { get; init; }
```

### Provenance

Classification of how the node was located.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
public AssemblyProvenance Provenance { get; init; }
```

### ReferencingBundlePath

The bundle path associated with the referencing analyzer, when applicable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ReferencingBundlePath { get; init; }
```

### ReferencingFilePath

The file path of the analyzer that first caused this node to be visited.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ReferencingFilePath { get; init; }
```

### ReferencingPreferredRuntimePack

The preferred runtime pack of the referencing analyzer.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ReferencingPreferredRuntimePack { get; init; }
```

### ReferencingTargetFramework

The target framework of the referencing analyzer, used for shared-framework probing.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ReferencingTargetFramework { get; init; }
```

### Resolved

The resolved assembly location, or null when the node is unresolved or
the provenance is [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/).

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

```csharp
public ResolvedAssembly? Resolved { get; init; }
```

