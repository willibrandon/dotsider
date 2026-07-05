---
title: "ReadyToRunComponent"
description: "One component assembly of a composite ReadyToRun image, from the ComponentAssemblies section joined to the manifest and its MVIDs. Its native code lives in the composite; its metadata is resolved from a sibling assembly matched by name and MVID."
slug: api/dotsider.core.analysis.models.readytoruncomponent
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One component assembly of a composite ReadyToRun image, from the `ComponentAssemblies`
section joined to the manifest and its MVIDs. Its native code lives in the composite; its
metadata is resolved from a sibling assembly matched by name and MVID.

```csharp
public sealed record ReadyToRunComponent : IEquatable<ReadyToRunComponent>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunComponent**

## Implements

- [IEquatable\<ReadyToRunComponent\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunComponent(string, Guid, int, int, string?, bool)

One component assembly of a composite ReadyToRun image, from the `ComponentAssemblies`
section joined to the manifest and its MVIDs. Its native code lives in the composite; its
metadata is resolved from a sibling assembly matched by name and MVID.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The component's simple assembly name from the manifest.
- `Mvid` ([Guid](https://learn.microsoft.com/dotnet/api/system.guid)): The component's module version id, used to validate the resolved sibling's identity.
- `CorHeaderRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA of the component's embedded COR header, or 0 when not embedded.
- `CoreHeaderRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA of the component's per-assembly ReadyToRun core header.
- `ResolvedPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The sibling assembly file whose MVID matched, or null when unresolved.
- `MetadataAvailable` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the component's metadata was resolved (name + MVID matched a sibling).

```csharp
public ReadyToRunComponent(string AssemblyName, Guid Mvid, int CorHeaderRva, int CoreHeaderRva, string? ResolvedPath, bool MetadataAvailable)
```

## Properties

### AssemblyName

The component's simple assembly name from the manifest.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### CoreHeaderRva

The RVA of the component's per-assembly ReadyToRun core header.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int CoreHeaderRva { get; init; }
```

### CorHeaderRva

The RVA of the component's embedded COR header, or 0 when not embedded.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int CorHeaderRva { get; init; }
```

### MetadataAvailable

Whether the component's metadata was resolved (name + MVID matched a sibling).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool MetadataAvailable { get; init; }
```

### Mvid

The component's module version id, used to validate the resolved sibling's identity.

**Returns:** [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

```csharp
public Guid Mvid { get; init; }
```

### ResolvedPath

The sibling assembly file whose MVID matched, or null when unresolved.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ResolvedPath { get; init; }
```

