---
title: "ReadyToRunNativeAvailability"
description: "Why a managed method does or does not have inspectable ReadyToRun native code. Distinguishing these keeps a correlation report honest — a missing composite or unresolved component metadata is not the same as a genuinely IL-only method."
slug: api/dotsider.core.analysis.models.readytorunnativeavailability
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Why a managed method does or does not have inspectable ReadyToRun native code. Distinguishing
these keeps a correlation report honest — a missing composite or unresolved component metadata
is not the same as a genuinely IL-only method.

```csharp
public enum ReadyToRunNativeAvailability
```

## Fields

### ArchUnsupported

The method is precompiled, but the image architecture could not be identified.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
ArchUnsupported = 4
```

### ComponentMetadataUnavailable

The owning component's metadata could not be resolved by name and MVID.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
ComponentMetadataUnavailable = 3
```

### NotPrecompiled

The method is genuinely IL-only — not precompiled in this image.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
NotPrecompiled = 1
```

### OwnerCompositeMissing

The method belongs to a component whose owner composite executable is not on disk.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
OwnerCompositeMissing = 2
```

### Precompiled

The method has a precompiled native body that can be shown.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
Precompiled = 0
```

