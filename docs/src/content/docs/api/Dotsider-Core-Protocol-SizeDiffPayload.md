---
title: "SizeDiffPayload"
description: "Size differences between two mstat-backed inputs. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.sizediffpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Size differences between two mstat-backed inputs.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SizeDiffPayload : IEquatable<SizeDiffPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffPayload**

## Implements

- [IEquatable\<SizeDiffPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffPayload(string, string, SizeBasis, long?, long, string, string, SizeDiffSummary, IReadOnlyList\<SizeDiffAggregate\>, IReadOnlyList\<SizeDiffAggregate\>, IReadOnlyList\<SizeDiffContributor\>, SizeDiffNode?, bool?, int?, int?)

Size differences between two mstat-backed inputs.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Left` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Right` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `LeftTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `RightFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Summary` ([SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/))
- `AssemblyDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `NamespaceDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Contributors` ([IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Root` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))
- `TreeTruncated` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `TreeTotalNodes` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `TreeIncludedNodes` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public SizeDiffPayload(string Left, string Right, SizeBasis TotalBasis, long? LeftTotal, long RightTotal, string LeftFormatVersion, string RightFormatVersion, SizeDiffSummary Summary, IReadOnlyList<SizeDiffAggregate> AssemblyDeltas, IReadOnlyList<SizeDiffAggregate> NamespaceDeltas, IReadOnlyList<SizeDiffContributor> Contributors, SizeDiffNode? Root, bool? TreeTruncated, int? TreeTotalNodes, int? TreeIncludedNodes)
```

## Properties

### AssemblyDeltas

**Returns:** [IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffAggregate> AssemblyDeltas { get; init; }
```

### Contributors

**Returns:** [IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffContributor> Contributors { get; init; }
```

### Left

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Left { get; init; }
```

### LeftFormatVersion

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LeftFormatVersion { get; init; }
```

### LeftTotal

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? LeftTotal { get; init; }
```

### NamespaceDeltas

**Returns:** [IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffAggregate> NamespaceDeltas { get; init; }
```

### Right

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Right { get; init; }
```

### RightFormatVersion

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RightFormatVersion { get; init; }
```

### RightTotal

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightTotal { get; init; }
```

### Root

**Returns:** [SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/)

```csharp
public SizeDiffNode? Root { get; init; }
```

### Summary

**Returns:** [SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/)

```csharp
public SizeDiffSummary Summary { get; init; }
```

### TotalBasis

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
public SizeBasis TotalBasis { get; init; }
```

### TreeIncludedNodes

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TreeIncludedNodes { get; init; }
```

### TreeTotalNodes

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TreeTotalNodes { get; init; }
```

### TreeTruncated

**Returns:** [Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public bool? TreeTruncated { get; init; }
```

## Methods

### Deconstruct(out string, out string, out SizeBasis, out long?, out long, out string, out string, out SizeDiffSummary, out IReadOnlyList\<SizeDiffAggregate\>, out IReadOnlyList\<SizeDiffAggregate\>, out IReadOnlyList\<SizeDiffContributor\>, out SizeDiffNode?, out bool?, out int?, out int?)

**Parameters:**

- `Left` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Right` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `LeftTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `RightFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Summary` ([SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/))
- `AssemblyDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `NamespaceDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Contributors` ([IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Root` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))
- `TreeTruncated` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `TreeTotalNodes` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `TreeIncludedNodes` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out string Left, out string Right, out SizeBasis TotalBasis, out long? LeftTotal, out long RightTotal, out string LeftFormatVersion, out string RightFormatVersion, out SizeDiffSummary Summary, out IReadOnlyList<SizeDiffAggregate> AssemblyDeltas, out IReadOnlyList<SizeDiffAggregate> NamespaceDeltas, out IReadOnlyList<SizeDiffContributor> Contributors, out SizeDiffNode? Root, out bool? TreeTruncated, out int? TreeTotalNodes, out int? TreeIncludedNodes)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeDiffPayload?)

**Parameters:**

- `other` ([SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeDiffPayload? other)
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

### operator !=(SizeDiffPayload?, SizeDiffPayload?)

**Parameters:**

- `left` ([SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/))
- `right` ([SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeDiffPayload? left, SizeDiffPayload? right)
```

### operator ==(SizeDiffPayload?, SizeDiffPayload?)

**Parameters:**

- `left` ([SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/))
- `right` ([SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeDiffPayload? left, SizeDiffPayload? right)
```
