---
title: "ResolvedAssembly.FromBundle"
description: "The assembly was found inside a single-file bundle."
slug: api/dotsider.core.analysis.models.resolvedassembly.frombundle
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The assembly was found inside a single-file bundle.

```csharp
public sealed record ResolvedAssembly.FromBundle : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromBundle>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/) → **ResolvedAssembly.FromBundle**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<FromBundle\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FromBundle(byte[], string, string)

The assembly was found inside a single-file bundle.

**Parameters:**

- `Bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): The raw assembly bytes extracted from the bundle.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly file name (e.g. "System.Runtime.dll").
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the bundle file that contains this assembly.

```csharp
public FromBundle(byte[] Bytes, string Name, string BundlePath)
```

## Properties

### BundlePath

Full path to the bundle file that contains this assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string BundlePath { get; init; }
```

### Bytes

The raw assembly bytes extracted from the bundle.

**Returns:** [Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])

```csharp
public byte[] Bytes { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### Name

The assembly file name (e.g. "System.Runtime.dll").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

## Methods

### Deconstruct(out byte[], out string, out string)

**Parameters:**

- `Bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[]))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out byte[] Bytes, out string Name, out string BundlePath)
```

### Equals(FromBundle?)

**Parameters:**

- `other` ([FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ResolvedAssembly.FromBundle? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ResolvedAssembly?)

**Parameters:**

- `other` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(ResolvedAssembly? other)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### PrintMembers(StringBuilder)

**Parameters:**

- `builder` ([StringBuilder](https://learn.microsoft.com/dotnet/api/system.text.stringbuilder))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
protected override bool PrintMembers(StringBuilder builder)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(FromBundle?, FromBundle?)

**Parameters:**

- `left` ([FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/))
- `right` ([FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ResolvedAssembly.FromBundle? left, ResolvedAssembly.FromBundle? right)
```

### operator ==(FromBundle?, FromBundle?)

**Parameters:**

- `left` ([FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/))
- `right` ([FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ResolvedAssembly.FromBundle? left, ResolvedAssembly.FromBundle? right)
```
