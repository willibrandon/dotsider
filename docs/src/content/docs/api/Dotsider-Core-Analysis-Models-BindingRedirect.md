---
title: "BindingRedirect"
description: "One &lt;bindingRedirect&gt; entry parsed from a .NET Framework configuration file or a publisher-policy assembly's embedded XML resource."
slug: api/dotsider.core.analysis.models.bindingredirect
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One `&lt;bindingRedirect&gt;` entry parsed from a .NET Framework configuration file
or a publisher-policy assembly's embedded XML resource.

```csharp
public sealed record BindingRedirect : IEquatable<BindingRedirect>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BindingRedirect**

## Implements

- [IEquatable\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BindingRedirect(PolicyLayer, string, string?, string, string?, Version, Version, Version)

One `&lt;bindingRedirect&gt;` entry parsed from a .NET Framework configuration file
or a publisher-policy assembly's embedded XML resource.

**Parameters:**

- `Source` ([PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)): Which policy layer this redirect came from.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Simple name of the redirected assembly.
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Hex-string PKT, lower-cased; null for weak-named.
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Culture, defaulting to `"neutral"`.
- `ProcessorArchitecture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): `processorArchitecture` attribute on `&lt;assemblyIdentity&gt;`, or
null when unspecified (applies to any architecture).
- `OldMin` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): Inclusive lower bound of the redirected range.
- `OldMax` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): Inclusive upper bound of the redirected range.
- `NewVersion` ([Version](https://learn.microsoft.com/dotnet/api/system.version)): The version the binder will use instead.

```csharp
public BindingRedirect(PolicyLayer Source, string Name, string? PublicKeyToken, string Culture, string? ProcessorArchitecture, Version OldMin, Version OldMax, Version NewVersion)
```

## Properties

### Culture

Culture, defaulting to `"neutral"`.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Culture { get; init; }
```

### Name

Simple name of the redirected assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### NewVersion

The version the binder will use instead.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version NewVersion { get; init; }
```

### OldMax

Inclusive upper bound of the redirected range.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version OldMax { get; init; }
```

### OldMin

Inclusive lower bound of the redirected range.

**Returns:** [Version](https://learn.microsoft.com/dotnet/api/system.version)

```csharp
public Version OldMin { get; init; }
```

### ProcessorArchitecture

`processorArchitecture` attribute on `&lt;assemblyIdentity&gt;`, or
null when unspecified (applies to any architecture).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ProcessorArchitecture { get; init; }
```

### PublicKeyToken

Hex-string PKT, lower-cased; null for weak-named.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Source

Which policy layer this redirect came from.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
public PolicyLayer Source { get; init; }
```

