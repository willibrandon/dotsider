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

### ClrHeader(int, int, int, int, CorFlags, int, int, int, int, int)

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

```csharp
public ClrHeader(int MajorRuntimeVersion, int MinorRuntimeVersion, int MetadataRva, int MetadataSize, CorFlags Flags, int EntryPointToken, int ResourcesRva, int ResourcesSize, int StrongNameSignatureRva, int StrongNameSignatureSize)
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

