---
title: "ClrHeader"
description: "CLR (Common Language Runtime) header information from the PE file's COR20 header."
slug: api/dotsider.core.analysis.models.clrheader
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

CLR (Common Language Runtime) header information from the PE file's COR20 header.

```csharp
public sealed record ClrHeader : IEquatable<ClrHeader>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ClrHeader**

## Implements

- [IEquatable\<ClrHeader\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ClrHeader(int, int, int, int, CorFlags, int, int, int, int, int, DirectoryEntry)

CLR (Common Language Runtime) header information from the PE file's COR20 header.

**Parameters:**

- `MajorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Major version of the CLR required.
- `MinorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Minor version of the CLR required.
- `MetadataRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): RVA of the metadata directory.
- `MetadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Size of the metadata directory in bytes.
- `Flags` ([CorFlags](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.corflags)): CLR header flags (ILOnly, 32BitRequired, StrongNameSigned, etc.).
- `EntryPointToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Metadata token of the entry point method, or zero.
- `ResourcesRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): RVA of the managed resources directory.
- `ResourcesSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Size of the managed resources directory.
- `StrongNameSignatureRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): RVA of the strong name signature.
- `StrongNameSignatureSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Size of the strong name signature.
- `ManagedNativeHeader` ([DirectoryEntry](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.directoryentry)): The managed native header directory. Non-empty for precompiled images: a crossgen2 ReadyToRun
image points it at the `READYTORUN_HEADER`. Empty (`Size == 0`) for a plain managed assembly.

```csharp
public ClrHeader(int MajorRuntimeVersion, int MinorRuntimeVersion, int MetadataRva, int MetadataSize, CorFlags Flags, int EntryPointToken, int ResourcesRva, int ResourcesSize, int StrongNameSignatureRva, int StrongNameSignatureSize, DirectoryEntry ManagedNativeHeader)
```

### ClrHeader(int, int, int, int, CorFlags, int, int, int, int, int)

Constructs a header without a managed native header directory. Preserves the original
ten-argument shape for callers written before [ManagedNativeHeader](/api/dotsider.core.analysis.models.clrheader.managednativeheader/) was added.

**Parameters:**

- `majorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `minorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `metadataRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `metadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `flags` ([CorFlags](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.corflags))
- `entryPointToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `resourcesRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `resourcesSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `strongNameSignatureRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `strongNameSignatureSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public ClrHeader(int majorRuntimeVersion, int minorRuntimeVersion, int metadataRva, int metadataSize, CorFlags flags, int entryPointToken, int resourcesRva, int resourcesSize, int strongNameSignatureRva, int strongNameSignatureSize)
```

## Properties

### EntryPointToken

Metadata token of the entry point method, or zero.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryPointToken { get; init; }
```

### Flags

CLR header flags (ILOnly, 32BitRequired, StrongNameSigned, etc.).

**Returns:** [CorFlags](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.corflags)

```csharp
public CorFlags Flags { get; init; }
```

### MajorRuntimeVersion

Major version of the CLR required.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MajorRuntimeVersion { get; init; }
```

### ManagedNativeHeader

The managed native header directory. Non-empty for precompiled images: a crossgen2 ReadyToRun
image points it at the `READYTORUN_HEADER`. Empty (`Size == 0`) for a plain managed assembly.

**Returns:** [DirectoryEntry](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.directoryentry)

```csharp
public DirectoryEntry ManagedNativeHeader { get; init; }
```

### MetadataRva

RVA of the metadata directory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataRva { get; init; }
```

### MetadataSize

Size of the metadata directory in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataSize { get; init; }
```

### MinorRuntimeVersion

Minor version of the CLR required.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MinorRuntimeVersion { get; init; }
```

### ResourcesRva

RVA of the managed resources directory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ResourcesRva { get; init; }
```

### ResourcesSize

Size of the managed resources directory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ResourcesSize { get; init; }
```

### StrongNameSignatureRva

RVA of the strong name signature.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StrongNameSignatureRva { get; init; }
```

### StrongNameSignatureSize

Size of the strong name signature.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StrongNameSignatureSize { get; init; }
```

## Methods

### Deconstruct(out int, out int, out int, out int, out CorFlags, out int, out int, out int, out int, out int, out DirectoryEntry)

**Parameters:**

- `MajorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MinorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MetadataRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MetadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Flags` ([CorFlags](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.corflags))
- `EntryPointToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ResourcesRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ResourcesSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `StrongNameSignatureRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `StrongNameSignatureSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ManagedNativeHeader` ([DirectoryEntry](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.directoryentry))

```csharp
public void Deconstruct(out int MajorRuntimeVersion, out int MinorRuntimeVersion, out int MetadataRva, out int MetadataSize, out CorFlags Flags, out int EntryPointToken, out int ResourcesRva, out int ResourcesSize, out int StrongNameSignatureRva, out int StrongNameSignatureSize, out DirectoryEntry ManagedNativeHeader)
```

### Deconstruct(out int, out int, out int, out int, out CorFlags, out int, out int, out int, out int, out int)

Deconstructs the original ten fields, preserving the pre-[ManagedNativeHeader](/api/dotsider.core.analysis.models.clrheader.managednativeheader/)
positional shape for existing deconstruction sites.

**Parameters:**

- `majorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `minorRuntimeVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `metadataRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `metadataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `flags` ([CorFlags](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.corflags))
- `entryPointToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `resourcesRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `resourcesSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `strongNameSignatureRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `strongNameSignatureSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out int majorRuntimeVersion, out int minorRuntimeVersion, out int metadataRva, out int metadataSize, out CorFlags flags, out int entryPointToken, out int resourcesRva, out int resourcesSize, out int strongNameSignatureRva, out int strongNameSignatureSize)
```

### Equals(ClrHeader?)

**Parameters:**

- `other` ([ClrHeader](/api/dotsider.core.analysis.models.clrheader/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ClrHeader? other)
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

### operator !=(ClrHeader?, ClrHeader?)

**Parameters:**

- `left` ([ClrHeader](/api/dotsider.core.analysis.models.clrheader/))
- `right` ([ClrHeader](/api/dotsider.core.analysis.models.clrheader/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ClrHeader? left, ClrHeader? right)
```

### operator ==(ClrHeader?, ClrHeader?)

**Parameters:**

- `left` ([ClrHeader](/api/dotsider.core.analysis.models.clrheader/))
- `right` ([ClrHeader](/api/dotsider.core.analysis.models.clrheader/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ClrHeader? left, ClrHeader? right)
```
