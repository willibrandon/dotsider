---
title: "MstatLargestMethodPayload"
description: "A large method reported by mstat. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatlargestmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A large method reported by mstat.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatLargestMethodPayload : IEquatable<MstatLargestMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatLargestMethodPayload**

## Implements

- [IEquatable\<MstatLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatLargestMethodPayload(string, MstatMethodPayload, long, string, IReadOnlyList\<string\>)

A large method reported by mstat.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Method` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MstatLargestMethodPayload(string Source, MstatMethodPayload Method, long Size, string FullPath, IReadOnlyList<string> NodeNames)
```

## Properties

### FullPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Method

**Returns:** [MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/)

```csharp
public MstatMethodPayload Method { get; init; }
```

### NodeNames

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> NodeNames { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### Source

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Source { get; init; }
```

## Methods

### Deconstruct(out string, out MstatMethodPayload, out long, out string, out IReadOnlyList\<string\>)

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Method` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Source, out MstatMethodPayload Method, out long Size, out string FullPath, out IReadOnlyList<string> NodeNames)
```

### Equals(MstatLargestMethodPayload?)

**Parameters:**

- `other` ([MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatLargestMethodPayload? other)
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

### operator !=(MstatLargestMethodPayload?, MstatLargestMethodPayload?)

**Parameters:**

- `left` ([MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/))
- `right` ([MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatLargestMethodPayload? left, MstatLargestMethodPayload? right)
```

### operator ==(MstatLargestMethodPayload?, MstatLargestMethodPayload?)

**Parameters:**

- `left` ([MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/))
- `right` ([MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatLargestMethodPayload? left, MstatLargestMethodPayload? right)
```
