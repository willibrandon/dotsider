---
title: "AssemblyAnalyzer"
description: "Core analyzer that reads a .NET assembly and extracts PE, metadata, IL, and string information. Uses PEReader and MetadataReader from the BCL."
slug: api/dotsider.core.analysis.assemblyanalyzer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Core analyzer that reads a .NET assembly and extracts PE, metadata, IL, and string information.
Uses [PEReader](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.pereader) and [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader) from the BCL.

```csharp
public sealed class AssemblyAnalyzer : IDisposable
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyAnalyzer**

## Implements

- [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

## Constructors

### AssemblyAnalyzer(byte[], string)

Creates an analyzer from raw bytes in memory. Used as a last-resort
fallback when disk I/O is unavailable after a save operation.

**Parameters:**

- `bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): 
- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public AssemblyAnalyzer(byte[] bytes, string filePath)
```

### AssemblyAnalyzer(string)

Opens and analyzes the specified .NET assembly file.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Absolute path to the assembly file.

```csharp
public AssemblyAnalyzer(string filePath)
```

## Properties

### Architecture

The PE architecture description (e.g., "AnyCPU", "x64", "ARM64").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; }
```

### AssemblyName

The assembly simple name, or null if the file has no assembly manifest.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; }
```

### AssemblyRefs

Gets the AssemblyRef metadata table entries.

**Returns:** [IReadOnlyList\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyRefInfo> AssemblyRefs { get; }
```

### AssemblyVersion

The assembly version string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyVersion { get; }
```

### ClrHeader

The parsed CLR header, or null if not a .NET assembly.

**Returns:** [ClrHeader](/api/dotsider.core.analysis.models.clrheader/)

```csharp
public ClrHeader? ClrHeader { get; }
```

### CreatedTime

The creation time in UTC.

**Returns:** [DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

```csharp
public DateTime CreatedTime { get; }
```

### Culture

The assembly culture, or null for culture-neutral assemblies.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Culture { get; }
```

### CustomAttributes

Gets the custom attributes applied to metadata entities.

**Returns:** [IReadOnlyList\<CustomAttributeInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; }
```

### FieldDefs

Gets the FieldDef metadata table entries.

**Returns:** [IReadOnlyList\<FieldDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<FieldDefInfo> FieldDefs { get; }
```

### FileName

The file name without directory path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FileName { get; }
```

### FilePath

The full path to the analyzed assembly file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; }
```

### FileSize

The file size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileSize { get; }
```

### HasMetadata

Whether the PE file contains .NET metadata.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasMetadata { get; }
```

### IsReadOnly

Whether the file is read-only on disk.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsReadOnly { get; }
```

### LastModified

The last modification time in UTC.

**Returns:** [DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

```csharp
public DateTime LastModified { get; }
```

### MemberRefs

Gets the MemberRef metadata table entries.

**Returns:** [IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MemberRefInfo> MemberRefs { get; }
```

### MethodDefs

Gets the MethodDef metadata table entries.

**Returns:** [IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MethodDefInfo> MethodDefs { get; }
```

### PeHeaders

The parsed PE headers.

**Returns:** [PeHeaders](/api/dotsider.core.analysis.models.peheaders/)

```csharp
public PeHeaders? PeHeaders { get; }
```

### PublicKeyToken

The public key token as a hex string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; }
```

### RawBytes

Gets the raw bytes of the file for hex editor display.

**Returns:** [ReadOnlyMemory\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlymemory-1)

```csharp
public ReadOnlyMemory<byte> RawBytes { get; }
```

### Resources

Gets the manifest resources defined in the assembly.

**Returns:** [IReadOnlyList\<ResourceInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ResourceInfo> Resources { get; }
```

### Sections

Gets the PE sections.

**Returns:** [IReadOnlyList\<SectionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SectionInfo> Sections { get; }
```

### TargetFramework

The target framework moniker (e.g., ".NETCoreApp,Version=v10.0"), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetFramework { get; }
```

### TypeDefs

Gets the TypeDef metadata table entries.

**Returns:** [IReadOnlyList\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TypeDefInfo> TypeDefs { get; }
```

### TypeRefs

Gets the TypeRef metadata table entries.

**Returns:** [IReadOnlyList\<TypeRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TypeRefInfo> TypeRefs { get; }
```

## Methods

### Dispose()

Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

```csharp
public void Dispose()
```

### GetMetadataReader()

Gets the underlying [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader) for advanced queries.
Returns null if the file has no .NET metadata.

**Returns:** [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader)

```csharp
public MetadataReader? GetMetadataReader()
```

### GetMethodBody(MethodDefInfo)

Gets the method body bytes for IL disassembly.
Returns null if the method has no IL body (abstract, extern, or native).

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method definition to get the body for.

**Returns:** [MethodBodyBlock](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.methodbodyblock)

The method body block, or null.

```csharp
public MethodBodyBlock? GetMethodBody(MethodDefInfo method)
```

### ResolveAssemblyPath(string, string)

Attempts to resolve a referenced assembly name to a file path on disk.
Searches the same directory as the referencing assembly, then .NET runtime dirs.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public static string? ResolveAssemblyPath(string referencingAssemblyPath, string assemblyName)
```

### ResolveToken(int)

Resolves a metadata token to a human-readable name.

**Parameters:**

- `token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token to resolve.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A display string for the token.

```csharp
public string ResolveToken(int token)
```

