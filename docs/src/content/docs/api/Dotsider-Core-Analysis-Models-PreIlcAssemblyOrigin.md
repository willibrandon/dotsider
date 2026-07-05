---
title: "PreIlcAssemblyOrigin"
description: "How the pre-ILC managed input of a Native AOT binary was located, ordered from most to least authoritative."
slug: api/dotsider.core.analysis.models.preilcassemblyorigin
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

How the pre-ILC managed input of a Native AOT binary was located, ordered from
most to least authoritative.

```csharp
public enum PreIlcAssemblyOrigin
```

## Fields

### BuildTreeLayout

Found at the SDK's conventional intermediate location for the recognized build
tree (`obj\&lt;cfg&gt;\&lt;tfm&gt;\&lt;rid&gt;`, or the artifacts-layout equivalent).

**Returns:** [PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)

```csharp
BuildTreeLayout = 2
```

### IlcResponseFile

Named as the root input of the ILC response file (`*.ilc.rsp`) — the exact
file the compiler consumed.

**Returns:** [PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)

```csharp
IlcResponseFile = 1
```

### None

No managed input was found; the result may still carry mstat/DGML paths.

**Returns:** [PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)

```csharp
None = 0
```

### SiblingAssembly

Found beside the binary itself — manual staging with no build provenance.

**Returns:** [PreIlcAssemblyOrigin](/api/dotsider.core.analysis.models.preilcassemblyorigin/)

```csharp
SiblingAssembly = 3
```

