---
title: "SizeDiffPayloadBuilder"
description: "Builds the serializable payloads the diff-size and check-size-budgets surfaces return. The MCP server's direct mode and the running-session protocol handler both call these, so the two transports cannot drift apart in shape or semantics."
slug: api/dotsider.core.protocol.sizediffpayloadbuilder
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Builds the serializable payloads the `diff-size` and `check-size-budgets`
surfaces return. The MCP server's direct mode and the running-session protocol handler
both call these, so the two transports cannot drift apart in shape or semantics.

```csharp
public static class SizeDiffPayloadBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffPayloadBuilder**

## Methods

### BuildBudgetPayload(MstatSource, MstatSource?, IReadOnlyList\<SizeBudget\>, int?)

Builds the `check-size-budgets` payload: the basis-resolved totals and the budget
report. Growth budgets without a baseline are the caller's error to reject; this
builder evaluates what it is given.

**Parameters:**

- `target` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The build under check.
- `baseline` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The baseline, or null for an absolute-only gate.
- `budgets` ([IReadOnlyList\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The budgets to evaluate.
- `topN` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): Contributors per violated budget, or null for [DefaultTopN](/api/dotsider.core.protocol.sizediffpayloadbuilder.defaulttopn/).

**Returns:** [Object](https://learn.microsoft.com/dotnet/api/system.object)

The serializable payload.

```csharp
public static object BuildBudgetPayload(MstatSource target, MstatSource? baseline, IReadOnlyList<SizeBudget> budgets, int? topN)
```

### BuildDiffPayload(MstatSource, MstatSource, int?, bool, int?)

Builds the `diff-size` payload: the diff's summary, aggregates, and top
contributors, plus — only on request — the delta tree, pruned depth-first by absolute
delta to a node cap with explicit truncation metadata, because a full tree for a real
application is enormous.

**Parameters:**

- `left` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The baseline input.
- `right` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The input under comparison.
- `topN` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): How many contributors to include, or null for [DefaultTopN](/api/dotsider.core.protocol.sizediffpayloadbuilder.defaulttopn/).
- `includeTree` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether to include the delta tree.
- `maxNodes` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The tree node cap, or null for [DefaultMaxNodes](/api/dotsider.core.protocol.sizediffpayloadbuilder.defaultmaxnodes/).

**Returns:** [Object](https://learn.microsoft.com/dotnet/api/system.object)

The serializable payload.

```csharp
public static object BuildDiffPayload(MstatSource left, MstatSource right, int? topN, bool includeTree, int? maxNodes)
```

## Fields

### DefaultMaxNodes

The default delta-tree node cap when a caller asks for the tree without one.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int DefaultMaxNodes = 500
```

### DefaultTopN

The default contributor count when a caller does not pin one.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int DefaultTopN = 20
```

