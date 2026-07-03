---
title: "CodeBaseEntry"
description: "One &lt;codeBase&gt; entry parsed from a .NET Framework configuration file or publisher-policy assembly. CodeBase entries are honored only for strong-named binds at the version specified."
slug: api/dotsider.core.analysis.models.codebaseentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One `&lt;codeBase&gt;` entry parsed from a .NET Framework configuration file or
publisher-policy assembly. CodeBase entries are honored only for strong-named binds at
the version specified.

```csharp
public sealed record CodeBaseEntry : IEquatable<CodeBaseEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CodeBaseEntry**

## Implements

- [IEquatable\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CodeBaseEntry(PolicyLayer, string, string?, string, Version, string)

One `&lt;codeBase&gt;` entry parsed from a .NET Framework configuration file or
publisher-policy assembly. CodeBase entries are honored only for strong-named binds at
the version specified.

**Parameters:**

- `Source` ([PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)): Which policy layer this codeBase came from.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Simple name of the assembly.
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Hex-string PKT.
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Culture, defaulting to `"neutral"`.
- `Version` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): The version this codeBase is anchored to.
- `Href` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The configured `href`, either an absolute path/URL or a path relative to the
application base.

```csharp
public CodeBaseEntry(PolicyLayer Source, string Name, string? PublicKeyToken, string Culture, Version Version, string Href)
```

## Properties

### Culture

Culture, defaulting to `"neutral"`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Culture { get; init; }
```

### Href

The configured `href`, either an absolute path/URL or a path relative to the
application base.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Href { get; init; }
```

### Name

Simple name of the assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### PublicKeyToken

Hex-string PKT.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Source

Which policy layer this codeBase came from.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
public PolicyLayer Source { get; init; }
```

### Version

The version this codeBase is anchored to.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version Version { get; init; }
```

