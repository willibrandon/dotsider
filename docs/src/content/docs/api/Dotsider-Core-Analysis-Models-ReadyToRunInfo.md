---
title: "ReadyToRunInfo"
description: "The parsed facts about a PE image's crossgen2 ReadyToRun header. Present whenever an image claims to be ReadyToRun (a managed native header directory or an RTR_HEADER export); Status says whether it is usable, so a corrupt or unsupported image surfaces its diagnostic rather than masquerading as plain managed."
slug: api/dotsider.core.analysis.models.readytoruninfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The parsed facts about a PE image's crossgen2 ReadyToRun header. Present whenever an image
claims to be ReadyToRun (a managed native header directory or an `RTR_HEADER` export);
[Status](/api/dotsider.core.analysis.models.readytoruninfo.status/) says whether it is usable, so a corrupt or unsupported image surfaces
its diagnostic rather than masquerading as plain managed.

```csharp
public sealed record ReadyToRunInfo : IEquatable<ReadyToRunInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunInfo**

## Implements

- [IEquatable\<ReadyToRunInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunInfo(uint, int, int, uint, bool, bool, bool, int, int, ReadyToRunStatus, string?, NativeArchitecture, string?, IReadOnlyList\<ReadyToRunSectionEntry\>, IReadOnlyList\<ReadyToRunComponent\>, int, int)

The parsed facts about a PE image's crossgen2 ReadyToRun header. Present whenever an image
claims to be ReadyToRun (a managed native header directory or an `RTR_HEADER` export);
[Status](/api/dotsider.core.analysis.models.readytoruninfo.status/) says whether it is usable, so a corrupt or unsupported image surfaces
its diagnostic rather than masquerading as plain managed.

**Parameters:**

- `Signature` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The header signature dword (expected `0x00525452`, "RTR\0").
- `MajorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The ReadyToRun major version.
- `MinorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The ReadyToRun minor version.
- `Flags` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The raw `ReadyToRunFlags` bitmask.
- `IsComposite` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this image is a composite (its native code covers several component assemblies).
- `IsComponent` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this image is a composite component (`READYTORUN_FLAG_COMPONENT`).
- `IsPartialImage` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether not every method is precompiled (`READYTORUN_FLAG_PARTIAL`) — a coverage flag, distinct from [Status](/api/dotsider.core.analysis.models.readytoruninfo.status/).
- `HeaderRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA the header was located at.
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of rows in the section table.
- `Status` ([ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)): The parse status.
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable explanation when the status is not [Valid](/api/dotsider.core.analysis.models.readytorunstatus.valid/), otherwise null.
- `Architecture` ([NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)): The image's real machine architecture (report-only when it has no disassembler).
- `OwnerCompositeExecutable` ([String](https://learn.microsoft.com/dotnet/api/system.string)): For a component image, the filename of the composite that holds its native code, otherwise null.
- `Sections` ([IReadOnlyList\<ReadyToRunSectionEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The section table rows.
- `Components` ([IReadOnlyList\<ReadyToRunComponent\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The composite component assemblies, empty for a non-composite image.
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of MethodDef entry points.
- `InstanceMethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of instantiated-generic entry points.

```csharp
public ReadyToRunInfo(uint Signature, int MajorVersion, int MinorVersion, uint Flags, bool IsComposite, bool IsComponent, bool IsPartialImage, int HeaderRva, int SectionCount, ReadyToRunStatus Status, string? Diagnostic, NativeArchitecture Architecture, string? OwnerCompositeExecutable, IReadOnlyList<ReadyToRunSectionEntry> Sections, IReadOnlyList<ReadyToRunComponent> Components, int MethodCount, int InstanceMethodCount)
```

## Properties

### Architecture

The image's real machine architecture (report-only when it has no disassembler).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
public NativeArchitecture Architecture { get; init; }
```

### Components

The composite component assemblies, empty for a non-composite image.

**Returns:** [IReadOnlyList\<ReadyToRunComponent\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunComponent> Components { get; init; }
```

### Diagnostic

A human-readable explanation when the status is not [Valid](/api/dotsider.core.analysis.models.readytorunstatus.valid/), otherwise null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Diagnostic { get; init; }
```

### Flags

The raw `ReadyToRunFlags` bitmask.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Flags { get; init; }
```

### HeaderRva

The RVA the header was located at.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int HeaderRva { get; init; }
```

### InstanceMethodCount

The number of instantiated-generic entry points.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int InstanceMethodCount { get; init; }
```

### IsComponent

Whether this image is a composite component (`READYTORUN_FLAG_COMPONENT`).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsComponent { get; init; }
```

### IsComposite

Whether this image is a composite (its native code covers several component assemblies).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsComposite { get; init; }
```

### IsPartialImage

Whether not every method is precompiled (`READYTORUN_FLAG_PARTIAL`) — a coverage flag, distinct from [Status](/api/dotsider.core.analysis.models.readytoruninfo.status/).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsPartialImage { get; init; }
```

### MajorVersion

The ReadyToRun major version.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MajorVersion { get; init; }
```

### MethodCount

The number of MethodDef entry points.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodCount { get; init; }
```

### MinorVersion

The ReadyToRun minor version.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MinorVersion { get; init; }
```

### OwnerCompositeExecutable

For a component image, the filename of the composite that holds its native code, otherwise null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? OwnerCompositeExecutable { get; init; }
```

### SectionCount

The number of rows in the section table.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

### Sections

The section table rows.

**Returns:** [IReadOnlyList\<ReadyToRunSectionEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunSectionEntry> Sections { get; init; }
```

### Signature

The header signature dword (expected `0x00525452`, "RTR\0").

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Signature { get; init; }
```

### Status

The parse status.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
public ReadyToRunStatus Status { get; init; }
```

