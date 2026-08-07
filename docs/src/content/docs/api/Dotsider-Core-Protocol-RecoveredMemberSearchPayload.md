---
title: "RecoveredMemberSearchPayload"
description: "Recovered Native AOT member-search results. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.recoveredmembersearchpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Recovered Native AOT member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredMemberSearchPayload : IEquatable<RecoveredMemberSearchPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RecoveredMemberSearchPayload**

## Implements

- [IEquatable\<RecoveredMemberSearchPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### RecoveredMemberSearchPayload(IReadOnlyList\<RecoveredTypePayload\>, IReadOnlyList\<RecoveredMethodPayload\>, IReadOnlyList\<MemberRefInfo\>)

Recovered Native AOT member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Types` ([IReadOnlyList\<RecoveredTypePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Methods` ([IReadOnlyList\<RecoveredMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MemberRefs` ([IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public RecoveredMemberSearchPayload(IReadOnlyList<RecoveredTypePayload> Types, IReadOnlyList<RecoveredMethodPayload> Methods, IReadOnlyList<MemberRefInfo> MemberRefs)
```

## Properties

### MemberRefs

**Returns:** [IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MemberRefInfo> MemberRefs { get; init; }
```

### Methods

**Returns:** [IReadOnlyList\<RecoveredMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<RecoveredMethodPayload> Methods { get; init; }
```

### Types

**Returns:** [IReadOnlyList\<RecoveredTypePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<RecoveredTypePayload> Types { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<RecoveredTypePayload\>, out IReadOnlyList\<RecoveredMethodPayload\>, out IReadOnlyList\<MemberRefInfo\>)

**Parameters:**

- `Types` ([IReadOnlyList\<RecoveredTypePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Methods` ([IReadOnlyList\<RecoveredMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MemberRefs` ([IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<RecoveredTypePayload> Types, out IReadOnlyList<RecoveredMethodPayload> Methods, out IReadOnlyList<MemberRefInfo> MemberRefs)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(RecoveredMemberSearchPayload?)

**Parameters:**

- `other` ([RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(RecoveredMemberSearchPayload? other)
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

### operator !=(RecoveredMemberSearchPayload?, RecoveredMemberSearchPayload?)

**Parameters:**

- `left` ([RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/))
- `right` ([RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(RecoveredMemberSearchPayload? left, RecoveredMemberSearchPayload? right)
```

### operator ==(RecoveredMemberSearchPayload?, RecoveredMemberSearchPayload?)

**Parameters:**

- `left` ([RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/))
- `right` ([RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(RecoveredMemberSearchPayload? left, RecoveredMemberSearchPayload? right)
```
