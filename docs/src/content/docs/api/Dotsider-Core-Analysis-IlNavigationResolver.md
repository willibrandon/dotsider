---
title: "IlNavigationResolver"
description: "Resolves a metadata token from an IL instruction to an IlNavigationTarget describing what the token points to and where it lives."
slug: api/dotsider.core.analysis.ilnavigationresolver
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves a metadata token from an IL instruction to an [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/)
describing what the token points to and where it lives.

```csharp
public static class IlNavigationResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlNavigationResolver**

## Methods

### Resolve(AssemblyAnalyzer, int, MethodDefInfo?)

Resolves the given metadata token against the analyzer's metadata tables.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The assembly analyzer containing the metadata.
- `token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The raw metadata token from an IL instruction operand.
- `contextMethod` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method whose IL body produced the token, when known. Needed to resolve
bare generic-parameter TypeSpecs (`ELEMENT_TYPE_VAR`/`ELEMENT_TYPE_MVAR`),
which do not encode their generic owner on their own.

**Returns:** [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/)

The resolved navigation target.

```csharp
public static IlNavigationTarget Resolve(AssemblyAnalyzer analyzer, int token, MethodDefInfo? contextMethod = null)
```
