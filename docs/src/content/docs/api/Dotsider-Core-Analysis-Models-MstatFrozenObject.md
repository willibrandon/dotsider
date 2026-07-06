---
title: "MstatFrozenObject"
description: "One frozen object from an ILC size report (format 2.1+) — an object allocated at compile time and baked into the image, most commonly a string literal. For back-compat these bytes are also summed into the ArrayOfFrozenObjects blob entry."
slug: api/dotsider.core.analysis.models.mstatfrozenobject
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One frozen object from an ILC size report (format 2.1+) — an object allocated at compile
time and baked into the image, most commonly a string literal. For back-compat these bytes
are also summed into the `ArrayOfFrozenObjects` blob entry.

```csharp
public sealed record MstatFrozenObject : IEquatable<MstatFrozenObject>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatFrozenObject**

## Implements

- [IEquatable\<MstatFrozenObject\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatFrozenObject(string, string, int, string?, string?, string?, string?)

One frozen object from an ILC size report (format 2.1+) — an object allocated at compile
time and baked into the image, most commonly a string literal. For back-compat these bytes
are also summed into the `ArrayOfFrozenObjects` blob entry.

**Parameters:**

- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The frozen object's type display name (for example `System.String`).
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly that defines the object's type.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The object size in bytes, including its object header.
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name; joins to the DGML node `Label`.
- `OwningType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The type whose static data serialized this object, or null when the object is not a
serialized static (string literals report null).
- `OwningAssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly defining [OwningType](/api/dotsider.core.analysis.models.mstatfrozenobject.owningtype/), or null when the object
has no owner. This — not [AssemblyName](/api/dotsider.core.analysis.models.mstatfrozenobject.assemblyname/) — is the assembly whose code caused the
bytes: a frozen string's [AssemblyName](/api/dotsider.core.analysis.models.mstatfrozenobject.assemblyname/) is always the core library.
- `OwningNamespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace of [OwningType](/api/dotsider.core.analysis.models.mstatfrozenobject.owningtype/), or null when the object has no owner.

```csharp
public MstatFrozenObject(string TypeName, string AssemblyName, int Size, string? NodeName, string? OwningType, string? OwningAssemblyName, string? OwningNamespace)
```

### MstatFrozenObject(string, string, int, string?, string?)

The pre-owner-attribution shape (five arguments), preserved so existing construction
sites keep compiling. The owner attribution fields default to null.

**Parameters:**

- `typeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The frozen object's type display name.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly defining the object's type.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The object size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name.
- `owningType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning type's display name, or null.

```csharp
public MstatFrozenObject(string typeName, string assemblyName, int size, string? nodeName, string? owningType)
```

## Properties

### AssemblyName

The simple name of the assembly that defines the object's type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### NodeName

The compiler's dependency-graph node name; joins to the DGML node `Label`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NodeName { get; init; }
```

### OwningAssemblyName

The simple name of the assembly defining [OwningType](/api/dotsider.core.analysis.models.mstatfrozenobject.owningtype/), or null when the object
has no owner. This — not [AssemblyName](/api/dotsider.core.analysis.models.mstatfrozenobject.assemblyname/) — is the assembly whose code caused the
bytes: a frozen string's [AssemblyName](/api/dotsider.core.analysis.models.mstatfrozenobject.assemblyname/) is always the core library.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwningAssemblyName { get; init; }
```

### OwningNamespace

The namespace of [OwningType](/api/dotsider.core.analysis.models.mstatfrozenobject.owningtype/), or null when the object has no owner.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwningNamespace { get; init; }
```

### OwningType

The type whose static data serialized this object, or null when the object is not a
serialized static (string literals report null).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwningType { get; init; }
```

### Size

The object size in bytes, including its object header.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

### TypeName

The frozen object's type display name (for example `System.String`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TypeName { get; init; }
```

## Methods

### Deconstruct(out string, out string, out int, out string?, out string?)

The pre-owner-attribution five-output deconstruction, preserved alongside the generated seven-output one.

**Parameters:**

- `typeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The frozen object's type display name.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly defining the object's type.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The object size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name.
- `owningType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning type's display name, or null.

```csharp
public void Deconstruct(out string typeName, out string assemblyName, out int size, out string? nodeName, out string? owningType)
```

