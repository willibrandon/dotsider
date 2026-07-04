---
title: "SourceLinkMapping"
description: "A single Source Link document mapping."
slug: api/dotsider.core.analysis.models.sourcelinkmapping
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single Source Link document mapping.

```csharp
public sealed record SourceLinkMapping : IEquatable<SourceLinkMapping>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SourceLinkMapping**

## Implements

- [IEquatable\<SourceLinkMapping\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SourceLinkMapping(string, string)

A single Source Link document mapping.

**Parameters:**

- `DocumentPattern` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The document path pattern.
- `UrlTemplate` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The URL template.

```csharp
public SourceLinkMapping(string DocumentPattern, string UrlTemplate)
```

## Properties

### DocumentPattern

The document path pattern.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DocumentPattern { get; init; }
```

### UrlTemplate

The URL template.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string UrlTemplate { get; init; }
```

