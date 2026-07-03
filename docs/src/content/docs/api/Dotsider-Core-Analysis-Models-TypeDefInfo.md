---
title: "TypeDefInfo"
description: "Information about a type defined in the assembly's TypeDef metadata table."
slug: api/dotsider.core.analysis.models.typedefinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a type defined in the assembly's TypeDef metadata table.

```csharp
public sealed record TypeDefInfo : IEquatable<TypeDefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TypeDefInfo**

## Implements

- [IEquatable\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TypeDefInfo(int, string, string, string, TypeAttributes, string?, int, int)

Information about a type defined in the assembly's TypeDef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this type definition.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace of the type, or empty string for global types.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the type.
- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name (Namespace.Name).
- `Attributes` ([TypeAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.typeattributes)): The type attribute flags (visibility, layout, semantics).
- `BaseType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the base type, or null for interfaces/System.Object.
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of methods defined on this type.
- `FieldCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of fields defined on this type.

```csharp
public TypeDefInfo(int Token, string Namespace, string Name, string FullName, TypeAttributes Attributes, string? BaseType, int MethodCount, int FieldCount)
```

## Properties

### Attributes

The type attribute flags (visibility, layout, semantics).

**Returns:** [TypeAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.typeattributes)

```csharp
public TypeAttributes Attributes { get; init; }
```

### BaseType

The fully qualified name of the base type, or null for interfaces/System.Object.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BaseType { get; init; }
```

### FieldCount

Number of fields defined on this type.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FieldCount { get; init; }
```

### FullName

The fully qualified name (Namespace.Name).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullName { get; init; }
```

### MethodCount

Number of methods defined on this type.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodCount { get; init; }
```

### Name

The simple name of the type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

The namespace of the type, or empty string for global types.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### Token

The metadata token for this type definition.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

