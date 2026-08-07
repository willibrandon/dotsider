---
title: "AssemblyOpenResult.NativeAot"
description: "The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O with no COR header whose image embeds a validated ReadyToRun header. No metadata is available, but PE structure, native import/export/load-config directories, and raw strings are."
slug: api/dotsider.core.analysis.models.assemblyopenresult.nativeaot
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
with no COR header whose image embeds a validated ReadyToRun header. No
metadata is available, but PE structure, native import/export/load-config
directories, and raw strings are.

```csharp
public sealed record AssemblyOpenResult.NativeAot : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.NativeAot>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.NativeAot**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<NativeAot\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAot(AssemblyAnalyzer)

The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
with no COR header whose image embeds a validated ReadyToRun header. No
metadata is available, but PE structure, native import/export/load-config
directories, and raw strings are.

**Parameters:**

- `Analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the Native AOT binary (no metadata).

```csharp
public NativeAot(AssemblyAnalyzer Analyzer)
```

## Properties

### Analyzer

The analyzer for the Native AOT binary (no metadata).

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

### Equals(NativeAot?)

**Parameters:**

- `other` ([NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyOpenResult.NativeAot? other)
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

### operator !=(NativeAot?, NativeAot?)

**Parameters:**

- `left` ([NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/))
- `right` ([NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyOpenResult.NativeAot? left, AssemblyOpenResult.NativeAot? right)
```

### operator ==(NativeAot?, NativeAot?)

**Parameters:**

- `left` ([NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/))
- `right` ([NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyOpenResult.NativeAot? left, AssemblyOpenResult.NativeAot? right)
```
