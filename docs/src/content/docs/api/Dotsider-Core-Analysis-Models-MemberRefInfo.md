---
title: "MemberRefInfo"
description: "Information about a referenced member (method or field) from the MemberRef metadata table."
slug: api/dotsider.core.analysis.models.memberrefinfo
sidebar:
  order: 1
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

### MemberRefInfo(int, string, string, string)

Information about a referenced member (method or field) from the MemberRef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this member reference.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name of the type that declares this member.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The name of the referenced member.
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded signature of the member.

```csharp
public MemberRefInfo(int Token, string DeclaringType, string Name, string Signature)
```

## Properties

### DeclaringType

The fully qualified name of the type that declares this member.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
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

