---
title: "ResourceInfo"
description: "Information about a managed resource embedded in the assembly."
slug: api/dotsider.core.analysis.models.resourceinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a managed resource embedded in the assembly.

```csharp
public sealed record ResourceInfo : IEquatable<ResourceInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ResourceInfo**

## Implements

- [IEquatable\<ResourceInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ResourceInfo(string, string, int, long, bool)

Information about a managed resource embedded in the assembly.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The name of the resource.
- `Visibility` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Whether the resource is public or private.
- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The byte offset of the resource data within the resources section.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The size of the resource data in bytes, or -1 if unknown.
- `IsLinked` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this is a linked (external) resource rather than embedded.

```csharp
public ResourceInfo(string Name, string Visibility, int Offset, long Size, bool IsLinked)
```

## Properties

### IsLinked

Whether this is a linked (external) resource rather than embedded.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsLinked { get; init; }
```

### Name

The name of the resource.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Offset

The byte offset of the resource data within the resources section.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Offset { get; init; }
```

### Size

The size of the resource data in bytes, or -1 if unknown.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### Visibility

Whether the resource is public or private.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Visibility { get; init; }
```

