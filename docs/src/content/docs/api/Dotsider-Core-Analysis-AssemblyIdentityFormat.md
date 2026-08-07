---
title: "AssemblyIdentityFormat"
description: "Formats an assembly's full identity into a stable opaque string used as a graph node identifier and as a key for grouping TypeRefInfo entries by the full identity of their resolution scope."
slug: api/dotsider.core.analysis.assemblyidentityformat
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Formats an assembly's full identity into a stable opaque string used as a graph node
identifier and as a key for grouping [TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/) entries by the
full identity of their resolution scope.

```csharp
public static class AssemblyIdentityFormat
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyIdentityFormat**

## Methods

### Format(string, string?, string?, string?)

Formats an assembly identity into its canonical identifier string.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly simple name.
- `version` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly version, or null.
- `culture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly culture, or null/empty for culture-neutral.
- `publicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The public key token hex, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A stable opaque identifier derived from the four identity fields.

```csharp
public static string Format(string name, string? version, string? culture, string? publicKeyToken)
```

## Remarks

The format is `"{Name}|{Version}|{Culture}|{PublicKeyToken}"`. Null or empty culture
is normalized to `"neutral"` so two nodes only differ by culture when they truly do.
The identifier is treated as opaque by consumers; it is never parsed.
