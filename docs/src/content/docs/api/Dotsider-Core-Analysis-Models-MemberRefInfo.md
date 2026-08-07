---
title: "MemberRefInfo"
description: "Information about a referenced member (method or field) from the MemberRef metadata table."
slug: api/dotsider.core.analysis.models.memberrefinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a referenced member (method or field) from the MemberRef metadata table.

```csharp
public sealed record MemberRefInfo : IEquatable<MemberRefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MemberRefInfo**

## Implements

- [IEquatable\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MemberRefInfo(int, string, string, string, MemberRefKind)

Information about a referenced member (method or field) from the MemberRef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this member reference.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the type that declares this member.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The name of the referenced member.
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded signature of the member.
- `Kind` ([MemberRefKind](/api/dotsider.core.analysis.models.memberrefkind/)): Whether this member reference is a method or a field.

```csharp
public MemberRefInfo(int Token, string DeclaringType, string Name, string Signature, MemberRefKind Kind)
```

## Properties

### DeclaringType

The fully qualified name of the type that declares this member.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### Kind

Whether this member reference is a method or a field.

**Returns:** [MemberRefKind](/api/dotsider.core.analysis.models.memberrefkind/)

```csharp
public MemberRefKind Kind { get; init; }
```

### Name

The name of the referenced member.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Signature

The decoded signature of the member.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Signature { get; init; }
```

### Token

The metadata token for this member reference.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

## Methods

### Deconstruct(out int, out string, out string, out string, out MemberRefKind)

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Kind` ([MemberRefKind](/api/dotsider.core.analysis.models.memberrefkind/))

```csharp
public void Deconstruct(out int Token, out string DeclaringType, out string Name, out string Signature, out MemberRefKind Kind)
```

### Equals(MemberRefInfo?)

**Parameters:**

- `other` ([MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MemberRefInfo? other)
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

### operator !=(MemberRefInfo?, MemberRefInfo?)

**Parameters:**

- `left` ([MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/))
- `right` ([MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MemberRefInfo? left, MemberRefInfo? right)
```

### operator ==(MemberRefInfo?, MemberRefInfo?)

**Parameters:**

- `left` ([MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/))
- `right` ([MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MemberRefInfo? left, MemberRefInfo? right)
```
