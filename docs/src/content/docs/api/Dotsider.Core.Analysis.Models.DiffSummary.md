---
title: "DiffSummary"
description: "Summary statistics for the diff."
slug: api/dotsider.core.analysis.models.diffsummary
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Summary statistics for the diff.

```csharp
public sealed record DiffSummary : IEquatable<DiffSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DiffSummary**

## Implements

- [IEquatable\<DiffSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DiffSummary(int, int, int, int, int, int, int, int, int, long)

Summary statistics for the diff.

**Parameters:**

- `TypesAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of types present only in the right assembly.
- `TypesRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of types present only in the left assembly.
- `TypesChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of types that differ between assemblies.
- `MethodsAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of methods present only in the right assembly.
- `MethodsRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of methods present only in the left assembly.
- `MethodsChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of methods that differ between assemblies.
- `RefsAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of assembly references present only in the right assembly.
- `RefsRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of assembly references present only in the left assembly.
- `RefsChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of assembly references that differ between assemblies.
- `SizeDelta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): File size difference in bytes (positive means the right assembly is larger).

```csharp
public DiffSummary(int TypesAdded, int TypesRemoved, int TypesChanged, int MethodsAdded, int MethodsRemoved, int MethodsChanged, int RefsAdded, int RefsRemoved, int RefsChanged, long SizeDelta)
```

## Properties

### MethodsAdded

Number of methods present only in the right assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsAdded { get; init; }
```

### MethodsChanged

Number of methods that differ between assemblies.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsChanged { get; init; }
```

### MethodsRemoved

Number of methods present only in the left assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsRemoved { get; init; }
```

### RefsAdded

Number of assembly references present only in the right assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsAdded { get; init; }
```

### RefsChanged

Number of assembly references that differ between assemblies.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsChanged { get; init; }
```

### RefsRemoved

Number of assembly references present only in the left assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsRemoved { get; init; }
```

### SizeDelta

File size difference in bytes (positive means the right assembly is larger).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long SizeDelta { get; init; }
```

### TypesAdded

Number of types present only in the right assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesAdded { get; init; }
```

### TypesChanged

Number of types that differ between assemblies.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesChanged { get; init; }
```

### TypesRemoved

Number of types present only in the left assembly.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesRemoved { get; init; }
```

