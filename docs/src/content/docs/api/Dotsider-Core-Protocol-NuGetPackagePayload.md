---
title: "NuGetPackagePayload"
description: "NuGet package identity and managed payload files. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nugetpackagepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

NuGet package identity and managed payload files.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NuGetPackagePayload : IEquatable<NuGetPackagePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NuGetPackagePayload**

## Implements

- [IEquatable\<NuGetPackagePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NuGetPackagePayload(string?, string?, string?, string?, IReadOnlyList\<NuGetFileEntry\>)

NuGet package identity and managed payload files.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `PackageId` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PackageVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Authors` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Description` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DllFiles` ([IReadOnlyList\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public NuGetPackagePayload(string? PackageId, string? PackageVersion, string? Authors, string? Description, IReadOnlyList<NuGetFileEntry> DllFiles)
```

## Properties

### Authors

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Authors { get; init; }
```

### Description

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Description { get; init; }
```

### DllFiles

**Returns:** [IReadOnlyList\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NuGetFileEntry> DllFiles { get; init; }
```

### PackageId

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageId { get; init; }
```

### PackageVersion

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PackageVersion { get; init; }
```

## Methods

### Deconstruct(out string?, out string?, out string?, out string?, out IReadOnlyList\<NuGetFileEntry\>)

**Parameters:**

- `PackageId` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PackageVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Authors` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Description` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DllFiles` ([IReadOnlyList\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string? PackageId, out string? PackageVersion, out string? Authors, out string? Description, out IReadOnlyList<NuGetFileEntry> DllFiles)
```

### Equals(NuGetPackagePayload?)

**Parameters:**

- `other` ([NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NuGetPackagePayload? other)
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

### operator !=(NuGetPackagePayload?, NuGetPackagePayload?)

**Parameters:**

- `left` ([NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/))
- `right` ([NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NuGetPackagePayload? left, NuGetPackagePayload? right)
```

### operator ==(NuGetPackagePayload?, NuGetPackagePayload?)

**Parameters:**

- `left` ([NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/))
- `right` ([NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NuGetPackagePayload? left, NuGetPackagePayload? right)
```
