---
title: "Dotsider.Core.Analysis.Models"
slug: api/dotsider.core.analysis.models
sidebar:
  order: 2
---

## Classes

### [AppliedPolicy](/api/dotsider.core.analysis.models.appliedpolicy/)

Records that a requested identity was rewritten by .NET Framework binding policy. Carried
on [AppliedPolicy](/api/dotsider.core.analysis.models.graphnavigationcontext.appliedpolicy/) so the UI can render
"↪ redirected 1.0.0.0 → 13.0.0.0 via app.config" without inventing new
[AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/) values for redirected hits — a redirect-applied AppLocal
hit is still [AppLocal](/api/dotsider.core.analysis.models.assemblyprovenance.applocal/), just with this annotation attached.

```csharp
public sealed record AppliedPolicy : IEquatable<AppliedPolicy>
```

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
with no metadata and no ReadyToRun header (unknown format).

```csharp
public sealed record AssemblyOpenResult.Direct : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.Direct>
```

### [AssemblyOpenResult.NativeAot](/api/dotsider.core.analysis.models.assemblyopenresult.nativeaot/)

The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
with no COR header whose image embeds a validated ReadyToRun header. No
metadata is available, but PE structure, native import/export/load-config
directories, and raw strings are.

```csharp
public sealed record AssemblyOpenResult.NativeAot : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.NativeAot>
```

### [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

Information about a referenced assembly from the AssemblyRef metadata table.

```csharp
public sealed record AssemblyRefInfo : IEquatable<AssemblyRefInfo>
```

### [AssemblyResolution](/api/dotsider.core.analysis.models.assemblyresolution/)

Outcome of an identity-based assembly resolution. Carries everything the dependency-graph
builder and UI need: the resolved file/bundle (or null on failure), the
provenance classifying how the file was located, the candidate path of an identity-mismatched
simple-name hit, and — for .NET Framework binds — the policy-layer attribution and the
effective bound identity.

```csharp
public sealed record AssemblyResolution : IEquatable<AssemblyResolution>
```

### [BindingPolicy](/api/dotsider.core.analysis.models.bindingpolicy/)

Aggregated .NET Framework binding policy assembled from framework unification, machine.config,
publisher-policy assemblies, and the application configuration file. Layers are stored in
document order with first-match semantics — the same model the CLR applies — and later layers
(machine.config &gt; publisher &gt; app &gt; framework unification) override earlier ones when
they target the same identity.

```csharp
public sealed record BindingPolicy : IEquatable<BindingPolicy>
```

### [BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/)

Output of NetFxRuntimeVersion): the redirects, codeBase entries,
per-identity publisher-policy disablements, probing privatePath segments, and the
runtime-scoped publisher-policy bypass flag found in a single configuration file.

```csharp
public sealed record BindingPolicyParseResult : IEquatable<BindingPolicyParseResult>
```

### [BindingRedirect](/api/dotsider.core.analysis.models.bindingredirect/)

One `&lt;bindingRedirect&gt;` entry parsed from a .NET Framework configuration file
or a publisher-policy assembly's embedded XML resource.

```csharp
public sealed record BindingRedirect : IEquatable<BindingRedirect>
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

### [CodeBaseEntry](/api/dotsider.core.analysis.models.codebaseentry/)

One `&lt;codeBase&gt;` entry parsed from a .NET Framework configuration file or
publisher-policy assembly. CodeBase entries are honored only for strong-named binds at
the version specified.

```csharp
public sealed record CodeBaseEntry : IEquatable<CodeBaseEntry>
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

### [DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/)

Display-ready PE debug directory entry information.

```csharp
public sealed record DebugDirectoryInfo : IEquatable<DebugDirectoryInfo>
```

### [DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/)

The result of building a transitive assembly dependency graph. Contains the public topology
consumed by serializers ([Nodes](/api/dotsider.core.analysis.models.dependencygraphresult.nodes/), [Edges](/api/dotsider.core.analysis.models.dependencygraphresult.edges/)) and the internal navigation
metadata consumed by the TUI ([NavigationById](/api/dotsider.core.analysis.models.dependencygraphresult.navigationbyid/)).

