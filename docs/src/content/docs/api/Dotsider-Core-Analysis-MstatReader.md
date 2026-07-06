---
title: "MstatReader"
description: "Reads an ILC size report (.mstat), the file IlcGenerateMstatFile emits when publishing a Native AOT project. The report is itself a valid ECMA-335 assembly: its assembly version carries the format version, and its data is encoded as IL instruction streams in global methods named Methods, Types, Blobs, and (in newer formats) RvaFields, FrozenObjects, ManifestResources, and DeduplicatedMethods. Format 2.0+ also stores each entry's dependency-graph node name in a custom .names PE section; those names equal the node labels in the DGML graphs IlcGenerateDgmlFile emits, which is how sizes join to dependency chains. Malformed input never throws: unreadable files return null, and a truncated IL stream yields the entries parsed before the damage."
slug: api/dotsider.core.analysis.mstatreader
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Reads an ILC size report (`.mstat`), the file `IlcGenerateMstatFile` emits when
publishing a Native AOT project. The report is itself a valid ECMA-335 assembly: its
assembly version carries the format version, and its data is encoded as IL instruction
streams in global methods named `Methods`, `Types`, `Blobs`, and (in newer
formats) `RvaFields`, `FrozenObjects`, `ManifestResources`, and
`DeduplicatedMethods`. Format 2.0+ also stores each entry's dependency-graph node name
in a custom `.names` PE section; those names equal the node labels in the DGML graphs
`IlcGenerateDgmlFile` emits, which is how sizes join to dependency chains.

Malformed input never throws: unreadable files return null, and a truncated IL stream
yields the entries parsed before the damage.

```csharp
public static class MstatReader
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatReader**

## Methods

### Probe(Stream)

Cheaply tests whether a stream looks like an ILC size report. The stream is left open.
See [String)](/api/dotsider.core.analysis.mstatreader.probe(system.string)/) for what the probe checks.

**Parameters:**

- `stream` ([Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)): A readable, seekable stream positioned at the start of the file.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

True when the content plausibly is an mstat; false otherwise.

```csharp
public static bool Probe(Stream stream)
```

### Probe(string)

Cheaply tests whether a file looks like an ILC size report, without decoding any IL
streams or node names: the PE must carry metadata, the assembly version major must be a
known format version, and `&lt;Module&gt;` must declare a global `Methods` or
`Types` method. A positive probe is a sniff, not a guarantee — follow it with
[String)](/api/dotsider.core.analysis.mstatreader.read(system.string)/) for the decoded report. Never throws.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the candidate file.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

True when the file plausibly is an mstat; false otherwise.

```csharp
public static bool Probe(string filePath)
```

### Read(Stream)

Reads an ILC size report from a stream. The stream is left open.

**Parameters:**

- `stream` ([Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)): A readable, seekable stream positioned at the start of the file.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

The decoded report, or null when the content is not an mstat.

```csharp
public static MstatData? Read(Stream stream)
```

### Read(string)

Reads an ILC size report from a file.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the `.mstat` file.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

The decoded report, or null when the file is missing or is not an mstat.

```csharp
public static MstatData? Read(string filePath)
```

