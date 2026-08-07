---
title: "WasmSectionsPayload"
description: "A WebAssembly section inventory. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.wasmsectionspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A WebAssembly section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSectionsPayload : IEquatable<WasmSectionsPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmSectionsPayload**

## Implements

- [IEquatable\<WasmSectionsPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmSectionsPayload(string, int, IReadOnlyList\<WasmSectionPayload\>)

A WebAssembly section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Sections` ([IReadOnlyList\<WasmSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public WasmSectionsPayload(string FilePath, int SectionCount, IReadOnlyList<WasmSectionPayload> Sections)
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

**Returns:** [IReadOnlyList\<WasmSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmSectionPayload> Sections { get; init; }
```

## Methods

### Deconstruct(out string, out int, out IReadOnlyList\<WasmSectionPayload\>)

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Sections` ([IReadOnlyList\<WasmSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string FilePath, out int SectionCount, out IReadOnlyList<WasmSectionPayload> Sections)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmSectionsPayload?)

**Parameters:**

- `other` ([WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmSectionsPayload? other)
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

### operator !=(WasmSectionsPayload?, WasmSectionsPayload?)

**Parameters:**

- `left` ([WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/))
- `right` ([WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmSectionsPayload? left, WasmSectionsPayload? right)
```

### operator ==(WasmSectionsPayload?, WasmSectionsPayload?)

**Parameters:**

- `left` ([WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/))
- `right` ([WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmSectionsPayload? left, WasmSectionsPayload? right)
```
