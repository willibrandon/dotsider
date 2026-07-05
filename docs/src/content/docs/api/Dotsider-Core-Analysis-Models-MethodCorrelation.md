---
title: "MethodCorrelation"
description: "One pre-ILC managed method joined to its native evidence: the symbols that carry its compiled code and the mstat rows that carry its sizes."
slug: api/dotsider.core.analysis.models.methodcorrelation
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One pre-ILC managed method joined to its native evidence: the symbols that carry its
compiled code and the mstat rows that carry its sizes.

```csharp
public sealed record MethodCorrelation : IEquatable<MethodCorrelation>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MethodCorrelation**

## Implements

- [IEquatable\<MethodCorrelation\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MethodCorrelation(string, MethodDefInfo, MethodCorrelationStatus, IReadOnlyList\<NativeSymbol\>, IReadOnlyList\<MstatMethod\>)

One pre-ILC managed method joined to its native evidence: the symbols that carry its
compiled code and the mstat rows that carry its sizes.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly the method is defined in.
- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The managed method definition.
- `Status` ([MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)): How the method relates to the native image.
- `NativeSymbols` ([IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The joined native symbols — owned when exact (several mean generic instantiations), the shared candidate pool when ambiguous.
- `MstatMethods` ([IReadOnlyList\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The joined mstat rows, empty when no mstat sidecar was available.

```csharp
public MethodCorrelation(string AssemblyName, MethodDefInfo Method, MethodCorrelationStatus Status, IReadOnlyList<NativeSymbol> NativeSymbols, IReadOnlyList<MstatMethod> MstatMethods)
```

## Properties

### AssemblyName

The simple name of the assembly the method is defined in.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Method

The managed method definition.

**Returns:** [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

```csharp
public MethodDefInfo Method { get; init; }
```

### MstatMethods

The joined mstat rows, empty when no mstat sidecar was available.

**Returns:** [IReadOnlyList\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatMethod> MstatMethods { get; init; }
```

### NativeSize

The native size in bytes this method owns outright — mstat sizes preferred, symbol
sizes otherwise. Zero when the evidence is shared with overloads: shared bytes are
never attributed to any single candidate.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long NativeSize { get; init; }
```

### NativeSymbols

The joined native symbols — owned when exact (several mean generic instantiations), the shared candidate pool when ambiguous.

**Returns:** [IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeSymbol> NativeSymbols { get; init; }
```

### SharedCandidateSize

The size of the shared evidence pool this method is a candidate for, when
[Status](/api/dotsider.core.analysis.models.methodcorrelation.status/) reflects shared evidence. The same pool is reported on every
sibling candidate; aggregate accounting counts it once.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long SharedCandidateSize { get; init; }
```

### Status

How the method relates to the native image.

**Returns:** [MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)

```csharp
public MethodCorrelationStatus Status { get; init; }
```

