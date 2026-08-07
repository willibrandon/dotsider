---
title: "MetadataMemberSearchPayload"
description: "Metadata-backed member-search results. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.metadatamembersearchpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Metadata-backed member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MetadataMemberSearchPayload : IEquatable<MetadataMemberSearchPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MetadataMemberSearchPayload**

## Implements

- [IEquatable\<MetadataMemberSearchPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MetadataMemberSearchPayload(IReadOnlyList\<TypeDefInfo\>, IReadOnlyList\<MethodDefInfo\>, IReadOnlyList\<MemberRefInfo\>)

Metadata-backed member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Types` ([IReadOnlyList\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Methods` ([IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MemberRefs` ([IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MetadataMemberSearchPayload(IReadOnlyList<TypeDefInfo> Types, IReadOnlyList<MethodDefInfo> Methods, IReadOnlyList<MemberRefInfo> MemberRefs)
```

## Properties

### MemberRefs

**Returns:** [IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MemberRefInfo> MemberRefs { get; init; }
```

### Methods

**Returns:** [IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MethodDefInfo> Methods { get; init; }
```

### Types

**Returns:** [IReadOnlyList\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TypeDefInfo> Types { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<TypeDefInfo\>, out IReadOnlyList\<MethodDefInfo\>, out IReadOnlyList\<MemberRefInfo\>)

**Parameters:**

- `Types` ([IReadOnlyList\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Methods` ([IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MemberRefs` ([IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<TypeDefInfo> Types, out IReadOnlyList<MethodDefInfo> Methods, out IReadOnlyList<MemberRefInfo> MemberRefs)
```

### Equals(MetadataMemberSearchPayload?)

**Parameters:**

- `other` ([MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MetadataMemberSearchPayload? other)
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

### operator !=(MetadataMemberSearchPayload?, MetadataMemberSearchPayload?)

**Parameters:**

- `left` ([MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/))
- `right` ([MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MetadataMemberSearchPayload? left, MetadataMemberSearchPayload? right)
```

### operator ==(MetadataMemberSearchPayload?, MetadataMemberSearchPayload?)

**Parameters:**

- `left` ([MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/))
- `right` ([MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MetadataMemberSearchPayload? left, MetadataMemberSearchPayload? right)
```
