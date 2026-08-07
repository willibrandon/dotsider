---
title: "PdbProvenanceKind"
description: "Portable PDB discovery outcomes that are meaningful to .NET developers."
slug: api/dotsider.core.analysis.models.pdbprovenancekind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Portable PDB discovery outcomes that are meaningful to .NET developers.

```csharp
public enum PdbProvenanceKind
```

## Fields

### BundleSidecarSkipped

The assembly came from a single-file bundle, so sidecar probing was intentionally skipped.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
BundleSidecarSkipped = 7
```

### CodeViewSidecarMismatched

A portable sidecar PDB was found, but its ID does not match the PE CodeView entry.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
CodeViewSidecarMismatched = 2
```

### CodeViewSidecarMissing

A portable CodeView entry points at a sidecar PDB that was not found.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
CodeViewSidecarMissing = 1
```

### Embedded

An embedded portable PDB was opened.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
Embedded = 4
```

### InvalidEmbeddedPdb

An embedded portable PDB was present, but it was malformed or exceeded a safety limit.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
InvalidEmbeddedPdb = 8
```

### NativePdb

A Windows native PDB was found beside the binary and its GUID and age match the CodeView entry.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
NativePdb = 6
```

### NoDebugDirectory

The PE has no debug directory.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
NoDebugDirectory = 0
```

### Sidecar

A matching portable sidecar PDB was opened.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
Sidecar = 3
```

### UnsupportedWindowsPdb

A CodeView entry was present, but it identifies a Windows PDB or another non-portable PDB.

**Returns:** [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

```csharp
UnsupportedWindowsPdb = 5
```
