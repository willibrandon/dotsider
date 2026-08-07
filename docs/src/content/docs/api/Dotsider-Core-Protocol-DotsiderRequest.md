---
title: "DotsiderRequest"
description: "JSON request sent to a dotsider diagnostics socket."
slug: api/dotsider.core.protocol.dotsiderrequest
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

JSON request sent to a dotsider diagnostics socket.

```csharp
public sealed class DotsiderRequest
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotsiderRequest**

## Constructors

### DotsiderRequest()

```csharp
public DotsiderRequest()
```

## Properties

### Arguments

Literal command-line arguments for starting a trace.

**Returns:** [String[]](https://learn.microsoft.com/dotnet/api/system.string[])

```csharp
public string[]? Arguments { get; set; }
```

### AssemblyName

Assembly name to resolve (e.g. "System.Runtime"), used by resolve-assembly and push-assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; set; }
```

### AssemblyPath

Path to an assembly file (for direct analysis or diff).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyPath { get; set; }
```

### BaselinePath

Baseline binary or mstat path for check-size-budgets.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BaselinePath { get; set; }
```

### BudgetFilePath

Path to a size-budget JSON file for check-size-budgets.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BudgetFilePath { get; set; }
```

### Budgets

Budget spec strings for check-size-budgets, in the size-budget grammar.

**Returns:** [String[]](https://learn.microsoft.com/dotnet/api/system.string[])

```csharp
public string[]? Budgets { get; set; }
```

### BudgetsJson

An inline size-budget JSON document for check-size-budgets (the budget-file schema).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? BudgetsJson { get; set; }
```

### CategoryFilter

Trace event category filter.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CategoryFilter { get; set; }
```

### IncludeCompilerGenerated

Whether member search includes compiler-generated members.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IncludeCompilerGenerated { get; set; }
```

### IncludeDebugInfo

Whether IL responses should include portable PDB debug information.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IncludeDebugInfo { get; set; }
```

### IncludeTree

Whether a diff-size response includes the delta tree.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IncludeTree { get; set; }
```

### IncludeWhy

Whether Native AOT contributor responses should include DGML why chains.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IncludeWhy { get; set; }
```

### LeftPath

Left assembly path for diff.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? LeftPath { get; set; }
```

### Length

Byte count for read-bytes.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? Length { get; set; }
```

### MaxCandidates

Maximum ambiguous candidates returned by Native AOT explanation tools.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MaxCandidates { get; set; }
```

### MaxNodes

The delta-tree node cap for diff-size when [IncludeTree](/api/dotsider.core.protocol.dotsiderrequest.includetree/) is set.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MaxNodes { get; set; }
```

### MaxResults

Maximum number of results to return.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MaxResults { get; set; }
```

### MaxWhyChains

Maximum DGML chains returned for one aggregated Native AOT contributor.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MaxWhyChains { get; set; }
```

### Method

The method to invoke (e.g. "assembly-info", "list-types", "disassemble").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Method { get; set; }
```

### MethodName

Full or partial method name for disassembly or filtering.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MethodName { get; set; }
```

### MethodOrAddress

Method name (optionally `Type.Method`) or `0x…` native address for correlate-method.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MethodOrAddress { get; set; }
```

### MinLength

Minimum string length for raw string extraction.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MinLength { get; set; }
```

### NamespaceName

Namespace filter for Native AOT size contributor tools.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NamespaceName { get; set; }
```

### Offset

Byte offset for read-bytes.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? Offset { get; set; }
```

### Query

Search query for find-members or search-il-opcodes.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Query { get; set; }
```

### RightPath

Right assembly path for diff.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? RightPath { get; set; }
```

### Section

mstat section filter for Native AOT size contributor tools.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Section { get; set; }
```

### SymbolAddress

Native symbol virtual address (hex `0x…` or decimal) for disassemble-native.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SymbolAddress { get; set; }
```

### SymbolName

Native symbol name for disassemble-native (managed name, raw name, or suffix).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SymbolName { get; set; }
```

### TabId

Tab identifier for navigation.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TabId { get; set; }
```

### Target

Target name, path, key, or node label for Native AOT size explanation tools.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Target { get; set; }
```

### Token

Metadata token for resolve-token.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? Token { get; set; }
```

### TopN

How many top contributors diff-size and check-size-budgets responses carry.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TopN { get; set; }
```

### TypeName

Full or partial type name for filtering.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TypeName { get; set; }
```

### V

Protocol version. Must match [Version](/api/dotsider.core.protocol.dotsiderprotocol.version/).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
[JsonRequired]
public int V { get; set; }
```
