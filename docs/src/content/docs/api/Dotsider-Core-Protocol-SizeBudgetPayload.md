---
title: "SizeBudgetPayload"
description: "Size-budget results for one mstat-backed input. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.sizebudgetpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Size-budget results for one mstat-backed input.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SizeBudgetPayload : IEquatable<SizeBudgetPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetPayload**

## Implements

- [IEquatable\<SizeBudgetPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeBudgetPayload(string, string?, bool, bool, SizeBasis, long, long, long?, long?, IReadOnlyList\<SizeBudgetEvaluation\>)

Size-budget results for one mstat-backed input.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Baseline` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasWarnings` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `LeftTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `RightMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Evaluations` ([IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public SizeBudgetPayload(string Target, string? Baseline, bool Passed, bool HasWarnings, SizeBasis TotalBasis, long LeftTotal, long RightTotal, long? LeftMstatTotal, long? RightMstatTotal, IReadOnlyList<SizeBudgetEvaluation> Evaluations)
```

## Properties

### Baseline

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Baseline { get; init; }
```

### Evaluations

**Returns:** [IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeBudgetEvaluation> Evaluations { get; init; }
```

### HasWarnings

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasWarnings { get; init; }
```

### LeftMstatTotal

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? LeftMstatTotal { get; init; }
```

### LeftTotal

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftTotal { get; init; }
```

### Passed

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Passed { get; init; }
```

### RightMstatTotal

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? RightMstatTotal { get; init; }
```

### RightTotal

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightTotal { get; init; }
```

### Target

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Target { get; init; }
```

### TotalBasis

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
public SizeBasis TotalBasis { get; init; }
```

## Methods

### Deconstruct(out string, out string?, out bool, out bool, out SizeBasis, out long, out long, out long?, out long?, out IReadOnlyList\<SizeBudgetEvaluation\>)

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Baseline` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasWarnings` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `LeftTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `RightMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Evaluations` ([IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Target, out string? Baseline, out bool Passed, out bool HasWarnings, out SizeBasis TotalBasis, out long LeftTotal, out long RightTotal, out long? LeftMstatTotal, out long? RightMstatTotal, out IReadOnlyList<SizeBudgetEvaluation> Evaluations)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeBudgetPayload?)

**Parameters:**

- `other` ([SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeBudgetPayload? other)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(SizeBudgetPayload?, SizeBudgetPayload?)

**Parameters:**

- `left` ([SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/))
- `right` ([SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeBudgetPayload? left, SizeBudgetPayload? right)
```

### operator ==(SizeBudgetPayload?, SizeBudgetPayload?)

**Parameters:**

- `left` ([SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/))
- `right` ([SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeBudgetPayload? left, SizeBudgetPayload? right)
```
