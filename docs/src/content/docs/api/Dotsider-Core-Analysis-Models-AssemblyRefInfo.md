---
title: "AssemblyRefInfo"
description: "Information about a referenced assembly from the AssemblyRef metadata table."
slug: api/dotsider.core.analysis.models.assemblyrefinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a referenced assembly from the AssemblyRef metadata table.

```csharp
public sealed record AssemblyRefInfo : IEquatable<AssemblyRefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyRefInfo**

## Implements

- [IEquatable\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AssemblyRefInfo(string, string, string, string?)

Information about a referenced assembly from the AssemblyRef metadata table.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the referenced assembly.
- `Version` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The version of the referenced assembly.
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The culture of the referenced assembly, or empty for culture-neutral.
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The public key token as a hex string, or null if not strong-named.

```csharp
public AssemblyRefInfo(string Name, string Version, string Culture, string? PublicKeyToken)
```

## Properties

### Culture

The culture of the referenced assembly, or empty for culture-neutral.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Culture { get; init; }
```

### Name

The simple name of the referenced assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### PublicKeyToken

The public key token as a hex string, or null if not strong-named.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Version

The version of the referenced assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Version { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out string?)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Version` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Name, out string Version, out string Culture, out string? PublicKeyToken)
```

### Equals(AssemblyRefInfo?)

**Parameters:**

- `other` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyRefInfo? other)
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

### operator !=(AssemblyRefInfo?, AssemblyRefInfo?)

**Parameters:**

- `left` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/))
- `right` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyRefInfo? left, AssemblyRefInfo? right)
```

### operator ==(AssemblyRefInfo?, AssemblyRefInfo?)

**Parameters:**

- `left` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/))
- `right` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyRefInfo? left, AssemblyRefInfo? right)
```
