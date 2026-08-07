---
title: "AppliedPolicy"
description: "Records that a requested identity was rewritten by .NET Framework binding policy. Carried on AppliedPolicy so the UI can render \"↪ redirected 1.0.0.0 → 13.0.0.0 via app.config\" without inventing new AssemblyProvenance values for redirected hits — a redirect-applied AppLocal hit is still AppLocal, just with this annotation attached."
slug: api/dotsider.core.analysis.models.appliedpolicy
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Records that a requested identity was rewritten by .NET Framework binding policy. Carried
on [AppliedPolicy](/api/dotsider.core.analysis.models.graphnavigationcontext.appliedpolicy/) so the UI can render
"↪ redirected 1.0.0.0 → 13.0.0.0 via app.config" without inventing new
[AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/) values for redirected hits — a redirect-applied AppLocal
hit is still [AppLocal](/api/dotsider.core.analysis.models.assemblyprovenance.applocal/), just with this annotation attached.

```csharp
public sealed record AppliedPolicy : IEquatable<AppliedPolicy>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AppliedPolicy**

## Implements

- [IEquatable\<AppliedPolicy\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AppliedPolicy(PolicyLayer, Version, Version, string?)

Records that a requested identity was rewritten by .NET Framework binding policy. Carried
on [AppliedPolicy](/api/dotsider.core.analysis.models.graphnavigationcontext.appliedpolicy/) so the UI can render
"↪ redirected 1.0.0.0 → 13.0.0.0 via app.config" without inventing new
[AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/) values for redirected hits — a redirect-applied AppLocal
hit is still [AppLocal](/api/dotsider.core.analysis.models.assemblyprovenance.applocal/), just with this annotation attached.

**Parameters:**

- `Source` ([PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)): The policy layer that produced the rewrite.
- `RequestedVersion` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): The version named by the metadata reference.
- `BoundVersion` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): The version the binder actually loaded.
- `CodeBaseHref` ([String](https://learn.microsoft.com/dotnet/api/system.string)): When Source is [CodeBase](/api/dotsider.core.analysis.models.policylayer.codebase/), the configured
`href` attribute. null for non-codeBase sources.

```csharp
public AppliedPolicy(PolicyLayer Source, Version RequestedVersion, Version BoundVersion, string? CodeBaseHref)
```

## Properties

### BoundVersion

The version the binder actually loaded.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version BoundVersion { get; init; }
```

### CodeBaseHref

When Source is [CodeBase](/api/dotsider.core.analysis.models.policylayer.codebase/), the configured
`href` attribute. null for non-codeBase sources.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CodeBaseHref { get; init; }
```

### RequestedVersion

The version named by the metadata reference.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version RequestedVersion { get; init; }
```

### Source

The policy layer that produced the rewrite.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
public PolicyLayer Source { get; init; }
```

## Methods

### Deconstruct(out PolicyLayer, out Version, out Version, out string?)

**Parameters:**

- `Source` ([PolicyLayer](/api/dotsider.core.analysis.models.policylayer/))
- `RequestedVersion` ([Version](https://learn.microsoft.com/dotnet/api/system.version))
- `BoundVersion` ([Version](https://learn.microsoft.com/dotnet/api/system.version))
- `CodeBaseHref` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out PolicyLayer Source, out Version RequestedVersion, out Version BoundVersion, out string? CodeBaseHref)
```

### Equals(AppliedPolicy?)

**Parameters:**

- `other` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AppliedPolicy? other)
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

### operator !=(AppliedPolicy?, AppliedPolicy?)

**Parameters:**

- `left` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/))
- `right` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AppliedPolicy? left, AppliedPolicy? right)
```

### operator ==(AppliedPolicy?, AppliedPolicy?)

**Parameters:**

- `left` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/))
- `right` ([AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AppliedPolicy? left, AppliedPolicy? right)
```
