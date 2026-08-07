---
title: "NativeAotSectionsPayload"
description: "A Native AOT module-section inventory. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativeaotsectionspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A Native AOT module-section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotSectionsPayload : IEquatable<NativeAotSectionsPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotSectionsPayload**

## Implements

- [IEquatable\<NativeAotSectionsPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAotSectionsPayload(string, int, IReadOnlyList\<NativeAotSectionPayload\>)

A Native AOT module-section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Sections` ([IReadOnlyList\<NativeAotSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public NativeAotSectionsPayload(string FilePath, int SectionCount, IReadOnlyList<NativeAotSectionPayload> Sections)
```

## Properties

### FilePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; init; }
```

### SectionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

### Sections

**Returns:** [IReadOnlyList\<NativeAotSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeAotSectionPayload> Sections { get; init; }
```

## Methods

### Deconstruct(out string, out int, out IReadOnlyList\<NativeAotSectionPayload\>)

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Sections` ([IReadOnlyList\<NativeAotSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string FilePath, out int SectionCount, out IReadOnlyList<NativeAotSectionPayload> Sections)
```

### Equals(NativeAotSectionsPayload?)

**Parameters:**

- `other` ([NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeAotSectionsPayload? other)
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

### operator !=(NativeAotSectionsPayload?, NativeAotSectionsPayload?)

**Parameters:**

- `left` ([NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/))
- `right` ([NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeAotSectionsPayload? left, NativeAotSectionsPayload? right)
```

### operator ==(NativeAotSectionsPayload?, NativeAotSectionsPayload?)

**Parameters:**

- `left` ([NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/))
- `right` ([NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeAotSectionsPayload? left, NativeAotSectionsPayload? right)
```
