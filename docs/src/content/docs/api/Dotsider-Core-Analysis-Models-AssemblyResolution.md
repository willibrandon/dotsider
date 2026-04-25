---
title: "AssemblyResolution"
description: "Outcome of an identity-based assembly resolution. Carries everything the dependency-graph builder and UI need: the resolved file/bundle (or null on failure), the provenance classifying how the file was located, the candidate path of an identity-mismatched simple-name hit, and — for .NET Framework binds — the policy-layer attribution and the effective bound identity."
slug: api/dotsider.core.analysis.models.assemblyresolution
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Outcome of an identity-based assembly resolution. Carries everything the dependency-graph
builder and UI need: the resolved file/bundle (or null on failure), the
provenance classifying how the file was located, the candidate path of an identity-mismatched
simple-name hit, and — for .NET Framework binds — the policy-layer attribution and the
effective bound identity.

```csharp
public sealed record AssemblyResolution : IEquatable<AssemblyResolution>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyResolution**

## Implements

- [IEquatable\<AssemblyResolution\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AssemblyResolution(ResolvedAssembly?, AssemblyProvenance, string?, AppliedPolicy?, AssemblyRefInfo?)

Outcome of an identity-based assembly resolution. Carries everything the dependency-graph
builder and UI need: the resolved file/bundle (or null on failure), the
provenance classifying how the file was located, the candidate path of an identity-mismatched
simple-name hit, and — for .NET Framework binds — the policy-layer attribution and the
effective bound identity.

**Parameters:**

- `Resolved` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)): The file or bundle the binder picked, or null when the bind failed
(Unresolved, IdentityMismatch, CodeBaseMissing).
- `Provenance` ([AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)): Classification of how the node was located.
- `CandidateProbePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple-name match whose identity did not align (IdentityMismatch), or the configured
codeBase href that does not exist (CodeBaseMissing). null for other outcomes.
- `AppliedPolicy` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/)): Records the requested → bound rewrite when .NET Framework binding policy fired.
null for non-redirected resolutions and for all .NET Core / .NET 5+ resolutions.
- `LoadedIdentity` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The identity the binder actually loaded after applying policy. May differ from the requested
identity for net48 binds when redirects collapsed multiple requested versions onto one loaded
version. null for non-net48 resolutions and for failures.

```csharp
public AssemblyResolution(ResolvedAssembly? Resolved, AssemblyProvenance Provenance, string? CandidateProbePath, AppliedPolicy? AppliedPolicy = null, AssemblyRefInfo? LoadedIdentity = null)
```

## Properties

### AppliedPolicy

Records the requested → bound rewrite when .NET Framework binding policy fired.
null for non-redirected resolutions and for all .NET Core / .NET 5+ resolutions.

**Returns:** [AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/)

```csharp
public AppliedPolicy? AppliedPolicy { get; init; }
```

### CandidateProbePath

The simple-name match whose identity did not align (IdentityMismatch), or the configured
codeBase href that does not exist (CodeBaseMissing). null for other outcomes.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CandidateProbePath { get; init; }
```

### LoadedIdentity

The identity the binder actually loaded after applying policy. May differ from the requested
identity for net48 binds when redirects collapsed multiple requested versions onto one loaded
version. null for non-net48 resolutions and for failures.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo? LoadedIdentity { get; init; }
```

### Provenance

Classification of how the node was located.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
public AssemblyProvenance Provenance { get; init; }
```

### Resolved

The file or bundle the binder picked, or null when the bind failed
(Unresolved, IdentityMismatch, CodeBaseMissing).

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

```csharp
public ResolvedAssembly? Resolved { get; init; }
```

