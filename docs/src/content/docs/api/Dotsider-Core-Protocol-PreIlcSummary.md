---
title: "PreIlcSummary"
description: "Compact provenance for the managed inputs used to produce a Native AOT binary. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.preilcsummary
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Compact provenance for the managed inputs used to produce a Native AOT binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record PreIlcSummary : IEquatable<PreIlcSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PreIlcSummary**

## Implements

- [IEquatable\<PreIlcSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### PreIlcSummary(bool, string?, string, string, bool, bool, int, int, int)

Compact provenance for the managed inputs used to produce a Native AOT binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `HasAttachableCompanion` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `RootAssembly` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Origin` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PdbStatus` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMstat` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasDgml` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `LocalReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `PackageReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `OtherReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public PreIlcSummary(bool HasAttachableCompanion, string? RootAssembly, string Origin, string PdbStatus, bool HasMstat, bool HasDgml, int LocalReferenceCount, int PackageReferenceCount, int OtherReferenceCount)
```

## Properties

### HasAttachableCompanion

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasAttachableCompanion { get; init; }
```

### HasDgml

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasDgml { get; init; }
```

### HasMstat

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasMstat { get; init; }
```

### LocalReferenceCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int LocalReferenceCount { get; init; }
```

### Origin

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Origin { get; init; }
```

### OtherReferenceCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int OtherReferenceCount { get; init; }
```

### PackageReferenceCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int PackageReferenceCount { get; init; }
```

### PdbStatus

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string PdbStatus { get; init; }
```

### RootAssembly

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? RootAssembly { get; init; }
```

## Methods

### Deconstruct(out bool, out string?, out string, out string, out bool, out bool, out int, out int, out int)

**Parameters:**

- `HasAttachableCompanion` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `RootAssembly` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Origin` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PdbStatus` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMstat` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasDgml` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `LocalReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `PackageReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `OtherReferenceCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out bool HasAttachableCompanion, out string? RootAssembly, out string Origin, out string PdbStatus, out bool HasMstat, out bool HasDgml, out int LocalReferenceCount, out int PackageReferenceCount, out int OtherReferenceCount)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(PreIlcSummary?)

**Parameters:**

- `other` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(PreIlcSummary? other)
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

### operator !=(PreIlcSummary?, PreIlcSummary?)

**Parameters:**

- `left` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))
- `right` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(PreIlcSummary? left, PreIlcSummary? right)
```

### operator ==(PreIlcSummary?, PreIlcSummary?)

**Parameters:**

- `left` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))
- `right` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(PreIlcSummary? left, PreIlcSummary? right)
```
