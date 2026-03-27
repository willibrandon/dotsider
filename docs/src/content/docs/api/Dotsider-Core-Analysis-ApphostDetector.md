---
title: "ApphostDetector"
description: "Detects .NET apphost executables and locates their companion managed assemblies."
slug: api/dotsider.core.analysis.apphostdetector
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Detects .NET apphost executables and locates their companion managed assemblies.

```csharp
public static class ApphostDetector
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ApphostDetector**

## Methods

### FindCompanionDll(string)

If exePath ends with `.exe` and the binary is a .NET
apphost (embeds both the companion DLL name and a `hostfxr` reference),
returns the path to the companion `.dll` provided it has readable .NET metadata.

**Parameters:**

- `exePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the executable file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The full path to the companion managed `.dll`, or `null` if the file
is not an apphost, no companion exists, or the companion has no readable .NET
metadata.

```csharp
public static string? FindCompanionDll(string exePath)
```

## Remarks

`dotnet build` produces both a managed `.dll` (the actual assembly with IL and
metadata) and a native apphost `.exe` (a launcher that bootstraps the runtime).
The apphost has no CLR metadata, so most analysis tabs are empty. This detector
verifies the `.exe` is an apphost by requiring two signals: the companion DLL
name embedded in the binary AND a reference to `hostfxr` (the .NET host
framework resolver that every apphost imports to bootstrap the runtime).