```csharp
public sealed record DependencyGraphResult : IEquatable<DependencyGraphResult>
```

### [DgmlGraph](/api/dotsider.core.analysis.models.dgmlgraph/)

An ILC dependency graph read from a DGML file, with the reverse index needed to answer
"why is this in my binary": a breadth-first walk from any node toward its dependers ends
at a root — a node nothing depends on — and the chain back down is the explanation.

```csharp
public sealed class DgmlGraph
```

### [DgmlLink](/api/dotsider.core.analysis.models.dgmllink/)

One edge of an ILC dependency graph: the source node depends on the target node, so the
target is in the binary because the source needed it.

```csharp
public sealed record DgmlLink : IEquatable<DgmlLink>
```

### [DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/)

One node of an ILC dependency graph. The label is the compiler's node name — the same
string an mstat size entry stores as its `NodeName`, which is how the two files join.

```csharp
public sealed record DgmlNode : IEquatable<DgmlNode>
```

### [DgmlPathStep](/api/dotsider.core.analysis.models.dgmlpathstep/)

One step of a root-to-node dependency chain — the answer to "why is this in my binary,"
read top-down: the root kept the second step, which kept the third, and so on to the node
that was asked about.

```csharp
public sealed record DgmlPathStep : IEquatable<DgmlPathStep>
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

### [EmbeddedSourceInfo](/api/dotsider.core.analysis.models.embeddedsourceinfo/)

Embedded source decoded from a portable PDB document.

```csharp
public sealed record EmbeddedSourceInfo : IEquatable<EmbeddedSourceInfo>
```

### [ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/)

A single entry in the PE export table.

```csharp
public sealed record ExportedFunctionInfo : IEquatable<ExportedFunctionInfo>
```

### [FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/)

Information about a field defined in the assembly's FieldDef metadata table.

```csharp
public sealed record FieldDefInfo : IEquatable<FieldDefInfo>
```

### [GraphEdge](/api/dotsider.core.analysis.models.graphedge/)

A directed edge from a referencing assembly to a referenced assembly in the transitive
dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen
target identity emits a new edge but does not re-expand the target's subtree.

```csharp
public sealed record GraphEdge : IEquatable<GraphEdge>
```

### [GraphNavigationContext](/api/dotsider.core.analysis.models.graphnavigationcontext/)

Internal per-node metadata describing how a dependency graph node was resolved and the
context under which it was reached. Used by the TUI for Enter-to-open navigation and
framework filtering. Never serialized — this data must not leak through CLI, diagnostics,
or MCP surfaces that publish graph topology.

```csharp
public sealed record GraphNavigationContext : IEquatable<GraphNavigationContext>
```

### [GraphNode](/api/dotsider.core.analysis.models.graphnode/)

A node in the transitive assembly dependency graph. Topology only — layout coordinates
and rendered labels are the responsibility of the view layer, which projects the visible
subgraph into a separate render model so filters and viewport changes rebalance without
perturbing this record.

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

A MethodSpec whose metadata could not be decoded into a navigable target.

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

### [ImportedFunctionInfo](/api/dotsider.core.analysis.models.importedfunctioninfo/)

A single function imported from a native module.

```csharp
public sealed record ImportedFunctionInfo : IEquatable<ImportedFunctionInfo>
```

### [ImportedModuleInfo](/api/dotsider.core.analysis.models.importedmoduleinfo/)

A native module referenced by the PE import table, with the functions imported from it.

```csharp
public sealed record ImportedModuleInfo : IEquatable<ImportedModuleInfo>
```

### [LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/)

Parsed PE load configuration directory. Pointer-width fields are widened to
[UInt64](https://learn.microsoft.com/dotnet/api/system.uint64) so a single record covers PE32 and PE32+ images. Fields
beyond the directory's declared size are zero — real-world load configs are
truncated at many historical lengths.

```csharp
public sealed record LoadConfigInfo : IEquatable<LoadConfigInfo>
```

### [LoadedAssemblyEntry](/api/dotsider.core.analysis.models.loadedassemblyentry/)

Per-loaded-identity entry interned in `LoadedAssemblyCache`. When two distinct requested
identities redirect to the same loaded identity, both [Loaded](/api/dotsider.core.analysis.models.netfxbindresult.loaded/)
values reference-equal this single entry, faithfully modeling the CLR's "already loaded"
reuse: only one filesystem read per loaded identity.

```csharp
public sealed record LoadedAssemblyEntry : IEquatable<LoadedAssemblyEntry>
```

### [LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/)

A PDB local variable slot and the IL range where its name is active.

```csharp
public sealed record LocalSlotInfo : IEquatable<LocalSlotInfo>
```

### [MemberRefInfo](/api/dotsider.core.analysis.models.memberrefinfo/)

Information about a referenced member (method or field) from the MemberRef metadata table.

```csharp
public sealed record MemberRefInfo : IEquatable<MemberRefInfo>
```

### [MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/)

Portable PDB debug information for a method.

```csharp
public sealed record MethodDebugInfo : IEquatable<MethodDebugInfo>
```

### [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

Information about a method defined in the assembly's MethodDef metadata table.

```csharp
public sealed record MethodDefInfo : IEquatable<MethodDefInfo>
```

### [MstatBlob](/api/dotsider.core.analysis.models.mstatblob/)

One named global data region from an ILC size report — embedded metadata, hydration
tables, dispatch maps, and the like. Blob names come from the compiler's node type names
(for example `Metadata` or `InterfaceDispatchMap`), with same-named regions
summed into one entry.

```csharp
public sealed record MstatBlob : IEquatable<MstatBlob>
```

### [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

The contents of an ILC size report (`.mstat`), produced by publishing a Native AOT
project with `IlcGenerateMstatFile`. The file is itself a valid ECMA-335 assembly whose
assembly version carries the format version and whose data lives in IL streams; this record
is the decoded result. Sections absent from older format versions are empty lists.

```csharp
public sealed record MstatData : IEquatable<MstatData>
```

### [MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/)

One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single
body and pointed these identical methods at it, so only the original contributes size.

```csharp
public sealed record MstatDeduplicatedMethod : IEquatable<MstatDeduplicatedMethod>
```

### [MstatFrozenObject](/api/dotsider.core.analysis.models.mstatfrozenobject/)

One frozen object from an ILC size report (format 2.1+) — an object allocated at compile
time and baked into the image, most commonly a string literal. For back-compat these bytes
are also summed into the `ArrayOfFrozenObjects` blob entry.

```csharp
public sealed record MstatFrozenObject : IEquatable<MstatFrozenObject>
```

### [MstatManifestResource](/api/dotsider.core.analysis.models.mstatmanifestresource/)

One embedded manifest resource from an ILC size report (format 2.1+). For back-compat
these bytes are also summed into the `ResourceData` blob entry.

```csharp
public sealed record MstatManifestResource : IEquatable<MstatManifestResource>
```

### [MstatMethod](/api/dotsider.core.analysis.models.mstatmethod/)

One compiled method body from an ILC size report. Sizes are bytes of native artifact, not
IL: ILC compiles each body once, so the sum over all methods is the code contribution to
the binary.

```csharp
public sealed record MstatMethod : IEquatable<MstatMethod>
```

### [MstatRvaField](/api/dotsider.core.analysis.models.mstatrvafield/)

One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
field mapped directly into the image, typically compiler-generated arrays behind
collection expressions and `ReadOnlySpan` literals. For back-compat these bytes are
also summed into the `FieldRvaData` blob entry.

```csharp
public sealed record MstatRvaField : IEquatable<MstatRvaField>
```

### [MstatType](/api/dotsider.core.analysis.models.mstattype/)

One constructed type from an ILC size report. The size is the type's MethodTable data —
the runtime type structure — not the code of its methods, which is reported per method.

```csharp
public sealed record MstatType : IEquatable<MstatType>
```

### [NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/)

Facts extracted from the embedded ReadyToRun header of a Native AOT binary.
Every Native AOT image embeds this header (signature "RTR\0") so the runtime can
locate its module sections; its presence with no COR header identifies the binary
as Native AOT compiled .NET.

```csharp
public sealed record NativeAotInfo : IEquatable<NativeAotInfo>
```

### [NativeInstruction](/api/dotsider.core.analysis.models.nativeinstruction/)

One decoded native instruction. The model is structured — bytes, structured operands, flow and
target metadata, and source attribution — so navigation, JSON/MCP output, syntax decoration,
and future diffing read facts rather than parse text. [OperandText](/api/dotsider.core.analysis.models.nativeinstruction.operandtext/) and the
rendered listing line are projections; [Address](/api/dotsider.core.analysis.models.nativeinstruction.address/) is the semantic key and
[DisplayLine](/api/dotsider.core.analysis.models.nativeinstruction.displayline/) the presentation key, mirroring
[IlInstruction](/api/dotsider.core.analysis.models.ilinstruction/) for the shared IL-Inspector plumbing.

```csharp
public sealed record NativeInstruction : IEquatable<NativeInstruction>
```

### [NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/)

One decoded operand of a [NativeInstruction](/api/dotsider.core.analysis.models.nativeinstruction/), carried structurally so navigation,
JSON/MCP output, syntax decoration, and future diffing never parse the rendered text. The
[Text](/api/dotsider.core.analysis.models.nativeoperand.text/) is the display projection; the typed fields describe what it renders.

```csharp
public sealed record NativeOperand : IEquatable<NativeOperand>
```

### [NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/)

One address→source mapping row recovered from a native sidecar (PDB C13 line table, DWARF/dSYM
line program): the virtual address a source line begins at, its byte length, and the file and
1-based line number.

```csharp
public sealed record NativeSourceLine : IEquatable<NativeSourceLine>
```

### [NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/)

An address-sorted map from virtual address to source file and line, aggregated from a native
binary's debug sidecar. Int32%40) resolves an instruction address to its source
location the way NativeSymbol%40) resolves an address to a symbol,
letting the disassembler annotate the listing with `// file:line` where the sidecar has data.

