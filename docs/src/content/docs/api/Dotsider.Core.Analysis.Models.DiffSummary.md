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

- `TypesAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `TypesRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `TypesChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `MethodsAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `MethodsRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `MethodsChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `RefsAdded` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `RefsRemoved` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `RefsChanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `SizeDelta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 

```csharp
public DiffSummary(int TypesAdded, int TypesRemoved, int TypesChanged, int MethodsAdded, int MethodsRemoved, int MethodsChanged, int RefsAdded, int RefsRemoved, int RefsChanged, long SizeDelta)
```

## Properties

### MethodsAdded

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsAdded { get; init; }
```

### MethodsChanged

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsChanged { get; init; }
```

### MethodsRemoved

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodsRemoved { get; init; }
```

### RefsAdded

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsAdded { get; init; }
```

### RefsChanged

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsChanged { get; init; }
```

### RefsRemoved

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RefsRemoved { get; init; }
```

### SizeDelta

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long SizeDelta { get; init; }
```

### TypesAdded

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesAdded { get; init; }
```

### TypesChanged

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesChanged { get; init; }
```

### TypesRemoved

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypesRemoved { get; init; }
```

