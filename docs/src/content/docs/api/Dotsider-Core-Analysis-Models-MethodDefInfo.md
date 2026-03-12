---
title: "MethodDefInfo"
description: "Information about a method defined in the assembly's MethodDef metadata table."
slug: api/dotsider.core.analysis.models.methoddefinfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a method defined in the assembly's MethodDef metadata table.

```csharp
public sealed record MethodDefInfo : IEquatable<MethodDefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MethodDefInfo**

## Implements

- [IEquatable\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MethodDefInfo(int, string, string, string, MethodAttributes, MethodImplAttributes, int)

Information about a method defined in the assembly's MethodDef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this method definition.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the type that declares this method.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the method.
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded method signature string (e.g., "void(int, string)").
- `Attributes` ([MethodAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.methodattributes)): The method attribute flags (access, vtable layout, implementation).
- `ImplAttributes` ([MethodImplAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.methodimplattributes)): The method implementation attribute flags (IL, native, runtime).
- `Rva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The relative virtual address of the method body, or zero for abstract/extern methods.

```csharp
public MethodDefInfo(int Token, string DeclaringType, string Name, string Signature, MethodAttributes Attributes, MethodImplAttributes ImplAttributes, int Rva)
```

## Properties

### Attributes

The method attribute flags (access, vtable layout, implementation).

**Returns:** [MethodAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.methodattributes)

```csharp
public MethodAttributes Attributes { get; init; }
```

### DeclaringType

The fully qualified name of the type that declares this method.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### ImplAttributes

The method implementation attribute flags (IL, native, runtime).

**Returns:** [MethodImplAttributes](https://learn.microsoft.com/dotnet/api/system.reflection.methodimplattributes)

```csharp
public MethodImplAttributes ImplAttributes { get; init; }
```

### Name

The simple name of the method.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Rva

The relative virtual address of the method body, or zero for abstract/extern methods.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Rva { get; init; }
```

### Signature

The decoded method signature string (e.g., "void(int, string)").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Signature { get; init; }
```

### Token

The metadata token for this method definition.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

