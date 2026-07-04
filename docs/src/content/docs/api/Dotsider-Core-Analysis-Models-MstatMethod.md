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

### MstatMethod(string, string, string, string, int, int, int, string?)

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

```csharp
public MstatMethod(string Name, string DeclaringType, string Namespace, string AssemblyName, int Size, int GcInfoSize, int EhInfoSize, string? NodeName)
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

### Size

The native code size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