```csharp
public sealed record NativeSourceMap : IEquatable<NativeSourceMap>
```

### [NativeSymbol](/api/dotsider.core.analysis.models.nativesymbol/)

One native symbol recovered from a binary: a function, a compiler-generated data blob, or a
nameless boundary. The address is carried in every form a consumer might need — virtual
address for display and cross-symbol ordering, PE RVA, file offset when the address is
file-backed, and the containing section — so the UI, hex views, and disassembly never have
to recompute a mapping.

```csharp
public sealed record NativeSymbol : IEquatable<NativeSymbol>
```

### [NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/)

The native symbols recovered from a binary, plus the provenance and status needed to explain
the result. Symbols are ordered by [VirtualAddress](/api/dotsider.core.analysis.models.nativesymbol.virtualaddress/), which
NativeSymbol%40) relies on to resolve an address to its containing symbol —
the lookup the disassembly and hex views use to name code.

```csharp
public sealed record NativeSymbolInfo : IEquatable<NativeSymbolInfo>
```

### [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)

Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
analyzed root via [AssemblyAnalyzer)](/api/dotsider.core.analysis.models.netfxbindingcontext.trybuild(dotsider.core.analysis.assemblyanalyzer)/); carried alongside the analyzer through every
resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
so that every code path produces the same answer for any .NET Framework reference.

