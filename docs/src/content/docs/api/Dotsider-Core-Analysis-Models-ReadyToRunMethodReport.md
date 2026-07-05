---
title: "ReadyToRunMethodReport"
description: "The resolved ReadyToRun correlation for one method, shared verbatim by the CLI --r2r-correlate option, the MCP correlate_r2r_method tool, and the session r2r-correlate command. Carries both rendered text and structured instruction arrays so programmatic callers get the IL and native code, not just formatted output."
slug: api/dotsider.core.analysis.models.readytorunmethodreport
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The resolved ReadyToRun correlation for one method, shared verbatim by the CLI
`--r2r-correlate` option, the MCP `correlate_r2r_method` tool, and the session
`r2r-correlate` command. Carries both rendered text and structured instruction arrays so
programmatic callers get the IL and native code, not just formatted output.

```csharp
public sealed record ReadyToRunMethodReport : IEquatable<ReadyToRunMethodReport>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunMethodReport**

## Implements

- [IEquatable\<ReadyToRunMethodReport\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunMethodReport(ReadyToRunNativeAvailability, string, Guid, string, int, bool, string?, bool, string?, IReadOnlyList\<CorrelationReportSymbol\>, long, string?, IReadOnlyList\<IlInstruction\>?, string?, IReadOnlyList\<NativeInstruction\>?, string?)

The resolved ReadyToRun correlation for one method, shared verbatim by the CLI
`--r2r-correlate` option, the MCP `correlate_r2r_method` tool, and the session
`r2r-correlate` command. Carries both rendered text and structured instruction arrays so
programmatic callers get the IL and native code, not just formatted output.

**Parameters:**

- `Availability` ([ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)): Why the method does or does not have inspectable native code.
- `Assembly` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning assembly's simple name.
- `Mvid` ([Guid](https://learn.microsoft.com/dotnet/api/system.guid)): The owning assembly's module version id (composite identity).
- `Method` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's display form: `DeclaringType::Name signature`.
- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.
- `IsComposite` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the image is composite.
- `OwnerComponent` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning component assembly for a composite, else null.
- `IsGenericInstantiation` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this entry is a generic instantiation.
- `InstantiationDisplay` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The rendered instantiation (e.g. `&lt;int&gt;`), or null.
- `Ranges` ([IReadOnlyList\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): One entry per native code range (hot entry, funclets, cold).
- `NativeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The total precompiled native code size.
- `Il` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's IL listing text, or null when metadata is unavailable.
- `IlInstructions` ([IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The structured IL instructions, or null.
- `NativeText` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The concatenated native disassembly across ranges, or null.
- `NativeInstructions` ([IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The structured native instructions across ranges, or null.
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable note for a non-[Precompiled](/api/dotsider.core.analysis.models.readytorunnativeavailability.precompiled/) availability, or null.

```csharp
public ReadyToRunMethodReport(ReadyToRunNativeAvailability Availability, string Assembly, Guid Mvid, string Method, int Token, bool IsComposite, string? OwnerComponent, bool IsGenericInstantiation, string? InstantiationDisplay, IReadOnlyList<CorrelationReportSymbol> Ranges, long NativeSize, string? Il, IReadOnlyList<IlInstruction>? IlInstructions, string? NativeText, IReadOnlyList<NativeInstruction>? NativeInstructions, string? Diagnostic)
```

## Properties

### Assembly

The owning assembly's simple name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Assembly { get; init; }
```

### Availability

Why the method does or does not have inspectable native code.

**Returns:** [ReadyToRunNativeAvailability](/api/dotsider.core.analysis.models.readytorunnativeavailability/)

```csharp
public ReadyToRunNativeAvailability Availability { get; init; }
```

### Diagnostic

A human-readable note for a non-[Precompiled](/api/dotsider.core.analysis.models.readytorunnativeavailability.precompiled/) availability, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Diagnostic { get; init; }
```

### Il

The method's IL listing text, or null when metadata is unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Il { get; init; }
```

### IlInstructions

The structured IL instructions, or null.

**Returns:** [IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<IlInstruction>? IlInstructions { get; init; }
```

### InstantiationDisplay

The rendered instantiation (e.g. `&lt;int&gt;`), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? InstantiationDisplay { get; init; }
```

### IsComposite

Whether the image is composite.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsComposite { get; init; }
```

### IsGenericInstantiation

Whether this entry is a generic instantiation.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsGenericInstantiation { get; init; }
```

### Method

The method's display form: `DeclaringType::Name signature`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Method { get; init; }
```

### Mvid

The owning assembly's module version id (composite identity).

**Returns:** [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

```csharp
public Guid Mvid { get; init; }
```

### NativeInstructions

The structured native instructions across ranges, or null.

**Returns:** [IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeInstruction>? NativeInstructions { get; init; }
```

### NativeSize

The total precompiled native code size.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long NativeSize { get; init; }
```

### NativeText

The concatenated native disassembly across ranges, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NativeText { get; init; }
```

### OwnerComponent

The owning component assembly for a composite, else null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwnerComponent { get; init; }
```

### Ranges

One entry per native code range (hot entry, funclets, cold).

**Returns:** [IReadOnlyList\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CorrelationReportSymbol> Ranges { get; init; }
```

### Token

The method's metadata token.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

