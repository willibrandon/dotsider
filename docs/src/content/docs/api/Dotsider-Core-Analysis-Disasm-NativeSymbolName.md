---
title: "NativeSymbolName"
description: "Splits a recovered managed name (as joined by the native symbol reader, e.g. System.Text.StringBuilder.Append(char)) into its namespace, declaring type, and member, so the native IL-inspector tree can bucket functions the same namespace → type → method way the managed tree does. The parse is signature-aware (it ignores the parameter list) and handles nested types (+) and generic arity markers."
slug: api/dotsider.core.analysis.disasm.nativesymbolname
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Disasm`

**Assembly:** Dotsider.Core.dll

Splits a recovered managed name (as joined by the native symbol reader, e.g.
`System.Text.StringBuilder.Append(char)`) into its namespace, declaring type, and member,
so the native IL-inspector tree can bucket functions the same namespace → type → method way the
managed tree does. The parse is signature-aware (it ignores the parameter list) and handles
nested types (`+`) and generic arity markers.

```csharp
public readonly struct NativeSymbolName : IEquatable<NativeSymbolName>
```

## Implements

- [IEquatable\<NativeSymbolName\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolName(string, string, string)

Splits a recovered managed name (as joined by the native symbol reader, e.g.
`System.Text.StringBuilder.Append(char)`) into its namespace, declaring type, and member,
so the native IL-inspector tree can bucket functions the same namespace → type → method way the
managed tree does. The parse is signature-aware (it ignores the parameter list) and handles
nested types (`+`) and generic arity markers.

**Parameters:**

- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace, or empty for the global namespace.
- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type (with any nested-type chain), or empty when absent.
- `MemberName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The member name with its signature, or the whole name when it has no type qualifier.

```csharp
public NativeSymbolName(string Namespace, string TypeName, string MemberName)
```

## Properties

### MemberName

The member name with its signature, or the whole name when it has no type qualifier.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string MemberName { get; init; }
```

### Namespace

The namespace, or empty for the global namespace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### TypeName

The declaring type (with any nested-type chain), or empty when absent.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TypeName { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string)

**Parameters:**

- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MemberName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Namespace, out string TypeName, out string MemberName)
```

### Equals(NativeSymbolName)

**Parameters:**

- `other` ([NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolName other)
```

### Equals(object)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### Parse(string)

Parses a managed name into namespace, type, and member.

**Parameters:**

- `managedName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The joined managed name.

**Returns:** [NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/)

```csharp
public static NativeSymbolName Parse(string managedName)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(NativeSymbolName, NativeSymbolName)

**Parameters:**

- `left` ([NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/))
- `right` ([NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolName left, NativeSymbolName right)
```

### operator ==(NativeSymbolName, NativeSymbolName)

**Parameters:**

- `left` ([NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/))
- `right` ([NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolName left, NativeSymbolName right)
```
