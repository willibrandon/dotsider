---
title: "CurrentViewPayload"
description: "The current interactive view of a standard dotsider session. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.currentviewpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

The current interactive view of a standard dotsider session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record CurrentViewPayload : IEquatable<CurrentViewPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CurrentViewPayload**

## Implements

- [IEquatable\<CurrentViewPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CurrentViewPayload(int, string, int, int, string, int, string?, bool, bool, bool, bool)

The current interactive view of a standard dotsider session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Tab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TabLabel` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PeSubTab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DynamicSubTab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `AssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NavigationDepth` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TracerState` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HexIsDirty` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasEntryPoint` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsNativeAot` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsNetFramework` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public CurrentViewPayload(int Tab, string TabLabel, int PeSubTab, int DynamicSubTab, string AssemblyPath, int NavigationDepth, string? TracerState, bool HexIsDirty, bool HasEntryPoint, bool IsNativeAot, bool IsNetFramework)
```

## Properties

### AssemblyPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyPath { get; init; }
```

### DynamicSubTab

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DynamicSubTab { get; init; }
```

### HasEntryPoint

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasEntryPoint { get; init; }
```

### HexIsDirty

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HexIsDirty { get; init; }
```

### IsNativeAot

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsNativeAot { get; init; }
```

### IsNetFramework

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsNetFramework { get; init; }
```

### NavigationDepth

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int NavigationDepth { get; init; }
```

### PeSubTab

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int PeSubTab { get; init; }
```

### Tab

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Tab { get; init; }
```

### TabLabel

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TabLabel { get; init; }
```

### TracerState

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TracerState { get; init; }
```

## Methods

### Deconstruct(out int, out string, out int, out int, out string, out int, out string?, out bool, out bool, out bool, out bool)

**Parameters:**

- `Tab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TabLabel` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PeSubTab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DynamicSubTab` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `AssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NavigationDepth` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TracerState` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HexIsDirty` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasEntryPoint` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsNativeAot` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `IsNetFramework` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out int Tab, out string TabLabel, out int PeSubTab, out int DynamicSubTab, out string AssemblyPath, out int NavigationDepth, out string? TracerState, out bool HexIsDirty, out bool HasEntryPoint, out bool IsNativeAot, out bool IsNetFramework)
```

### Equals(CurrentViewPayload?)

**Parameters:**

- `other` ([CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CurrentViewPayload? other)
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

### operator !=(CurrentViewPayload?, CurrentViewPayload?)

**Parameters:**

- `left` ([CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/))
- `right` ([CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CurrentViewPayload? left, CurrentViewPayload? right)
```

### operator ==(CurrentViewPayload?, CurrentViewPayload?)

**Parameters:**

- `left` ([CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/))
- `right` ([CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CurrentViewPayload? left, CurrentViewPayload? right)
```
