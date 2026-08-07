---
title: "CustomAttributeInfo"
description: "Information about a custom attribute applied to a metadata entity."
slug: api/dotsider.core.analysis.models.customattributeinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a custom attribute applied to a metadata entity.

```csharp
public sealed record CustomAttributeInfo : IEquatable<CustomAttributeInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CustomAttributeInfo**

## Implements

- [IEquatable\<CustomAttributeInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CustomAttributeInfo(string, string, string?)

Information about a custom attribute applied to a metadata entity.

**Parameters:**

- `Parent` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A description of the entity the attribute is applied to.
- `Constructor` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the attribute constructor method.
- `Value` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded attribute value as a display string, or null if decoding failed.

```csharp
public CustomAttributeInfo(string Parent, string Constructor, string? Value)
```

## Properties

### Constructor

The fully qualified name of the attribute constructor method.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Constructor { get; init; }
```

### Parent

A description of the entity the attribute is applied to.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Parent { get; init; }
```

### Value

The decoded attribute value as a display string, or null if decoding failed.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Value { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string?)

**Parameters:**

- `Parent` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Constructor` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Value` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Parent, out string Constructor, out string? Value)
```

### Equals(CustomAttributeInfo?)

**Parameters:**

- `other` ([CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CustomAttributeInfo? other)
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

### operator !=(CustomAttributeInfo?, CustomAttributeInfo?)

**Parameters:**

- `left` ([CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/))
- `right` ([CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CustomAttributeInfo? left, CustomAttributeInfo? right)
```

### operator ==(CustomAttributeInfo?, CustomAttributeInfo?)

**Parameters:**

- `left` ([CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/))
- `right` ([CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CustomAttributeInfo? left, CustomAttributeInfo? right)
```
