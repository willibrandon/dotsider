---
title: "PreIlcSidecarDetector"
description: "Locates the pre-ILC build outputs of a Native AOT binary: the managed input assembly the compiler consumed, its portable PDB, and the mstat/DGML sidecars in the build's intermediate tree."
slug: api/dotsider.core.analysis.preilcsidecardetector
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Locates the pre-ILC build outputs of a Native AOT binary: the managed input assembly
the compiler consumed, its portable PDB, and the mstat/DGML sidecars in the build's
intermediate tree.

```csharp
public static class PreIlcSidecarDetector
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PreIlcSidecarDetector**

## Methods

### Find(string)

Probes for the pre-ILC sidecars of the Native AOT binary at
binaryPath.

**Parameters:**

- `binaryPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the Native AOT executable or library.

**Returns:** [PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/)

The discovered sidecars — possibly without an attachable managed assembly when only
mstat/DGML files were found — or `null` when nothing was found at all. The
caller is responsible for having established that the binary is Native AOT.

```csharp
public static PreIlcSidecars? Find(string binaryPath)
```

## Remarks

An AOT publish leaves its inputs behind: the managed assembly and portable PDB in the
intermediate directory (`obj\&lt;cfg&gt;\&lt;tfm&gt;\&lt;rid&gt;`, or the artifacts-layout
equivalent), and an ILC response file (`*.ilc.rsp`) in `native\` whose first
non-switch token names the exact root input. The probe tries three origins in
authority order — response file, conventional intermediate location, sibling file —
validating each candidate (readable CLR metadata, identity, never the binary itself)
and falling through on failure. Recognition is positional segment mapping, so custom
configuration names, TFMs, RIDs, and artifacts pivots all work without being parsed.
The probe never throws; failures degrade to a smaller result or null.
