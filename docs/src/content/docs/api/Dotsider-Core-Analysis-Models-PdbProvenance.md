---
title: "PdbProvenance"
description: "Describes where portable PDB information was found, or why it could not be used."
slug: api/dotsider.core.analysis.models.pdbprovenance
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Describes where portable PDB information was found, or why it could not be used.

```csharp
public sealed record PdbProvenance : IEquatable<PdbProvenance>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PdbProvenance**

## Implements

- [IEquatable\<PdbProvenance\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### PdbProvenance(PdbProvenanceKind, string?, string?)

Describes where portable PDB information was found, or why it could not be used.

**Parameters:**

- `Kind` ([PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)): The resolved provenance category.
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The sidecar PDB path when one was used or probed.
- `Details` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Additional diagnostic context for display surfaces.

```csharp
public PdbProvenance(PdbProvenanceKind Kind, string? Path = null, string? Details = null)
```

## Properties

### Details

Additional diagnostic context for display surfaces.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Details { get; init; }
```

### Kind

The resolved provenance category.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
public PdbProvenanceKind Kind { get; init; }
```

### Path

The sidecar PDB path when one was used or probed.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Path { get; init; }
```

## Methods

### ToString()

Returns a string that represents the current object.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A string that represents the current object.

```csharp
public override string ToString()
```

