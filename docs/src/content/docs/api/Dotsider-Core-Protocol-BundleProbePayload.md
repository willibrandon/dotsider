---
title: "BundleProbePayload"
description: "A single-file bundle probe result. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.bundleprobepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A single-file bundle probe result.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleProbePayload : IEquatable<BundleProbePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BundleProbePayload**

## Implements

- [IEquatable\<BundleProbePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleProbePayload(bool, long)

A single-file bundle probe result.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `IsBundle` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HeaderOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public BundleProbePayload(bool IsBundle, long HeaderOffset)
```

## Properties

### HeaderOffset

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long HeaderOffset { get; init; }
```

### IsBundle

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsBundle { get; init; }
```

## Methods

### Deconstruct(out bool, out long)

**Parameters:**

- `IsBundle` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HeaderOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out bool IsBundle, out long HeaderOffset)
```

### Equals(BundleProbePayload?)

**Parameters:**

- `other` ([BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(BundleProbePayload? other)
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

### operator !=(BundleProbePayload?, BundleProbePayload?)

**Parameters:**

- `left` ([BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/))
- `right` ([BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(BundleProbePayload? left, BundleProbePayload? right)
```

### operator ==(BundleProbePayload?, BundleProbePayload?)

**Parameters:**

- `left` ([BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/))
- `right` ([BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(BundleProbePayload? left, BundleProbePayload? right)
```
