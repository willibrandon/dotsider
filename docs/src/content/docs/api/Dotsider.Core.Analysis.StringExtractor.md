---
title: "StringExtractor"
description: "Extracts strings from .NET assemblies across three sources: the #US heap (user string literals), the #Strings heap (metadata identifiers), and raw printable character sequences from the binary."
slug: api/dotsider.core.analysis.stringextractor
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Extracts strings from .NET assemblies across three sources:
the #US heap (user string literals), the #Strings heap (metadata identifiers),
and raw printable character sequences from the binary.

```csharp
public sealed class StringExtractor
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **StringExtractor**

## Constructors

### StringExtractor(AssemblyAnalyzer)

Extracts strings from .NET assemblies across three sources:
the #US heap (user string literals), the #Strings heap (metadata identifiers),
and raw printable character sequences from the binary.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): 

```csharp
public StringExtractor(AssemblyAnalyzer analyzer)
```

## Properties

### SkippedMetadataStringCount

Number of malformed entries skipped during the last [ExtractMetadataStrings](/api/dotsider.core.analysis.stringextractor.extractmetadatastrings/) call.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SkippedMetadataStringCount { get; }
```

### SkippedUserStringCount

Number of malformed entries skipped during the last [ExtractUserStrings](/api/dotsider.core.analysis.stringextractor.extractuserstrings/) call.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SkippedUserStringCount { get; }
```

## Methods

### ExtractMetadataStrings()

Extracts all identifier strings from the #Strings metadata heap.
These are type names, method names, namespace names, and other metadata identifiers.

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

A list of string entries from the metadata strings heap.

```csharp
public IReadOnlyList<StringEntry> ExtractMetadataStrings()
```

### ExtractRawStrings(int)

Extracts raw printable character sequences from the binary file.
Scans for consecutive ASCII printable characters (0x20-0x7E) of at least minLength bytes.

**Parameters:**

- `minLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The minimum number of consecutive printable characters to consider a string.

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

A list of string entries extracted from the raw binary.

```csharp
public IReadOnlyList<StringEntry> ExtractRawStrings(int minLength = 4)
```

### ExtractUserStrings()

Extracts all user string literals from the #US metadata heap.
These are the string constants used in IL code via `ldstr`.

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

A list of string entries from the user strings heap.

```csharp
public IReadOnlyList<StringEntry> ExtractUserStrings()
```

