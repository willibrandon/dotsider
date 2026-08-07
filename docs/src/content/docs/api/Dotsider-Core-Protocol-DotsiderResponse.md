---
title: "DotsiderResponse"
description: "JSON response from a dotsider diagnostics socket."
slug: api/dotsider.core.protocol.dotsiderresponse
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

JSON response from a dotsider diagnostics socket.

```csharp
public sealed class DotsiderResponse
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotsiderResponse**

## Constructors

### DotsiderResponse()

```csharp
public DotsiderResponse()
```

## Properties

### Data

Response payload.

**Returns:** [Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public JsonElement? Data { get; set; }
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

- `error` ([String](https://learn.microsoft.com/dotnet/api/system.string))

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Fail(string error)
```

### Ok()

Creates a successful response without a payload.

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Ok()
```

### Ok(JsonElement?)

Creates a successful response from an existing JSON payload.

**Parameters:**

- `data` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Ok(JsonElement? data)
```

### Ok\<T\>(T, JsonTypeInfo\<T\>)

Creates a successful response using source-generated JSON metadata.

**Parameters:**

- `data` (\<T\>)
- `jsonTypeInfo` ([JsonTypeInfo\<\<T\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1))

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Ok<T>(T data, JsonTypeInfo<T> jsonTypeInfo)
```

### Ok\<T\>(T)

Creates a successful response using the protocol's source-generated metadata.

**Parameters:**

- `data` (\<T\>)

**Returns:** [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

```csharp
public static DotsiderResponse Ok<T>(T data)
```
