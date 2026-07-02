---
title: "ImportedModuleInfo"
description: "A native module referenced by the PE import table, with the functions imported from it."
slug: api/dotsider.core.analysis.models.importedmoduleinfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A native module referenced by the PE import table, with the functions imported from it.

```csharp
public sealed record ImportedModuleInfo : IEquatable<ImportedModuleInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ImportedModuleInfo**

## Implements

- [IEquatable\<ImportedModuleInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ImportedModuleInfo(string, IReadOnlyList\<ImportedFunctionInfo\>)

A native module referenced by the PE import table, with the functions imported from it.

**Parameters:**

- `ModuleName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The module file name (e.g. "KERNEL32.dll").
- `Functions` ([IReadOnlyList\<ImportedFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The functions imported from the module.

```csharp
public ImportedModuleInfo(string ModuleName, IReadOnlyList<ImportedFunctionInfo> Functions)
```

## Properties

### Functions

The functions imported from the module.

**Returns:** [IReadOnlyList\<ImportedFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ImportedFunctionInfo> Functions { get; init; }
```

### ModuleName

The module file name (e.g. "KERNEL32.dll").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ModuleName { get; init; }
```

