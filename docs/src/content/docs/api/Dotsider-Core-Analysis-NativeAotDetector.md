---
title: "NativeAotDetector"
description: "Detects Native AOT compiled .NET binaries by locating and validating the embedded ReadyToRun header. Every Native AOT image carries this header (signature \"RTR\\0\") so the runtime can find its module sections, but the signature bytes also occur as code immediates, so each candidate is validated against the field ranges the ILC toolchain actually emits before it is accepted."
slug: api/dotsider.core.analysis.nativeaotdetector
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Detects Native AOT compiled .NET binaries by locating and validating the embedded
ReadyToRun header. Every Native AOT image carries this header (signature "RTR\0")
so the runtime can find its module sections, but the signature bytes also occur as
code immediates, so each candidate is validated against the field ranges the ILC
toolchain actually emits before it is accepted.

```csharp
public static class NativeAotDetector
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotDetector**

## Methods

### Detect(ReadOnlySpan\<byte\>)

Probes the given binary image for a validated ReadyToRun header.

**Parameters:**

- `bytes` ([ReadOnlySpan\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlyspan-1)): The raw bytes of the binary file.

**Returns:** [NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/)

The extracted header facts when the image is a PE, ELF, or Mach-O binary
containing a validated ReadyToRun header; otherwise null. Callers decide
what the result means in combination with metadata presence — a managed
R2R assembly also embeds this header, so probe only metadata-less files.

```csharp
public static NativeAotInfo? Detect(ReadOnlySpan<byte> bytes)
```
