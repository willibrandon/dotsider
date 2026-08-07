---
title: "MstatCandidatePayload"
description: "A possible match for an ambiguous mstat query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatcandidatepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A possible match for an ambiguous mstat query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatCandidatePayload : IEquatable<MstatCandidatePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatCandidatePayload**

## Implements

- [IEquatable\<MstatCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatCandidatePayload(MstatSectionKind, string, string, string, long, int, IReadOnlyList\<string\>)

A possible match for an ambiguous mstat query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Section` ([MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/))
- `Key` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MstatCandidatePayload(MstatSectionKind Section, string Key, string FullPath, string DisplayName, long Size, int EntryCount, IReadOnlyList<string> NodeNames)
```

## Properties

### DisplayName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayName { get; init; }
```

### EntryCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryCount { get; init; }
```

### FullPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Key

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Key { get; init; }
```

### NodeNames

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> NodeNames { get; init; }
```

### Section

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
public MstatSectionKind Section { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

## Methods

### Deconstruct(out MstatSectionKind, out string, out string, out string, out long, out int, out IReadOnlyList\<string\>)

**Parameters:**

- `Section` ([MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/))
- `Key` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out MstatSectionKind Section, out string Key, out string FullPath, out string DisplayName, out long Size, out int EntryCount, out IReadOnlyList<string> NodeNames)
```

### Equals(MstatCandidatePayload?)

**Parameters:**

- `other` ([MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatCandidatePayload? other)
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

### operator !=(MstatCandidatePayload?, MstatCandidatePayload?)

**Parameters:**

- `left` ([MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/))
- `right` ([MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatCandidatePayload? left, MstatCandidatePayload? right)
```

### operator ==(MstatCandidatePayload?, MstatCandidatePayload?)

**Parameters:**

- `left` ([MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/))
- `right` ([MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatCandidatePayload? left, MstatCandidatePayload? right)
```
