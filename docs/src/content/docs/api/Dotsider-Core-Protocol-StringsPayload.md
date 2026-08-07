---
title: "StringsPayload"
description: "All string categories extracted from a binary. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.stringspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

All string categories extracted from a binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record StringsPayload : IEquatable<StringsPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **StringsPayload**

## Implements

- [IEquatable\<StringsPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### StringsPayload(IReadOnlyList\<StringEntry\>, IReadOnlyList\<StringEntry\>, IReadOnlyList\<StringEntry\>, IReadOnlyList\<StringEntry\>, IReadOnlyList\<StringEntry\>)

All string categories extracted from a binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `UserStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MetadataStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RawStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RawUtf16Strings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `FrozenStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public StringsPayload(IReadOnlyList<StringEntry> UserStrings, IReadOnlyList<StringEntry> MetadataStrings, IReadOnlyList<StringEntry> RawStrings, IReadOnlyList<StringEntry> RawUtf16Strings, IReadOnlyList<StringEntry> FrozenStrings)
```

## Properties

### FrozenStrings

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> FrozenStrings { get; init; }
```

### MetadataStrings

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> MetadataStrings { get; init; }
```

### RawStrings

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> RawStrings { get; init; }
```

### RawUtf16Strings

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> RawUtf16Strings { get; init; }
```

### UserStrings

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> UserStrings { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<StringEntry\>, out IReadOnlyList\<StringEntry\>, out IReadOnlyList\<StringEntry\>, out IReadOnlyList\<StringEntry\>, out IReadOnlyList\<StringEntry\>)

**Parameters:**

- `UserStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MetadataStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RawStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RawUtf16Strings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `FrozenStrings` ([IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<StringEntry> UserStrings, out IReadOnlyList<StringEntry> MetadataStrings, out IReadOnlyList<StringEntry> RawStrings, out IReadOnlyList<StringEntry> RawUtf16Strings, out IReadOnlyList<StringEntry> FrozenStrings)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(StringsPayload?)

**Parameters:**

- `other` ([StringsPayload](/api/dotsider.core.protocol.stringspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(StringsPayload? other)
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

### operator !=(StringsPayload?, StringsPayload?)

**Parameters:**

- `left` ([StringsPayload](/api/dotsider.core.protocol.stringspayload/))
- `right` ([StringsPayload](/api/dotsider.core.protocol.stringspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(StringsPayload? left, StringsPayload? right)
```

### operator ==(StringsPayload?, StringsPayload?)

**Parameters:**

- `left` ([StringsPayload](/api/dotsider.core.protocol.stringspayload/))
- `right` ([StringsPayload](/api/dotsider.core.protocol.stringspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(StringsPayload? left, StringsPayload? right)
```
