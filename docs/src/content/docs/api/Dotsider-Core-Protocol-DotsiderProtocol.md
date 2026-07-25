---
title: "DotsiderProtocol"
description: "Constants for the dotsider diagnostics protocol."
slug: api/dotsider.core.protocol.dotsiderprotocol
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Constants for the dotsider diagnostics protocol.

```csharp
public static class DotsiderProtocol
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotsiderProtocol**

## Fields

### MaxRequestBytes

Maximum UTF-8 byte length of a diagnostics request payload, excluding
an optional UTF-8 byte-order mark and the line delimiter.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int MaxRequestBytes = 1048576
```

### Version

Current protocol version. Changing field types or semantics bumps this;
adding optional fields does not.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int Version = 2
```