```csharp
public sealed record NetFxBindingContext : IEquatable<NetFxBindingContext>
```

### [NetFxBindResult](/api/dotsider.core.analysis.models.netfxbindresult/)

Result of a single .NET Framework bind. Carries the requested identity, the effective identity
after policy was applied, the loaded identity (when binding succeeded), the file path the CLR
would load, the provenance classification, the policy-layer attribution, and (when binding
failed) a human-readable reason for UI surfacing.

```csharp
public sealed record NetFxBindResult : IEquatable<NetFxBindResult>
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

### [PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)

Describes where portable PDB information was found, or why it could not be used.

```csharp
public sealed record PdbProvenance : IEquatable<PdbProvenance>
```

### [PeHeaders](/api/dotsider.core.analysis.models.peheaders/)

Aggregated PE header information for a .NET assembly.

```csharp
public sealed record PeHeaders : IEquatable<PeHeaders>
```

### [RecoveredType](/api/dotsider.core.analysis.models.recoveredtype/)

A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
binary's own types and methods, so a stripped binary can describe itself.

```csharp
public sealed record RecoveredType : IEquatable<RecoveredType>
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

### [RtrSection](/api/dotsider.core.analysis.models.rtrsection/)

One entry in a Native AOT binary's ReadyToRun section table. Each section describes a
runtime data region — frozen objects, GC statics, dehydrated data, or a readonly blob
such as the embedded metadata — the way an ECMA-335 table describes a managed assembly.

