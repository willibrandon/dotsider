---
title: "ResolvedAssembly.FromFile"
description: "The assembly was found as a file on disk."
slug: api/dotsider.core.analysis.models.resolvedassembly.fromfile
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The assembly was found as a file on disk.

```csharp
public sealed record ResolvedAssembly.FromFile : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromFile>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/) → **ResolvedAssembly.FromFile**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<FromFile\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### FromFile(string)

The assembly was found as a file on disk.

**Parameters:**

- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the assembly file.

```csharp
public FromFile(string Path)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### Path

Full path to the assembly file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Path { get; init; }
```

## Methods

### Deconstruct(out string)

**Parameters:**

- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Path)
```

### Equals(FromFile?)

**Parameters:**

- `other` ([FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ResolvedAssembly.FromFile? other)
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

### operator !=(FromFile?, FromFile?)

**Parameters:**

- `left` ([FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/))
- `right` ([FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ResolvedAssembly.FromFile? left, ResolvedAssembly.FromFile? right)
```

### operator ==(FromFile?, FromFile?)

**Parameters:**

- `left` ([FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/))
- `right` ([FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ResolvedAssembly.FromFile? left, ResolvedAssembly.FromFile? right)
```
