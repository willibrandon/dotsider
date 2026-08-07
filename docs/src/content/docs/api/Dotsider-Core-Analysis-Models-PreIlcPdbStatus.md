---
title: "PreIlcPdbStatus"
description: "The portable-PDB situation of a located pre-ILC managed assembly."
slug: api/dotsider.core.analysis.models.preilcpdbstatus
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The portable-PDB situation of a located pre-ILC managed assembly.

```csharp
public enum PreIlcPdbStatus
```

## Fields

### Embedded

No sidecar PDB, but the assembly embeds a portable PDB — source and sequence points still work.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
Embedded = 2
```

### Matched

A sidecar portable PDB exists and its ID matches the assembly's debug directory.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
Matched = 1
```

### Mismatched

A sidecar PDB exists but its ID does not match the assembly — it belongs to a different build.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
Mismatched = 4
```

### Missing

The assembly references a portable PDB but neither a sidecar nor an embedded copy was found.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
Missing = 3
```

### NotApplicable

No managed assembly was located, so no PDB question arises.

**Returns:** [PreIlcPdbStatus](/api/dotsider.core.analysis.models.preilcpdbstatus/)

```csharp
NotApplicable = 0
```
