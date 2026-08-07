---
title: "IlDisassemblyPayload"
description: "IL and optional portable-PDB data for one method. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.ildisassemblypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

IL and optional portable-PDB data for one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlDisassemblyPayload : IEquatable<IlDisassemblyPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlDisassemblyPayload**

## Implements

- [IEquatable\<IlDisassemblyPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### IlDisassemblyPayload(MethodDefInfo, PdbProvenance, SourceLinkInfo, MethodDebugInfo?, IReadOnlyList\<IlInstruction\>)

IL and optional portable-PDB data for one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))
- `Pdb` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/))
- `SourceLink` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `DebugInfo` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))
- `Instructions` ([IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public IlDisassemblyPayload(MethodDefInfo Method, PdbProvenance Pdb, SourceLinkInfo SourceLink, MethodDebugInfo? DebugInfo, IReadOnlyList<IlInstruction> Instructions)
```

## Properties

### DebugInfo

**Returns:** [MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/)

```csharp
public MethodDebugInfo? DebugInfo { get; init; }
```

### Instructions

**Returns:** [IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<IlInstruction> Instructions { get; init; }
```

### Method

**Returns:** [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

```csharp
public MethodDefInfo Method { get; init; }
```

### Pdb

**Returns:** [PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)

```csharp
public PdbProvenance Pdb { get; init; }
```

### SourceLink

**Returns:** [SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/)

```csharp
public SourceLinkInfo SourceLink { get; init; }
```

## Methods

### Deconstruct(out MethodDefInfo, out PdbProvenance, out SourceLinkInfo, out MethodDebugInfo?, out IReadOnlyList\<IlInstruction\>)

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))
- `Pdb` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/))
- `SourceLink` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `DebugInfo` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))
- `Instructions` ([IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out MethodDefInfo Method, out PdbProvenance Pdb, out SourceLinkInfo SourceLink, out MethodDebugInfo? DebugInfo, out IReadOnlyList<IlInstruction> Instructions)
```

### Equals(IlDisassemblyPayload?)

**Parameters:**

- `other` ([IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlDisassemblyPayload? other)
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

### operator !=(IlDisassemblyPayload?, IlDisassemblyPayload?)

**Parameters:**

- `left` ([IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/))
- `right` ([IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlDisassemblyPayload? left, IlDisassemblyPayload? right)
```

### operator ==(IlDisassemblyPayload?, IlDisassemblyPayload?)

**Parameters:**

- `left` ([IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/))
- `right` ([IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlDisassemblyPayload? left, IlDisassemblyPayload? right)
```
