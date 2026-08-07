---
title: "SingleFileBundleReader"
description: "Reads .NET single-file bundles — detects the bundle signature, parses the manifest header, and extracts individual entries."
slug: api/dotsider.core.analysis.singlefilebundlereader
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Reads .NET single-file bundles — detects the bundle signature, parses the
manifest header, and extracts individual entries.

```csharp
public static class SingleFileBundleReader
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SingleFileBundleReader**

## Methods

### FindEntryAssembly(string)

Detects a single-file bundle and extracts the entry assembly (the app's own managed code).
Uses dotted-name-safe basename matching: for `.exe` files, strips the extension;
for extensionless files, appends `.dll` to the full filename.

**Parameters:**

- `bundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the potential bundle file.

**Returns:** [Nullable\<Byte[], String\>\>](https://learn.microsoft.com/dotnet/api/system.nullable-2)

The entry assembly bytes and name, or `null` if the file is not a bundle,
the manifest is invalid, or no entry assembly could be identified.

```csharp
public static (byte[] Bytes, string Name)? FindEntryAssembly(string bundlePath)
```

### IsBundle(ReadOnlySpan\<byte\>, out long)

Checks whether the raw bytes contain the .NET single-file bundle signature.

**Parameters:**

- `data` ([ReadOnlySpan\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlyspan-1)): The raw file bytes.
- `headerOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): When this method returns `true`, contains the byte offset of the bundle header.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

`true` if the signature is found; otherwise `false`.

```csharp
public static bool IsBundle(ReadOnlySpan<byte> data, out long headerOffset)
```

### IsBundle(string, out long)

Checks whether the file at filePath is a .NET single-file bundle.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the file to inspect.
- `headerOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): When this method returns `true`, contains the byte offset of the bundle header.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

`true` if the file contains the bundle signature; otherwise `false`.

```csharp
public static bool IsBundle(string filePath, out long headerOffset)
```

### ReadAssembly(string, BundleManifest, string)

Finds and reads an assembly entry by assembly name (without extension).

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the bundle file.
- `manifest` ([BundleManifest](/api/dotsider.core.analysis.models.bundlemanifest/)): The bundle manifest.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly name without extension (e.g. "System.Runtime").

**Returns:** [Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])

The assembly bytes, or `null` if the entry is not found, is unsafe, or cannot be read.

```csharp
public static byte[]? ReadAssembly(string filePath, BundleManifest manifest, string assemblyName)
```

### ReadEntry(string, BundleManifest, string)

Reads a specific entry's raw bytes from the bundle.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the bundle file.
- `manifest` ([BundleManifest](/api/dotsider.core.analysis.models.bundlemanifest/)): The bundle manifest.
- `entryRelativePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The [RelativePath](/api/dotsider.core.analysis.models.bundleentry.relativepath/) to read.

**Returns:** [Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])

The entry's bytes, or `null` if the entry was not found, is unsafe, or cannot be read.

```csharp
public static byte[]? ReadEntry(string filePath, BundleManifest manifest, string entryRelativePath)
```

### ReadManifest(Stream)

Reads the bundle manifest from a readable, seekable stream positioned at the header.

**Parameters:**

- `stream` ([Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)): A readable, seekable stream positioned at the bundle header offset.

**Returns:** [BundleManifest](/api/dotsider.core.analysis.models.bundlemanifest/)

The parsed bundle manifest.

**Exceptions:**

- [InvalidDataException](https://learn.microsoft.com/dotnet/api/system.io.invaliddataexception): The stream is unsuitable, the manifest is malformed, or the bundle version is unsupported.

```csharp
public static BundleManifest ReadManifest(Stream stream)
```

### ReadManifest(string, long)

Reads the bundle manifest from the file at filePath.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path to the bundle file.
- `headerOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The byte offset of the bundle header, as returned by Int64%40).

**Returns:** [BundleManifest](/api/dotsider.core.analysis.models.bundlemanifest/)

The parsed bundle manifest.

**Exceptions:**

- [InvalidDataException](https://learn.microsoft.com/dotnet/api/system.io.invaliddataexception): The header offset is invalid, the manifest is malformed, or the bundle version is unsupported.

```csharp
public static BundleManifest ReadManifest(string filePath, long headerOffset)
```
