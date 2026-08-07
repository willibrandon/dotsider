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

## Methods

### Deconstruct(out string?, out ushort?, out ushort?)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Ordinal` ([Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Hint` ([Nullable\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out string? Name, out ushort? Ordinal, out ushort? Hint)
```

### Equals(ImportedFunctionInfo?)

**Parameters:**

- `other` ([ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ImportedFunctionInfo? other)
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

### operator !=(ImportedFunctionInfo?, ImportedFunctionInfo?)

**Parameters:**

- `left` ([ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/))
- `right` ([ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ImportedFunctionInfo? left, ImportedFunctionInfo? right)
```

### operator ==(ImportedFunctionInfo?, ImportedFunctionInfo?)

**Parameters:**

- `left` ([ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/))
- `right` ([ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ImportedFunctionInfo? left, ImportedFunctionInfo? right)
```
