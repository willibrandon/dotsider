---
title: "NetFxBindResult"
description: "Result of a single .NET Framework bind. Carries the requested identity, the effective identity after policy was applied, the loaded identity (when binding succeeded), the file path the CLR would load, the provenance classification, the policy-layer attribution, and (when binding failed) a human-readable reason for UI surfacing."
slug: api/dotsider.core.analysis.models.netfxbindresult
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Result of a single .NET Framework bind. Carries the requested identity, the effective identity
after policy was applied, the loaded identity (when binding succeeded), the file path the CLR
would load, the provenance classification, the policy-layer attribution, and (when binding
failed) a human-readable reason for UI surfacing.

```csharp
public sealed record NetFxBindResult : IEquatable<NetFxBindResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NetFxBindResult**

## Implements

- [IEquatable\<NetFxBindResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NetFxBindResult(AssemblyRefInfo, AssemblyRefInfo, AssemblyRefInfo?, string?, AssemblyProvenance, AppliedPolicy?, string?, string?)

Result of a single .NET Framework bind. Carries the requested identity, the effective identity
after policy was applied, the loaded identity (when binding succeeded), the file path the CLR
would load, the provenance classification, the policy-layer attribution, and (when binding
failed) a human-readable reason for UI surfacing.

**Parameters:**

- `Requested` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): Identity exactly as named by the metadata reference.
- `EffectiveAfterPolicy` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): Identity after framework unification + machine + publisher + app.
- `Loaded` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): Identity of the file the binder actually opened, or null on failure.
- `LoadedPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path the binder would hand to the CLR loader, or null on failure.
- `Provenance` ([AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)): Classification of how the node was located.
- `AppliedPolicy` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/)): Records the requested → bound rewrite when policy fired.
- `FailureReason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Human-readable explanation for non-success outcomes.
- `CandidateProbePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): For [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/), the simple-name match whose identity
did not align. null for other outcomes.

```csharp
public NetFxBindResult(AssemblyRefInfo Requested, AssemblyRefInfo EffectiveAfterPolicy, AssemblyRefInfo? Loaded, string? LoadedPath, AssemblyProvenance Provenance, AppliedPolicy? AppliedPolicy, string? FailureReason, string? CandidateProbePath)
```

## Properties

### AppliedPolicy

Records the requested → bound rewrite when policy fired.

**Returns:** [AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/)

```csharp
public AppliedPolicy? AppliedPolicy { get; init; }
```

### CandidateProbePath

For [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/), the simple-name match whose identity
did not align. null for other outcomes.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CandidateProbePath { get; init; }
```

### EffectiveAfterPolicy

Identity after framework unification + machine + publisher + app.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo EffectiveAfterPolicy { get; init; }
```

### FailureReason

Human-readable explanation for non-success outcomes.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? FailureReason { get; init; }
```

### Loaded

Identity of the file the binder actually opened, or null on failure.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo? Loaded { get; init; }
```

### LoadedPath

Path the binder would hand to the CLR loader, or null on failure.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? LoadedPath { get; init; }
```

### Provenance

Classification of how the node was located.

**Returns:** [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

```csharp
public AssemblyProvenance Provenance { get; init; }
```

### Requested

Identity exactly as named by the metadata reference.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo Requested { get; init; }
```

