---
title: "DotsiderResponse"
description: "JSON response from a dotsider diagnostics socket."
slug: api/dotsider.core.protocol.dotsiderresponse
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

JSON response from a dotsider diagnostics socket.

```csharp
public sealed class DotsiderResponse
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotsiderResponse**

## Properties

### Data

Response payload, serialized as the appropriate type.

**Returns:** [Object](https://learn.microsoft.com/dotnet/api/system.object)

```csharp
public object? Data { get; set; }
```

### Error

Error message if the request failed.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Error { get; set; }
```

### Success

Whether the request succeeded.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Success { get; set; }
```

### V

Protocol version echoed in every response.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
[JsonRequired]
public int V { get; set; }
```

## Methods

### Fail(string)

Creates an error response with the given message.

**Parameters:**

- `error` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Fail(string error)
```

### Ok(object?)

Creates a successful response with the given data.

**Parameters:**

- `data` ([Object](https://learn.microsoft.com/dotnet/api/system.object)): 

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Ok(object? data = null)
```

