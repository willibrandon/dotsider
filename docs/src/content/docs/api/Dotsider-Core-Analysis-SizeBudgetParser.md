---
title: "SizeBudgetParser"
description: "Parses size-budget spec strings. The grammar is [scope:]limit(,limit)* where scope is total (the default), ns=NAME, or asm=NAME, and each limit is max=SIZE or growth=SIZE|PERCENT. Sizes accept b, kb, mb, and gb suffixes (1 kb = 1024 bytes; a bare number is bytes); percentages (growth=1%) apply to growth only. Examples: max=25mb · growth=1% · total:max=25mb,growth=50kb · ns=System.Text.Json:growth=10kb · asm=MyApp:max=2mb."
slug: api/dotsider.core.analysis.sizebudgetparser
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Parses size-budget spec strings. The grammar is
`[scope:]limit(,limit)*` where scope is `total` (the default), `ns=NAME`, or
`asm=NAME`, and each limit is `max=SIZE` or `growth=SIZE|PERCENT`. Sizes
accept `b`, `kb`, `mb`, and `gb` suffixes (1 kb = 1024 bytes; a bare
number is bytes); percentages (`growth=1%`) apply to growth only. Examples:
`max=25mb` · `growth=1%` · `total:max=25mb,growth=50kb` ·
`ns=System.Text.Json:growth=10kb` · `asm=MyApp:max=2mb`.

```csharp
public static class SizeBudgetParser
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetParser**

## Methods

### Parse(string)

Parses one budget spec.

**Parameters:**

- `spec` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The spec string.

**Returns:** [SizeBudget](/api/dotsider.core.analysis.models.sizebudget/)

The parsed budget, at error severity.

**Exceptions:**

- [FormatException](https://learn.microsoft.com/dotnet/api/system.formatexception): The spec does not match the grammar; the message names the offending part.

```csharp
public static SizeBudget Parse(string spec)
```
