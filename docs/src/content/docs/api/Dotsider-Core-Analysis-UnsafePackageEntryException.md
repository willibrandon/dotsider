---
title: "UnsafePackageEntryException"
description: "The exception that is thrown when a package entry cannot be safely extracted beneath its destination directory."
slug: api/dotsider.core.analysis.unsafepackageentryexception
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

The exception that is thrown when a package entry cannot be safely extracted beneath its
destination directory.

```csharp
public sealed class UnsafePackageEntryException : IOException, ISerializable
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [SystemException](https://learn.microsoft.com/dotnet/api/system.systemexception) → [IOException](https://learn.microsoft.com/dotnet/api/system.io.ioexception) → **UnsafePackageEntryException**

## Implements

- [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

## Constructors

### UnsafePackageEntryException()

Initializes a new instance of the [UnsafePackageEntryException](/api/dotsider.core.analysis.unsafepackageentryexception/) class.

```csharp
public UnsafePackageEntryException()
```

### UnsafePackageEntryException(string?, Exception?)

Initializes a new instance of the [UnsafePackageEntryException](/api/dotsider.core.analysis.unsafepackageentryexception/) class with a
specified error message and a reference to the inner exception that caused this exception.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The message that describes the error.
- `innerException` ([Exception](https://learn.microsoft.com/dotnet/api/system.exception)): The exception that caused the current exception, or null.

```csharp
public UnsafePackageEntryException(string? message, Exception? innerException)
```

### UnsafePackageEntryException(string?)

Initializes a new instance of the [UnsafePackageEntryException](/api/dotsider.core.analysis.unsafepackageentryexception/) class with a
specified error message.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The message that describes the error.

```csharp
public UnsafePackageEntryException(string? message)
```
