---
title: "MstatMethod"
description: "One compiled method body from an ILC size report. Sizes are bytes of native artifact, not IL: ILC compiles each body once, so the sum over all methods is the code contribution to the binary."
slug: api/dotsider.core.analysis.models.mstatmethod
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One compiled method body from an ILC size report. Sizes are bytes of native artifact, not
IL: ILC compiles each body once, so the sum over all methods is the code contribution to
the binary.

```csharp
public sealed record MstatMethod : IEquatable<MstatMethod>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatMethod**

## Implements

- [IEquatable\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatMethod(string, string, string, string, int, int, int, string?, string)

One compiled method body from an ILC size report. Sizes are bytes of native artifact, not
IL: ILC compiles each body once, so the sum over all methods is the code contribution to
the binary.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method name, with generic arguments rendered when instantiated.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's display name, including namespace.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's namespace, or an empty string for the global namespace.
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly the method was compiled from.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The native code size in bytes.
- `GcInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The GC info size in bytes.
- `EhInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The exception-handling info size in bytes, or 0 when the method has none.
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports. The same
string appears as the node `Label` in the DGML graphs `IlcGenerateDgmlFile` emits,
which is how a size entry joins to its dependency chain.
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The rendered parameter-type list of the method's definition (for example
`(string, int)`), or an empty string when the signature could not be decoded.
Overloads share a [Name](/api/dotsider.core.analysis.models.mstatmethod.name/) but never a signature, so
([AssemblyName](/api/dotsider.core.analysis.models.mstatmethod.assemblyname/), [DeclaringType](/api/dotsider.core.analysis.models.mstatmethod.declaringtype/), [Name](/api/dotsider.core.analysis.models.mstatmethod.name/),
[Signature](/api/dotsider.core.analysis.models.mstatmethod.signature/)) identifies a method stably across builds.

```csharp
public MstatMethod(string Name, string DeclaringType, string Namespace, string AssemblyName, int Size, int GcInfoSize, int EhInfoSize, string? NodeName, string Signature)
```

### MstatMethod(string, string, string, string, int, int, int, string?)

The pre-signature shape (eight arguments), preserved so existing construction sites keep
compiling. [Signature](/api/dotsider.core.analysis.models.mstatmethod.signature/) defaults to an empty string.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method name.
- `declaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's display name.
- `namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's namespace.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the defining assembly.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The native code size in bytes.
- `gcInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The GC info size in bytes.
- `ehInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The EH info size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name, or null.

```csharp
public MstatMethod(string name, string declaringType, string @namespace, string assemblyName, int size, int gcInfoSize, int ehInfoSize, string? nodeName)
```

## Properties

### AssemblyName

The simple name of the assembly the method was compiled from.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### DeclaringType

The declaring type's display name, including namespace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### EhInfoSize

The exception-handling info size in bytes, or 0 when the method has none.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EhInfoSize { get; init; }
```

### GcInfoSize

The GC info size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int GcInfoSize { get; init; }
```

### Name

The method name, with generic arguments rendered when instantiated.

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

The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports. The same
string appears as the node `Label` in the DGML graphs `IlcGenerateDgmlFile` emits,
which is how a size entry joins to its dependency chain.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NodeName { get; init; }
```

### Signature

The rendered parameter-type list of the method's definition (for example
`(string, int)`), or an empty string when the signature could not be decoded.
Overloads share a [Name](/api/dotsider.core.analysis.models.mstatmethod.name/) but never a signature, so
([AssemblyName](/api/dotsider.core.analysis.models.mstatmethod.assemblyname/), [DeclaringType](/api/dotsider.core.analysis.models.mstatmethod.declaringtype/), [Name](/api/dotsider.core.analysis.models.mstatmethod.name/),
[Signature](/api/dotsider.core.analysis.models.mstatmethod.signature/)) identifies a method stably across builds.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Signature { get; init; }
```

### Size

The native code size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out string, out int, out int, out int, out string?, out string)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `GcInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `EhInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Name, out string DeclaringType, out string Namespace, out string AssemblyName, out int Size, out int GcInfoSize, out int EhInfoSize, out string? NodeName, out string Signature)
```

### Deconstruct(out string, out string, out string, out string, out int, out int, out int, out string?)

The pre-signature eight-output deconstruction, preserved alongside the generated nine-output one.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method name.
- `declaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's display name.
- `namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's namespace.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the defining assembly.
- `size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The native code size in bytes.
- `gcInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The GC info size in bytes.
- `ehInfoSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The EH info size in bytes.
- `nodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's dependency-graph node name, or null.

```csharp
public void Deconstruct(out string name, out string declaringType, out string @namespace, out string assemblyName, out int size, out int gcInfoSize, out int ehInfoSize, out string? nodeName)
```

### Equals(MstatMethod?)

**Parameters:**

- `other` ([MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatMethod? other)
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

### operator !=(MstatMethod?, MstatMethod?)

**Parameters:**

- `left` ([MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/))
- `right` ([MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatMethod? left, MstatMethod? right)
```

### operator ==(MstatMethod?, MstatMethod?)

**Parameters:**

- `left` ([MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/))
- `right` ([MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatMethod? left, MstatMethod? right)
```
