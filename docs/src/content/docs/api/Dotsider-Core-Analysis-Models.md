---
title: "Dotsider.Core.Analysis.Models"
slug: api/dotsider.core.analysis.models
sidebar:
  order: 1
---

## Classes

### [AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/)

The complete diff result between two assemblies.

```csharp
public sealed record AssemblyDiffResult : IEquatable<AssemblyDiffResult>
```

### [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/)

The result of opening an assembly file via [AssemblyLoader](/api/dotsider.core.analysis.assemblyloader/),
distinguishing between direct loads, apphost companion redirects, and
single-file bundle entry extractions.

```csharp
public abstract record AssemblyOpenResult : IEquatable<AssemblyOpenResult>
```

### [AssemblyOpenResult.ApphostWithCompanion](/api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion/)

The file is a native apphost with a companion managed .dll on disk.
The caller decides when to redirect (e.g. showing a dialog first).

```csharp
public sealed record AssemblyOpenResult.ApphostWithCompanion : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.ApphostWithCompanion>
```

### [AssemblyOpenResult.BundleEntry](/api/dotsider.core.analysis.models.assemblyopenresult.bundleentry/)

The file is a single-file bundle. The entry assembly has been extracted
from the bundle and is ready for analysis.

```csharp
public sealed record AssemblyOpenResult.BundleEntry : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.BundleEntry>
```

### [AssemblyOpenResult.Direct](/api/dotsider.core.analysis.models.assemblyopenresult.direct/)

Direct load — the file is a .dll or .exe with metadata, or a native binary
with no metadata (NativeAOT, unknown format).

```csharp
public sealed record AssemblyOpenResult.Direct : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.Direct>
```

### [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

Information about a referenced assembly from the AssemblyRef metadata table.

```csharp
public sealed record AssemblyRefInfo : IEquatable<AssemblyRefInfo>
```

### [BundleEntry](/api/dotsider.core.analysis.models.bundleentry/)

Describes a single file entry within a .NET single-file bundle.

```csharp
public sealed record BundleEntry : IEquatable<BundleEntry>
```

### [BundleManifest](/api/dotsider.core.analysis.models.bundlemanifest/)

The parsed manifest header of a .NET single-file bundle.

```csharp
public sealed record BundleManifest : IEquatable<BundleManifest>
```

### [ClrHeader](/api/dotsider.core.analysis.models.clrheader/)

CLR (Common Language Runtime) header information from the PE file's COR20 header.

```csharp
public sealed record ClrHeader : IEquatable<ClrHeader>
```

### [CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/)

A snapshot of runtime performance counters at a point in time.

```csharp
public sealed record CounterSnapshot : IEquatable<CounterSnapshot>
```

### [CustomAttributeInfo](/api/dotsider.core.analysis.models.customattributeinfo/)

Information about a custom attribute applied to a metadata entity.

```csharp
public sealed record CustomAttributeInfo : IEquatable<CustomAttributeInfo>
```

### [DiffEntry\<T\>](/api/dotsider.core.analysis.models.diffentry-1/)

A single diff entry wrapping an item from either side.

```csharp
public sealed record DiffEntry<T> : IEquatable<DiffEntry<T>>
```

### [DiffSummary](/api/dotsider.core.analysis.models.diffsummary/)

Summary statistics for the diff.

```csharp
public sealed record DiffSummary : IEquatable<DiffSummary>
```

### [FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/)

Information about a field defined in the assembly's FieldDef metadata table.

```csharp
public sealed record FieldDefInfo : IEquatable<FieldDefInfo>
```

### [GraphEdge](/api/dotsider.core.analysis.models.graphedge/)

An edge connecting two nodes in the dependency graph.

```csharp
public sealed record GraphEdge : IEquatable<GraphEdge>
```

### [GraphNode](/api/dotsider.core.analysis.models.graphnode/)

A node in the assembly dependency graph.

```csharp
public sealed record GraphNode : IEquatable<GraphNode>
```

### [IlInstruction](/api/dotsider.core.analysis.models.ilinstruction/)

A single decoded IL (Intermediate Language) instruction.

```csharp
public sealed record IlInstruction : IEquatable<IlInstruction>
```

### [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/)

Represents the resolved target of an IL code navigation (go-to-definition) action.

```csharp
public abstract record IlNavigationTarget : IEquatable<IlNavigationTarget>
```

### [IlNavigationTarget.ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/)

A field in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalField : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalField>
```

### [IlNavigationTarget.ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/)

A method in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalMethod : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalMethod>
```

### [IlNavigationTarget.ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/)

A type in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalType : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalType>
```

### [IlNavigationTarget.GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/)

A generic instantiation (TypeSpec or MethodSpec) that cannot be navigated directly.

```csharp
public sealed record IlNavigationTarget.GenericInstantiation : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.GenericInstantiation>
```

### [IlNavigationTarget.LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/)

A field defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalField : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalField>
```

