---
title: "AssemblyOpenResult.Direct"
description: "Direct load — the file is a .dll or .exe with metadata, a raw WebAssembly module, or a native binary with no metadata and no ReadyToRun header (unknown format)."
slug: api/dotsider.core.analysis.models.assemblyopenresult.direct
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Direct load — the file is a .dll or .exe with metadata, a raw WebAssembly module,
or a native binary with no metadata and no ReadyToRun header (unknown format).

```csharp
public sealed record AssemblyOpenResult.Direct : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.Direct>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.Direct**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Direct\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Direct(AssemblyAnalyzer)

Direct load — the file is a .dll or .exe with metadata, a raw WebAssembly module,
or a native binary with no metadata and no ReadyToRun header (unknown format).

**Parameters:**

- `Analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the opened file.

```csharp
public Direct(AssemblyAnalyzer Analyzer)
```

## Properties

### Analyzer

The analyzer for the opened file.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer Analyzer { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

## Methods

### Deconstruct(out AssemblyAnalyzer)

**Parameters:**

- `Analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))

```csharp
public void Deconstruct(out AssemblyAnalyzer Analyzer)
```

### Equals(AssemblyOpenResult?)

**Parameters:**

- `other` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(AssemblyOpenResult? other)
```

### Equals(Direct?)

**Parameters:**

- `other` ([Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyOpenResult.Direct? other)
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

### operator !=(Direct?, Direct?)

**Parameters:**

- `left` ([Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/))
- `right` ([Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyOpenResult.Direct? left, AssemblyOpenResult.Direct? right)
```

### operator ==(Direct?, Direct?)

**Parameters:**

- `left` ([Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/))
- `right` ([Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyOpenResult.Direct? left, AssemblyOpenResult.Direct? right)
```
