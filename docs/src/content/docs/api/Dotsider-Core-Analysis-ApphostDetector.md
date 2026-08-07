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

### FindBundledEntryAssembly(string)

If the file at exePath is a single-file bundle, extracts the
entry assembly (the app's own managed code) and returns its bytes and name.
Uses dotted-name-safe basename matching to locate the entry assembly within
the bundle manifest.

**Parameters:**

- `exePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the executable file.

**Returns:** [Nullable\<Byte[], String\>\>](https://learn.microsoft.com/dotnet/api/system.nullable-2)

The entry assembly bytes and file name, or `null` if the file is not a
single-file bundle or the entry assembly could not be identified.

```csharp
public static (byte[] Bytes, string Name)? FindBundledEntryAssembly(string exePath)
```

### FindCompanionDll(string)

If the binary at exePath is a .NET apphost (embeds both the
companion DLL name and a `hostfxr` reference), returns the path to the
companion `.dll` provided it has readable .NET metadata. Works with Windows
`.exe` files and extensionless Linux/macOS executables.

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
metadata) and a native apphost launcher that bootstraps the runtime. On Windows the
apphost is a `.exe` (PE format); on Linux and macOS it is an extensionless
executable (ELF or Mach-O). The apphost has no CLR metadata, so most analysis tabs
are empty. This detector verifies the file is an apphost by requiring two signals:
the companion DLL name embedded in the binary AND a reference to `hostfxr`
(the .NET host framework resolver). These signals are platform-invariant — the
.NET SDK embeds them identically regardless of binary format.
