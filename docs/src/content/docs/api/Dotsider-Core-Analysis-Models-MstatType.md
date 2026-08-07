---
title: "MstatType"
description: "One constructed type from an ILC size report. The size is the type's MethodTable data — the runtime type structure — not the code of its methods, which is reported per method."
slug: api/dotsider.core.analysis.models.mstattype
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One constructed type from an ILC size report. The size is the type's MethodTable data —
the runtime type structure — not the code of its methods, which is reported per method.

```csharp
public sealed record MstatType : IEquatable<MstatType>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatType**

## Implements

- [IEquatable\<MstatType\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatType(string, string, string, int, string?)

One constructed type from an ILC size report. The size is the type's MethodTable data —
the runtime type structure — not the code of its methods, which is reported per method.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The type's display name, with generic arguments rendered when instantiated.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The type's namespace, or an empty string for the global namespace.
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly that defines the type.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The MethodTable size in bytes.
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports; joins to
the DGML node `Label`.

```csharp
public MstatType(string Name, string Namespace, string AssemblyName, int Size, string? NodeName)
```

## Properties

### AssemblyName

The simple name of the assembly that defines the type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Name

The type's display name, with generic arguments rendered when instantiated.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

The type's namespace, or an empty string for the global namespace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### NodeName

The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports; joins to
the DGML node `Label`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NodeName { get; init; }
```

### Size

The MethodTable size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out int, out string?)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Name, out string Namespace, out string AssemblyName, out int Size, out string? NodeName)
```

### Equals(MstatType?)

**Parameters:**

- `other` ([MstatType](/api/dotsider.core.analysis.models.mstattype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatType? other)
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

### operator !=(MstatType?, MstatType?)

**Parameters:**

- `left` ([MstatType](/api/dotsider.core.analysis.models.mstattype/))
- `right` ([MstatType](/api/dotsider.core.analysis.models.mstattype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatType? left, MstatType? right)
```

### operator ==(MstatType?, MstatType?)

**Parameters:**

- `left` ([MstatType](/api/dotsider.core.analysis.models.mstattype/))
- `right` ([MstatType](/api/dotsider.core.analysis.models.mstattype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatType? left, MstatType? right)
```
