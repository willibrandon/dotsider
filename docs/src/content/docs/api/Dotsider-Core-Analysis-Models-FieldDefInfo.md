---
title: "FieldDefInfo"
description: "Information about a field defined in the assembly's FieldDef metadata table."
slug: api/dotsider.core.analysis.models.fielddefinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a field defined in the assembly's FieldDef metadata table.

```csharp
public sealed record FieldDefInfo : IEquatable<FieldDefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **FieldDefInfo**

## Implements

- [IEquatable\<FieldDefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FieldDefInfo(int, string, string, FieldAttributes, string)

Information about a field defined in the assembly's FieldDef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this field definition.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the type that declares this field.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The name of the field.
- `Attributes` ([FieldAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.fieldattributes)): The field attribute flags (access, static, literal, etc.).
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded field type signature string.

```csharp
public FieldDefInfo(int Token, string DeclaringType, string Name, FieldAttributes Attributes, string Signature)
```

## Properties

### Attributes

The field attribute flags (access, static, literal, etc.).

**Returns:** [FieldAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.fieldattributes)

```csharp
public FieldAttributes Attributes { get; init; }
```

### DeclaringType

The fully qualified name of the type that declares this field.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### Name

The name of the field.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Signature

The decoded field type signature string.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Signature { get; init; }
```

### Token

The metadata token for this field definition.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