### [IlNavigationTarget.LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/)

A method defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalMethod : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalMethod>
```

### [IlNavigationTarget.LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/)

A type defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalType : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalType>
```

### [IlNavigationTarget.Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/)

A token that could not be resolved to any known target.

```csharp
public sealed record IlNavigationTarget.Unresolved : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unresolved>
```

### [IlNavigationTarget.Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/)

A token kind that is recognized but not supported for navigation.

```csharp
public sealed record IlNavigationTarget.Unsupported : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unsupported>
```

### [MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/)

Information about a referenced member (method or field) from the MemberRef metadata table.

```csharp
public sealed record MemberRefInfo : IEquatable<MemberRefInfo>
```

### [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

Information about a method defined in the assembly's MethodDef metadata table.

```csharp
public sealed record MethodDefInfo : IEquatable<MethodDefInfo>
```

### [NuGetFileEntry](/api/dotsider.core.analysis.models.nugetfileentry/)

Represents a file entry within a NuGet package (.nupkg).

```csharp
public sealed record NuGetFileEntry : IEquatable<NuGetFileEntry>
```

### [OutputLine](/api/dotsider.core.analysis.models.outputline/)

A line of output captured from the traced process's stdout or stderr.

```csharp
public sealed record OutputLine : IEquatable<OutputLine>
```

### [PeHeaders](/api/dotsider.core.analysis.models.peheaders/)

Aggregated PE header information for a .NET assembly.

```csharp
public sealed record PeHeaders : IEquatable<PeHeaders>
```

### [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

The result of resolving an assembly reference — either a file on disk or bytes from a bundle.

```csharp
public abstract record ResolvedAssembly : IEquatable<ResolvedAssembly>
```

### [ResolvedAssembly.FromBundle](/api/dotsider.core.analysis.models.resolvedassembly.frombundle/)

The assembly was found inside a single-file bundle.

```csharp
public sealed record ResolvedAssembly.FromBundle : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromBundle>
```

### [ResolvedAssembly.FromFile](/api/dotsider.core.analysis.models.resolvedassembly.fromfile/)

The assembly was found as a file on disk.

```csharp
public sealed record ResolvedAssembly.FromFile : ResolvedAssembly, IEquatable<ResolvedAssembly>, IEquatable<ResolvedAssembly.FromFile>
```

### [ResourceInfo](/api/dotsider.core.analysis.models.resourceinfo/)

Information about a managed resource embedded in the assembly.

```csharp
public sealed record ResourceInfo : IEquatable<ResourceInfo>
```

### [SectionInfo](/api/dotsider.core.analysis.models.sectioninfo/)

Information about a single PE section (e.g., .text, .rsrc, .reloc).

```csharp
public sealed record SectionInfo : IEquatable<SectionInfo>
```

### [SizeNode](/api/dotsider.core.analysis.models.sizenode/)

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method.

```csharp
public sealed record SizeNode : IEquatable<SizeNode>
```

### [StringEntry](/api/dotsider.core.analysis.models.stringentry/)

A string extracted from the assembly, along with its source and offset.

```csharp
public sealed record StringEntry : IEquatable<StringEntry>
```

### [TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/)

A single traced runtime event captured from the EventPipe session.

```csharp
public sealed record TraceEventEntry : IEquatable<TraceEventEntry>
```

### [TraceSummary](/api/dotsider.core.analysis.models.tracesummary/)

Summary statistics aggregated from all collected trace events.

```csharp
public sealed record TraceSummary : IEquatable<TraceSummary>
```

### [TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)

Information about a type defined in the assembly's TypeDef metadata table.

```csharp
public sealed record TypeDefInfo : IEquatable<TypeDefInfo>
```

### [TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/)

Information about a referenced type from the TypeRef metadata table.

```csharp
public sealed record TypeRefInfo : IEquatable<TypeRefInfo>
```

## Enums

### [BundleFileType](/api/dotsider.core.analysis.models.bundlefiletype/)

Identifies the type of file embedded in a .NET single-file bundle.

```csharp
public enum BundleFileType : byte
```

### [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

Describes the kind of difference detected between two assembly elements.

```csharp
public enum DiffKind
```

### [MemberRefKind](/api/dotsider.core.analysis.models.memberrefkind/)

Distinguishes whether a MemberRef entry refers to a method or a field.

```csharp
public enum MemberRefKind
```

### [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

The granularity level of a [SizeNode](/api/dotsider.core.analysis.models.sizenode/) in the size breakdown tree.

```csharp
public enum SizeNodeKind
```

### [StringSource](/api/dotsider.core.analysis.models.stringsource/)

Identifies the source from which a string was extracted.

```csharp
public enum StringSource
```

### [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

Category of a traced runtime event, used for coloring in the events table.

```csharp
public enum TraceEventCategory
```

### [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

Current state of the traced process lifecycle.

```csharp
public enum TraceProcessState
```

