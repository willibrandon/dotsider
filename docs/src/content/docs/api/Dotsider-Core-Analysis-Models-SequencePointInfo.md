---
title: "SequencePointInfo"
description: "A source sequence point decoded from a portable PDB."
slug: api/dotsider.core.analysis.models.sequencepointinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A source sequence point decoded from a portable PDB.

```csharp
public sealed record SequencePointInfo : IEquatable<SequencePointInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SequencePointInfo**

## Implements

- [IEquatable\<SequencePointInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SequencePointInfo(int, string?, int, int, int, int, bool, string?, bool)

A source sequence point decoded from a portable PDB.

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The IL offset where the sequence point starts.
- `Document` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source document path.
- `StartLine` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The source start line.
- `StartColumn` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The source start column.
- `EndLine` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The source end line.
- `EndColumn` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The source end column.
- `IsHidden` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the sequence point is hidden.
- `SourceLinkUrl` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The Source Link URL resolved for the document, or null.
- `HasEmbeddedSource` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the document has embedded source.

```csharp
public SequencePointInfo(int Offset, string? Document, int StartLine, int StartColumn, int EndLine, int EndColumn, bool IsHidden, string? SourceLinkUrl, bool HasEmbeddedSource)
```

## Properties

### Document

The source document path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Document { get; init; }
```

### EndColumn

The source end column.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EndColumn { get; init; }
```

### EndLine

The source end line.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EndLine { get; init; }
```

### HasEmbeddedSource

Whether the document has embedded source.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasEmbeddedSource { get; init; }
```

### IsHidden

Whether the sequence point is hidden.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsHidden { get; init; }
```

### Offset

The IL offset where the sequence point starts.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Offset { get; init; }
```

### SourceLinkUrl

The Source Link URL resolved for the document, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceLinkUrl { get; init; }
```

### StartColumn

The source start column.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StartColumn { get; init; }
```

### StartLine

The source start line.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StartLine { get; init; }
```