```csharp
public sealed record RtrSection : IEquatable<RtrSection>
```

### [SectionInfo](/api/dotsider.core.analysis.models.sectioninfo/)

Information about a single PE section (e.g., .text, .rsrc, .reloc).

```csharp
public sealed record SectionInfo : IEquatable<SectionInfo>
```

### [SequencePointInfo](/api/dotsider.core.analysis.models.sequencepointinfo/)

A source sequence point decoded from a portable PDB.

```csharp
public sealed record SequencePointInfo : IEquatable<SequencePointInfo>
```

### [SizeNode](/api/dotsider.core.analysis.models.sizenode/)

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method — or,
for Native AOT trees, a data category and its entries.

```csharp
public sealed record SizeNode : IEquatable<SizeNode>
```

### [SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/)

Source Link mappings decoded from portable PDB custom debug information.

```csharp
public sealed record SourceLinkInfo : IEquatable<SourceLinkInfo>
```

### [SourceLinkMapping](/api/dotsider.core.analysis.models.sourcelinkmapping/)

A single Source Link document mapping.

```csharp
public sealed record SourceLinkMapping : IEquatable<SourceLinkMapping>
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

## Structs

### [NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/)

The column ranges of the mnemonic, operands, and target within a rendered disassembly line,
set by [NativeDisassembler](/api/dotsider.core.analysis.disasm.nativedisassembler/)'s text formatter. The
TUI decoration providers highlight and hit-test by these spans rather than re-parsing the line,
so the rendered text stays a pure projection of the structured instruction.

```csharp
public readonly record struct NativeLineLayout : IEquatable<NativeLineLayout>
```

### [NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/)

A resolved reference to the symbol containing a target address, with the offset into it — so a
call or branch landing inside a function displays honestly as `Foo+0x12` rather than
failing or pretending an exact hit.

```csharp
public readonly record struct NativeSymbolRef : IEquatable<NativeSymbolRef>
```

## Enums

### [AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)

Describes how an assembly in the dependency graph was located — or why it could not be.

```csharp
public enum AssemblyProvenance
```

### [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

Coarse classification of an analyzed binary.

```csharp
public enum BinaryKind
```

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

### [GraphNodeKind](/api/dotsider.core.analysis.models.graphnodekind/)

What a dependency-graph node represents. Managed graphs contain only assemblies; the
Native AOT graph adds the binary's native import modules.

```csharp
public enum GraphNodeKind
```

### [MemberRefKind](/api/dotsider.core.analysis.models.memberrefkind/)

Distinguishes whether a MemberRef entry refers to a method or a field.

```csharp
public enum MemberRefKind
```

### [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

The instruction-set architecture a native code window is decoded as. Carried on
[NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/) from the real image (or the selected fat-Mach-O slice) so the
disassembler never has to guess from an ambiguous machine string.

```csharp
public enum NativeArchitecture
```

### [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

How a decoded instruction affects control flow. Drives listing navigation (which instructions
carry a jumpable target) and future analysis without re-parsing the mnemonic.

```csharp
public enum NativeFlowKind
```

### [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

A coarse classification of a decoded instruction by function, for grouping, coloring, and
summaries without inspecting the mnemonic string.

```csharp
public enum NativeInstructionCategory
```

### [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

The kind of a decoded operand, so consumers (JSON, decoration, diffing) read structure rather
than parsing the rendered text.

```csharp
public enum NativeOperandKind
```

### [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

What a native symbol represents. Native AOT binaries carry compiler-generated code and data
symbols beyond ordinary functions; this classification drives the Size Map's category
grouping and the symbol view's presentation.

```csharp
public enum NativeSymbolKind
```

### [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

Where a binary's native symbols came from. The three primary sources carry names and (mostly)
sizes; the three fallback sources recover only function boundaries from unwind data and are
lower fidelity — they can miss leaf and thunk functions.

```csharp
public enum NativeSymbolSource
```

### [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

The outcome of probing a binary for native symbols. When no symbols are returned, the status
distinguishes the reasons — missing, mismatched, corrupt, ambiguous, or fallback-only — so
callers can explain the result instead of showing an empty table with no cause.

```csharp
public enum NativeSymbolStatus
```

### [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

What a decoded instruction's resolved [TargetAddress](/api/dotsider.core.analysis.models.nativeinstruction.targetaddress/) points at,
so the view can style and navigate a call/branch/data reference correctly.

```csharp
public enum NativeTargetKind
```

### [NetFxArchitecture](/api/dotsider.core.analysis.models.netfxarchitecture/)

Effective process bitness for a .NET Framework root assembly. Models actual runtime
architecture, not the PE's compile-time descriptor — AnyCPU is a compile-time attribute
that resolves to host bitness at load time, so there is no `MSIL` runtime arch.

```csharp
public enum NetFxArchitecture
```

### [NetFxRuntimeVersion](/api/dotsider.core.analysis.models.netfxruntimeversion/)

.NET Framework CLR version a [NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/) targets. The CLR version (not
the product TFM) drives the binding pipeline because the GAC layout, machine.config path,
framework runtime directory, reference-assemblies tree, and `appliesTo` filter all switch
on the CLR generation: [Clr2](/api/dotsider.core.analysis.models.netfxruntimeversion.clr2/) covers .NET Framework 2.0 / 3.0 / 3.5 SP1 (process
runs on `v2.0.50727`); [Clr4](/api/dotsider.core.analysis.models.netfxruntimeversion.clr4/) covers .NET Framework 4.0 through 4.8.x
(process runs on `v4.0.30319`).

```csharp
public enum NetFxRuntimeVersion
```

### [PdbProvenanceKind](/api/dotsider.core.analysis.models.pdbprovenancekind/)

Portable PDB discovery outcomes that are meaningful to .NET developers.

```csharp
public enum PdbProvenanceKind
```

### [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

Identifies which layer of .NET Framework binding policy rewrote a requested assembly
identity. The CLR walks app config first, then publisher policy (unless bypassed by
`&lt;publisherPolicy apply="no"/&gt;`), then machine.config; later layers override
earlier ones, so the effective winner is machine.config &gt; publisher &gt; app &gt;
framework unification.

```csharp
public enum PolicyLayer
```

### [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

The granularity level of a [SizeNode](/api/dotsider.core.analysis.models.sizenode/) in the size breakdown tree. The kinds
beyond [Method](/api/dotsider.core.analysis.models.sizenodekind.method/) appear only in Native AOT trees, built from an mstat report or,
when none sits beside the binary, from its merged native symbols.

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

