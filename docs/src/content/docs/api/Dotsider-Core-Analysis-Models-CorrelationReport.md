---
title: "CorrelationReport"
description: "The resolved correlation payload shared verbatim by every programmatic surface — the CLI --correlate option, the session correlate-method command, and the MCP correlate_method tool — so a method's pre-ILC IL and its native code are reported identically wherever they are requested."
slug: api/dotsider.core.analysis.models.correlationreport
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The resolved correlation payload shared verbatim by every programmatic surface — the CLI
`--correlate` option, the session `correlate-method` command, and the MCP
`correlate_method` tool — so a method's pre-ILC IL and its native code are reported
identically wherever they are requested.

```csharp
public sealed record CorrelationReport : IEquatable<CorrelationReport>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CorrelationReport**

## Implements

- [IEquatable\<CorrelationReport\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CorrelationReport(string, string, string, int, IReadOnlyList\<CorrelationReportSymbol\>, long, long, long, string?, string?)

The resolved correlation payload shared verbatim by every programmatic surface — the CLI
`--correlate` option, the session `correlate-method` command, and the MCP
`correlate_method` tool — so a method's pre-ILC IL and its native code are reported
identically wherever they are requested.

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The correlation status name (exact, ambiguous, size-only, not-in-image).
- `Assembly` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly the method is defined in.
- `Method` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's display form: `DeclaringType::Name signature`.
- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.
- `Symbols` ([IReadOnlyList\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The native symbols carrying the method's code — several mean generic instantiations, or a shared overload pool when ambiguous.
- `NativeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The native bytes the method owns outright, or 0 when its evidence is shared.
- `SharedCandidateSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The size of the shared evidence pool when the correlation is ambiguous, otherwise 0.
- `MstatSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The total mstat-reported native size, or 0 when no mstat sidecar was available.
- `Il` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's IL listing from the pre-ILC assembly, or null when it has no metadata body.
- `NativeDisassembly` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The correlation-aware native disassembly across all symbols, or null when no symbol is disassemblable.

```csharp
public CorrelationReport(string Status, string Assembly, string Method, int Token, IReadOnlyList<CorrelationReportSymbol> Symbols, long NativeSize, long SharedCandidateSize, long MstatSize, string? Il, string? NativeDisassembly)
```

## Properties

### Assembly

The simple name of the assembly the method is defined in.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Assembly { get; init; }
```

### Il

The method's IL listing from the pre-ILC assembly, or null when it has no metadata body.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Il { get; init; }
```

### Method

The method's display form: `DeclaringType::Name signature`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Method { get; init; }
```

### MstatSize

The total mstat-reported native size, or 0 when no mstat sidecar was available.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long MstatSize { get; init; }
```

### NativeDisassembly

The correlation-aware native disassembly across all symbols, or null when no symbol is disassemblable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NativeDisassembly { get; init; }
```

### NativeSize

The native bytes the method owns outright, or 0 when its evidence is shared.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long NativeSize { get; init; }
```

### SharedCandidateSize

The size of the shared evidence pool when the correlation is ambiguous, otherwise 0.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long SharedCandidateSize { get; init; }
```

### Status

The correlation status name (exact, ambiguous, size-only, not-in-image).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Status { get; init; }
```

### Symbols

The native symbols carrying the method's code — several mean generic instantiations, or a shared overload pool when ambiguous.

**Returns:** [IReadOnlyList\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CorrelationReportSymbol> Symbols { get; init; }
```

### Token

The method's metadata token.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out int, out IReadOnlyList\<CorrelationReportSymbol\>, out long, out long, out long, out string?, out string?)

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Assembly` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Method` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Symbols` ([IReadOnlyList\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `NativeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `SharedCandidateSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `MstatSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Il` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NativeDisassembly` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Status, out string Assembly, out string Method, out int Token, out IReadOnlyList<CorrelationReportSymbol> Symbols, out long NativeSize, out long SharedCandidateSize, out long MstatSize, out string? Il, out string? NativeDisassembly)
```

### Equals(CorrelationReport?)

**Parameters:**

- `other` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CorrelationReport? other)
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

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(CorrelationReport?, CorrelationReport?)

**Parameters:**

- `left` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))
- `right` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CorrelationReport? left, CorrelationReport? right)
```

### operator ==(CorrelationReport?, CorrelationReport?)

**Parameters:**

- `left` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))
- `right` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CorrelationReport? left, CorrelationReport? right)
```
