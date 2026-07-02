---
title: "MstatRvaField"
description: "One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a field mapped directly into the image, typically compiler-generated arrays behind collection expressions and ReadOnlySpan literals. For back-compat these bytes are also summed into the FieldRvaData blob entry."
slug: api/dotsider.core.analysis.models.mstatrvafield
sidebar:
  order: 1
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

### MstatRvaField(string, string, int, string?)

One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
field mapped directly into the image, typically compiler-generated arrays behind
collection expressions and `ReadOnlySpan` literals. For back-compat these bytes are
also summed into the `FieldRvaData` blob entry.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The field's display name, including its declaring type.
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly that defines the field.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA data size in bytes.
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name; joins to the DGML node `Label`.

```csharp
public MstatRvaField(string Name, string AssemblyName, int Size, string? NodeName)
```

## Properties

### AssemblyName

The simple name of the assembly that defines the field.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Name

The field's display name, including its declaring type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
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

