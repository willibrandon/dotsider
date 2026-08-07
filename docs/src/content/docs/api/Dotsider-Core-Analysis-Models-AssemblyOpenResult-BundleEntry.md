---
title: "AssemblyOpenResult.BundleEntry"
description: "The file is a single-file bundle. The entry assembly has been extracted from the bundle and is ready for analysis."
slug: api/dotsider.core.analysis.models.assemblyopenresult.bundleentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a single-file bundle. The entry assembly has been extracted
from the bundle and is ready for analysis.

```csharp
public sealed record AssemblyOpenResult.BundleEntry : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.BundleEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.BundleEntry**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleEntry(AssemblyAnalyzer, string)

The file is a single-file bundle. The entry assembly has been extracted
from the bundle and is ready for analysis.

**Parameters:**

- `EntryAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the extracted entry assembly.
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the bundle file.

```csharp
public BundleEntry(AssemblyAnalyzer EntryAnalyzer, string BundlePath)
```

## Properties

### BundlePath

Full path to the bundle file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string BundlePath { get; init; }
```

### EntryAnalyzer

The analyzer for the extracted entry assembly.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer EntryAnalyzer { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

## Methods

### Deconstruct(out AssemblyAnalyzer, out string)

**Parameters:**

- `EntryAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out AssemblyAnalyzer EntryAnalyzer, out string BundlePath)
```

### Equals(AssemblyOpenResult?)

**Parameters:**

- `other` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(AssemblyOpenResult? other)
```

### Equals(BundleEntry?)

**Parameters:**

- `other` ([BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyOpenResult.BundleEntry? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### PrintMembers(StringBuilder)

**Parameters:**

- `builder` ([StringBuilder](https://learn.microsoft.com/dotnet/api/system.text.stringbuilder))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
protected override bool PrintMembers(StringBuilder builder)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(BundleEntry?, BundleEntry?)

**Parameters:**

- `left` ([BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/))
- `right` ([BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyOpenResult.BundleEntry? left, AssemblyOpenResult.BundleEntry? right)
```

### operator ==(BundleEntry?, BundleEntry?)

**Parameters:**

- `left` ([BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/))
- `right` ([BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyOpenResult.BundleEntry? left, AssemblyOpenResult.BundleEntry? right)
```
