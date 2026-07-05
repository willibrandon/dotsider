---
title: "DotsiderRequest"
description: "JSON request sent to a dotsider diagnostics socket."
slug: api/dotsider.core.protocol.dotsiderrequest
sidebar:
  order: 3
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

JSON request sent to a dotsider diagnostics socket.

```csharp
public sealed class DotsiderRequest
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DotsiderRequest**

## Properties

### Arguments

Command-line arguments for starting a trace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Arguments { get; set; }
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

### CategoryFilter

Trace event category filter.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? CategoryFilter { get; set; }
```

### IncludeDebugInfo

Whether IL responses should include portable PDB debug information.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IncludeDebugInfo { get; set; }
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

### MaxResults

Maximum number of results to return.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MaxResults { get; set; }
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

### Token

Metadata token for resolve-token.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? Token { get; set; }
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

