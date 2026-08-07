---
title: "MstatWhyPayload"
description: "Outcome of a Native AOT dependency explanation query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatwhypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Outcome of a Native AOT dependency explanation query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatWhyPayload : IEquatable<MstatWhyPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatWhyPayload**

## Implements

- [IEquatable\<MstatWhyPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatWhyPayload(string, MstatSourceSummaryPayload, string, string?, int?, IReadOnlyList\<MstatCandidatePayload\>?, bool?, MstatContributorPayload?)

Outcome of a Native AOT dependency explanation query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Source` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `Outcome` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CandidateCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Candidates` ([IReadOnlyList\<MstatCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Truncated` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Contributor` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))

```csharp
public MstatWhyPayload(string Target, MstatSourceSummaryPayload Source, string Outcome, string? Message = null, int? CandidateCount = null, IReadOnlyList<MstatCandidatePayload>? Candidates = null, bool? Truncated = null, MstatContributorPayload? Contributor = null)
```

## Properties

### CandidateCount

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? CandidateCount { get; init; }
```

### Candidates

**Returns:** [IReadOnlyList\<MstatCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatCandidatePayload>? Candidates { get; init; }
```

### Contributor

**Returns:** [MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/)

```csharp
public MstatContributorPayload? Contributor { get; init; }
```

### Message

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Message { get; init; }
```

### Outcome

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Outcome { get; init; }
```

### Source

**Returns:** [MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/)

```csharp
public MstatSourceSummaryPayload Source { get; init; }
```

### Target

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Target { get; init; }
```

### Truncated

**Returns:** [Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public bool? Truncated { get; init; }
```

## Methods

### Deconstruct(out string, out MstatSourceSummaryPayload, out string, out string?, out int?, out IReadOnlyList\<MstatCandidatePayload\>?, out bool?, out MstatContributorPayload?)

**Parameters:**

- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Source` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `Outcome` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CandidateCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Candidates` ([IReadOnlyList\<MstatCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Truncated` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Contributor` ([MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/))

```csharp
public void Deconstruct(out string Target, out MstatSourceSummaryPayload Source, out string Outcome, out string? Message, out int? CandidateCount, out IReadOnlyList<MstatCandidatePayload>? Candidates, out bool? Truncated, out MstatContributorPayload? Contributor)
```

### Equals(MstatWhyPayload?)

**Parameters:**

- `other` ([MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatWhyPayload? other)
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

### operator !=(MstatWhyPayload?, MstatWhyPayload?)

**Parameters:**

- `left` ([MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/))
- `right` ([MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatWhyPayload? left, MstatWhyPayload? right)
```

### operator ==(MstatWhyPayload?, MstatWhyPayload?)

**Parameters:**

- `left` ([MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/))
- `right` ([MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatWhyPayload? left, MstatWhyPayload? right)
```
