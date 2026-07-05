---
title: "ReadyToRunMethodEntry"
description: "A managed method joined to its precompiled ReadyToRun native code: the owning assembly identity, the MethodDef token (or the instantiation for a generic), and the full ordered list of ReadyToRunCodeRange blocks that make up the body."
slug: api/dotsider.core.analysis.models.readytorunmethodentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A managed method joined to its precompiled ReadyToRun native code: the owning assembly
identity, the MethodDef token (or the instantiation for a generic), and the full ordered list
of [ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/) blocks that make up the body.

```csharp
public sealed record ReadyToRunMethodEntry : IEquatable<ReadyToRunMethodEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunMethodEntry**

## Implements

- [IEquatable\<ReadyToRunMethodEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunMethodEntry(string, Guid, int, string?, string?, string?, IReadOnlyList\<ReadyToRunCodeRange\>, int, int, bool, string?, long)

A managed method joined to its precompiled ReadyToRun native code: the owning assembly
identity, the MethodDef token (or the instantiation for a generic), and the full ordered list
of [ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/) blocks that make up the body.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly that owns the method.
- `Mvid` ([Guid](https://learn.microsoft.com/dotnet/api/system.guid)): The owning assembly's module version id (composite identity validation).
- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token (`0x06000000 | rid`).
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring type's display name, or null when metadata is unavailable.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's simple name, or null when metadata is unavailable.
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's decoded signature, or null when metadata is unavailable.
- `CodeRanges` ([IReadOnlyList\<ReadyToRunCodeRange\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The ordered native code blocks (hot entry, funclets, cold) — never empty for a precompiled method.
- `EntryPointRuntimeFunctionId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The index of the method's first runtime function in the RuntimeFunctions table.
- `RuntimeFunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of runtime functions the method owns (hot funclets plus cold).
- `IsGenericInstantiation` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this entry is a generic instantiation from the InstanceMethodEntryPoints table.
- `InstantiationDisplay` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A rendered instantiation (e.g. `Describe&lt;int&gt;`), or null for a non-generic entry.
- `TotalSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The total native code size in bytes, summed across CodeRanges.

```csharp
public ReadyToRunMethodEntry(string AssemblyName, Guid Mvid, int Token, string? DeclaringType, string? Name, string? Signature, IReadOnlyList<ReadyToRunCodeRange> CodeRanges, int EntryPointRuntimeFunctionId, int RuntimeFunctionCount, bool IsGenericInstantiation, string? InstantiationDisplay, long TotalSize)
```

## Properties

### AssemblyName

The simple name of the assembly that owns the method.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### CodeRanges

The ordered native code blocks (hot entry, funclets, cold) — never empty for a precompiled method.

**Returns:** [IReadOnlyList\<ReadyToRunCodeRange\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunCodeRange> CodeRanges { get; init; }
```

### DeclaringType

The declaring type's display name, or null when metadata is unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? DeclaringType { get; init; }
```

### EntryPointRuntimeFunctionId

The index of the method's first runtime function in the RuntimeFunctions table.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryPointRuntimeFunctionId { get; init; }
```

### InstantiationDisplay

A rendered instantiation (e.g. `Describe&lt;int&gt;`), or null for a non-generic entry.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? InstantiationDisplay { get; init; }
```

### IsGenericInstantiation

Whether this entry is a generic instantiation from the InstanceMethodEntryPoints table.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsGenericInstantiation { get; init; }
```

### Mvid

The owning assembly's module version id (composite identity validation).

**Returns:** [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

```csharp
public Guid Mvid { get; init; }
```

### Name

The method's simple name, or null when metadata is unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Name { get; init; }
```

### RuntimeFunctionCount

The number of runtime functions the method owns (hot funclets plus cold).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RuntimeFunctionCount { get; init; }
```

### Signature

The method's decoded signature, or null when metadata is unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Signature { get; init; }
```

### Token

The method's metadata token (`0x06000000 | rid`).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

### TotalSize

The total native code size in bytes, summed across CodeRanges.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalSize { get; init; }
```

