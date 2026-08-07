---
title: "MstatContributorPayload"
description: "One Native AOT size contributor. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatcontributorpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

One Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatContributorPayload : IEquatable<MstatContributorPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatContributorPayload**

## Implements

- [IEquatable\<MstatContributorPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatContributorPayload(MstatSectionKind, string, string, string, string, string, string, string, long, int, IReadOnlyList\<string\>, IReadOnlyList\<MstatWhyChainPayload\>?)

One Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Section` ([MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/))
- `Key` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `LeafName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `WhyChains` ([IReadOnlyList\<MstatWhyChainPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MstatContributorPayload(MstatSectionKind Section, string Key, string AssemblyName, string Namespace, string TypeName, string LeafName, string DisplayName, string FullPath, long Size, int EntryCount, IReadOnlyList<string> NodeNames, IReadOnlyList<MstatWhyChainPayload>? WhyChains)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

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

### LeafName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LeafName { get; init; }
```

### Namespace

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
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

### TypeName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TypeName { get; init; }
```

### WhyChains

**Returns:** [IReadOnlyList\<MstatWhyChainPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatWhyChainPayload>? WhyChains { get; init; }
```

## Methods

### Deconstruct(out MstatSectionKind, out string, out string, out string, out string, out string, out string, out string, out long, out int, out IReadOnlyList\<string\>, out IReadOnlyList\<MstatWhyChainPayload\>?)

**Parameters:**

- `Section` ([MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/))
- `Key` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `LeafName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `WhyChains` ([IReadOnlyList\<MstatWhyChainPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out MstatSectionKind Section, out string Key, out string AssemblyName, out string Namespace, out string TypeName, out string LeafName, out string DisplayName, out string FullPath, out long Size, out int EntryCount, out IReadOnlyList<string> NodeNames, out IReadOnlyList<MstatWhyChainPayload>? WhyChains)
```

### Equals(MstatContributorPayload?)

**Parameters:**

- `other` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatContributorPayload? other)
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

### operator !=(MstatContributorPayload?, MstatContributorPayload?)

**Parameters:**

- `left` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))
- `right` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatContributorPayload? left, MstatContributorPayload? right)
```

### operator ==(MstatContributorPayload?, MstatContributorPayload?)

**Parameters:**

- `left` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))
- `right` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatContributorPayload? left, MstatContributorPayload? right)
```
