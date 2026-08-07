---
title: "SourceLinkInfo"
description: "Source Link mappings decoded from portable PDB custom debug information."
slug: api/dotsider.core.analysis.models.sourcelinkinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Source Link mappings decoded from portable PDB custom debug information.

```csharp
public sealed record SourceLinkInfo : IEquatable<SourceLinkInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SourceLinkInfo**

## Implements

- [IEquatable\<SourceLinkInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SourceLinkInfo(IReadOnlyList\<SourceLinkMapping\>)

Source Link mappings decoded from portable PDB custom debug information.

**Parameters:**

- `Mappings` ([IReadOnlyList\<SourceLinkMapping\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The document pattern to URL template mappings.

```csharp
public SourceLinkInfo(IReadOnlyList<SourceLinkMapping> Mappings)
```

## Properties

### IsPresent

Gets whether Source Link data was present.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsPresent { get; }
```

### Mappings

The document pattern to URL template mappings.

**Returns:** [IReadOnlyList\<SourceLinkMapping\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SourceLinkMapping> Mappings { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<SourceLinkMapping\>)

**Parameters:**

- `Mappings` ([IReadOnlyList\<SourceLinkMapping\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<SourceLinkMapping> Mappings)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SourceLinkInfo?)

**Parameters:**

- `other` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SourceLinkInfo? other)
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

### operator !=(SourceLinkInfo?, SourceLinkInfo?)

**Parameters:**

- `left` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `right` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SourceLinkInfo? left, SourceLinkInfo? right)
```

### operator ==(SourceLinkInfo?, SourceLinkInfo?)

**Parameters:**

- `left` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `right` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SourceLinkInfo? left, SourceLinkInfo? right)
```
