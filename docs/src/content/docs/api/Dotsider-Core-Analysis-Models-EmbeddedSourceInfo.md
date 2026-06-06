---
title: "EmbeddedSourceInfo"
description: "Embedded source decoded from a portable PDB document."
slug: api/dotsider.core.analysis.models.embeddedsourceinfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Embedded source decoded from a portable PDB document.

```csharp
public sealed record EmbeddedSourceInfo : IEquatable<EmbeddedSourceInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **EmbeddedSourceInfo**

## Implements

- [IEquatable\<EmbeddedSourceInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### EmbeddedSourceInfo(string, string, byte[])

Embedded source decoded from a portable PDB document.

**Parameters:**

- `Document` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The PDB document path.
- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded source text.
- `Bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): The decoded source bytes.

```csharp
public EmbeddedSourceInfo(string Document, string Text, byte[] Bytes)
```

## Properties

### Bytes

The decoded source bytes.

**Returns:** [Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])

```csharp
public byte[] Bytes { get; init; }
```

### Document

The PDB document path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Document { get; init; }
```

### Text

The decoded source text.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Text { get; init; }
```

