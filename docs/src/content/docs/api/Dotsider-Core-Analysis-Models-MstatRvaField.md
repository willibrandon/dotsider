---
title: "MstatRvaField"
description: "One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a field mapped directly into the image, typically compiler-generated arrays behind collection expressions and ReadOnlySpan literals. For back-compat these bytes are also summed into the FieldRvaData blob entry."
slug: api/dotsider.core.analysis.models.mstatrvafield
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
field mapped directly into the image, typically compiler-generated arrays behind
collection expressions and `ReadOnlySpan` literals. For back-compat these bytes are
also summed into the `FieldRvaData` blob entry.

```csharp
public sealed record MstatRvaField : IEquatable<MstatRvaField>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatRvaField**

## Implements

- [IEquatable\<MstatRvaField\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatRvaField(string, string, int, string?, string)

One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
field mapped directly into the image, typically compiler-generated arrays behind
collection expressions and `ReadOnlySpan` literals. For back-compat these bytes are
also summed into the `FieldRvaData` blob entry.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The field's display name, including its declaring type (`Type::Field`).
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly that defines the field.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA data size in bytes.
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name; joins to the DGML node `Label`.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's namespace, or an empty string for the global namespace.

```csharp
public MstatRvaField(string Name, string AssemblyName, int Size, string? NodeName, string Namespace)
```

### MstatRvaField(string, string, int, string?)

The pre-namespace shape (four arguments), preserved so existing construction sites keep
compiling. [Namespace](/api/dotsider.core.analysis.models.mstatrvafield.namespace/) defaults to an empty string.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The field's display name, including its declaring type.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the defining assembly.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA data size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name.

```csharp
public MstatRvaField(string name, string assemblyName, int size, string? nodeName)
```

## Properties

### AssemblyName

The simple name of the assembly that defines the field.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Name

The field's display name, including its declaring type (`Type::Field`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

The declaring type's namespace, or an empty string for the global namespace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### NodeName

The compiler's dependency-graph node name; joins to the DGML node `Label`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NodeName { get; init; }
```

### Size

The RVA data size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

## Methods

### Deconstruct(out string, out string, out int, out string?, out string)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Name, out string AssemblyName, out int Size, out string? NodeName, out string Namespace)
```

### Deconstruct(out string, out string, out int, out string?)

The pre-namespace four-output deconstruction, preserved alongside the generated five-output one.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The field's display name, including its declaring type.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the defining assembly.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA data size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name.

```csharp
public void Deconstruct(out string name, out string assemblyName, out int size, out string? nodeName)
```

### Equals(MstatRvaField?)

**Parameters:**

- `other` ([MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatRvaField? other)
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

### operator !=(MstatRvaField?, MstatRvaField?)

**Parameters:**

- `left` ([MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/))
- `right` ([MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatRvaField? left, MstatRvaField? right)
```

### operator ==(MstatRvaField?, MstatRvaField?)

**Parameters:**

- `left` ([MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/))
- `right` ([MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatRvaField? left, MstatRvaField? right)
```
