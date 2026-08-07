---
title: "AssemblyOpenResult.ApphostWithCompanion"
description: "The file is a native apphost with a companion managed .dll on disk. The caller decides when to redirect (e.g. showing a dialog first)."
slug: api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a native apphost with a companion managed .dll on disk.
The caller decides when to redirect (e.g. showing a dialog first).

```csharp
public sealed record AssemblyOpenResult.ApphostWithCompanion : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.ApphostWithCompanion>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.ApphostWithCompanion**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ApphostWithCompanion\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ApphostWithCompanion(AssemblyAnalyzer, string)

The file is a native apphost with a companion managed .dll on disk.
The caller decides when to redirect (e.g. showing a dialog first).

**Parameters:**

- `HostAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the native apphost (no metadata).
- `CompanionDllPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the companion managed .dll.

```csharp
public ApphostWithCompanion(AssemblyAnalyzer HostAnalyzer, string CompanionDllPath)
```

## Properties

### CompanionDllPath

Full path to the companion managed .dll.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string CompanionDllPath { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### HostAnalyzer

The analyzer for the native apphost (no metadata).

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer HostAnalyzer { get; init; }
```

## Methods

### Deconstruct(out AssemblyAnalyzer, out string)

**Parameters:**

- `HostAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `CompanionDllPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out AssemblyAnalyzer HostAnalyzer, out string CompanionDllPath)
```

### Equals(ApphostWithCompanion?)

**Parameters:**

- `other` ([ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyOpenResult.ApphostWithCompanion? other)
```

### Equals(AssemblyOpenResult?)

**Parameters:**

- `other` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(AssemblyOpenResult? other)
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

### operator !=(ApphostWithCompanion?, ApphostWithCompanion?)

**Parameters:**

- `left` ([ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/))
- `right` ([ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyOpenResult.ApphostWithCompanion? left, AssemblyOpenResult.ApphostWithCompanion? right)
```

### operator ==(ApphostWithCompanion?, ApphostWithCompanion?)

**Parameters:**

- `left` ([ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/))
- `right` ([ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyOpenResult.ApphostWithCompanion? left, AssemblyOpenResult.ApphostWithCompanion? right)
```
