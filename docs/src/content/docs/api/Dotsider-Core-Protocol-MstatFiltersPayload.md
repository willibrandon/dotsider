---
title: "MstatFiltersPayload"
description: "Filters applied to an mstat contributor query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatfilterspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Filters applied to an mstat contributor query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatFiltersPayload : IEquatable<MstatFiltersPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatFiltersPayload**

## Implements

- [IEquatable\<MstatFiltersPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatFiltersPayload(string?, string?, string?, string?)

Filters applied to an mstat contributor query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Query` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Section` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public MstatFiltersPayload(string? Query, string? Section, string? AssemblyName, string? Namespace)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; init; }
```

### Namespace

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Namespace { get; init; }
```

### Query

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Query { get; init; }
```

### Section

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Section { get; init; }
```

## Methods

### Deconstruct(out string?, out string?, out string?, out string?)

**Parameters:**

- `Query` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Section` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string? Query, out string? Section, out string? AssemblyName, out string? Namespace)
```

### Equals(MstatFiltersPayload?)

**Parameters:**

- `other` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatFiltersPayload? other)
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

### operator !=(MstatFiltersPayload?, MstatFiltersPayload?)

**Parameters:**

- `left` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))
- `right` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatFiltersPayload? left, MstatFiltersPayload? right)
```

### operator ==(MstatFiltersPayload?, MstatFiltersPayload?)

**Parameters:**

- `left` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))
- `right` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatFiltersPayload? left, MstatFiltersPayload? right)
```
