---
title: "ImportedFunctionInfo"
description: "A single function imported from a native module."
slug: api/dotsider.core.analysis.models.importedfunctioninfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single function imported from a native module.

```csharp
public sealed record ImportedFunctionInfo : IEquatable<ImportedFunctionInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ImportedFunctionInfo**

## Implements

- [IEquatable\<ImportedFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ImportedFunctionInfo(string?, ushort?, ushort?)

A single function imported from a native module.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The imported function name, or null for ordinal-only imports.
- `Ordinal` ([Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The import ordinal, or null for named imports.
- `Hint` ([Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The export-name-table hint for named imports, or null.

```csharp
public ImportedFunctionInfo(string? Name, ushort? Ordinal, ushort? Hint)
```

## Properties

### Hint

The export-name-table hint for named imports, or null.

**Returns:** [Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ushort? Hint { get; init; }
```

### Name

The imported function name, or null for ordinal-only imports.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Name { get; init; }
```

### Ordinal

The import ordinal, or null for named imports.

**Returns:** [Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ushort? Ordinal { get; init; }
```

