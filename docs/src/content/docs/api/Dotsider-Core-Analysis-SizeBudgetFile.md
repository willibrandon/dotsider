---
title: "SizeBudgetFile"
description: "Reads a size-budget document: { \"budgets\": [ ... ] } where each entry is either a spec string in the SizeBudgetParser grammar or an object { \"name\", \"description\", \"scope\", \"max\", \"growth\", \"severity\", \"topN\" } — the object form is how a team names its budgets, downgrades one to a warning, or pins a per-budget contributor count. Both forms mix freely in one document. The CLI's --budget-file and the MCP server's inline budget JSON share this one parser."
slug: api/dotsider.core.analysis.sizebudgetfile
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Reads a size-budget document: `{ "budgets": [ ... ] }` where each entry is either a
spec string in the [SizeBudgetParser](/api/dotsider.core.analysis.sizebudgetparser/) grammar or an object
`{ "name", "description", "scope", "max", "growth", "severity", "topN" }` — the object
form is how a team names its budgets, downgrades one to a warning, or pins a per-budget
contributor count. Both forms mix freely in one document. The CLI's `--budget-file`
and the MCP server's inline budget JSON share this one parser.

```csharp
public static class SizeBudgetFile
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetFile**

## Methods

### Load(string)

Loads a budget document from a file.

**Parameters:**

- `path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The path of the JSON document.

**Returns:** [IReadOnlyList\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

The parsed budgets, in document order.

```csharp
public static IReadOnlyList<SizeBudget> Load(string path)
```

### Parse(string)

Parses a budget document from its JSON text.

**Parameters:**

- `json` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The document text.

**Returns:** [IReadOnlyList\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

The parsed budgets, in document order.

```csharp
public static IReadOnlyList<SizeBudget> Parse(string json)
```

