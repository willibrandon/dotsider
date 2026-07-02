# Dotsider Benchmarks

BenchmarkDotNet project measuring performance of core analyzers against large BCL assemblies and real sample fixtures.

## Running

Run all benchmarks:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release
```

Run a specific benchmark class:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --filter '*HexSearchBenchmarks*'
```

Run the parameterized hex search threshold test:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --filter '*HexSearchThresholdBenchmarks*'
```

List all available benchmarks:

```sh
dotnet run --project benchmarks/Dotsider.Benchmarks -c Release -- --list flat
```

## Benchmark Classes

| Class | What it measures |
|---|---|
| `ApphostDetectorBenchmarks` | Apphost companion-DLL detection (real apphost, dotted-name, fake exe, early exit) and bundled-entry extraction |
| `AssemblyAnalyzerBenchmarks` | Constructor (file and byte[]), lazy metadata properties (TypeDefs, MethodDefs, AssemblyRefs, TypeRefs, MemberRefs, FieldDefs, CustomAttributes, Resources, Sections), token resolution, and 7-step assembly resolution chain (app-local, NuGet deps.json, runtime directory, source bundle, host bundle, adjacent bundles, shared framework) |
| `AssemblyDifferBenchmarks` | Dictionary-based O(n) diff of two assemblies by type, method, and reference with normalized IL body comparison |
| `DependencyGraphBuilderBenchmarks` | Full transitive assembly dependency closure — BFS walk that resolves each AssemblyRef by full identity, opens it, and recurses (CoreLib has no outbound refs so this is pure builder overhead; Xml walks its full BCL/runtime-pack closure) |
| `DotNetRuntimeLocatorColdBenchmarks` | Cold-path .NET runtime discovery: base path + shared framework resolution with cache cleared |
| `DotNetRuntimeLocatorWarmBenchmarks` | Warm-cache ConcurrentDictionary hit for shared framework lookup |
| `HexSearchBenchmarks` | `FindBytePattern` with short, long, and no-match patterns against real assemblies |
| `HexSearchThresholdBenchmarks` | Parameterized sweep (4–16MB) to pinpoint the 8ms adaptive search crossover |
| `IlDisassemblerBenchmarks` | Bulk disassembly/format of all methods, single-method DisassembleWithText and GetHeaderLineCount |
| `IlNavigationResolverBenchmarks` | Token resolution across all metadata handle kinds (MethodDef, TypeDef, FieldDef, MemberRef, TypeSpec, MethodSpec) plus batch method-body resolution |
| `ImplementationAssemblyResolverColdBenchmarks` | Cold-path reference-to-implementation resolution: known mappings, type forwarder chains, direct usable metadata |
| `ImplementationAssemblyResolverWarmBenchmarks` | Warm-cache hit for implementation assembly resolution |
| `McpToolBenchmarks` | MCP tool call round-trip and session discovery through in-process pipe transport |
| `NativeAotDetectorBenchmarks` | ReadyToRun header scan + validation on a real Native AOT exe (positive with false-positive rejection), CoreLib (R2R negative full scan), and an apphost (no candidates) |
| `NuGetPackageAnalyzerBenchmarks` | NuGet package construction and DLL extraction from standard (2 DLLs) and large (120+ entry) packages |
| `PeDirectoryReaderBenchmarks` | Native import/export/load-config parsing on a Native AOT binary (PE, ELF, or Mach-O by platform) and CoreLib |
| `RuntimeTracerDataRetrievalBenchmarks` | Ring buffer snapshot, summary aggregation, output materialization, and counter read with populated trace data |
| `RuntimeTracerThroughputBenchmarks` | Data retrieval under concurrent event-processing load (lock contention, volatile reads) |
| `RuntimeTracerWritePathBenchmarks` | Full write pipeline throughput: event collection, counter acquisition, and Start/Stop lifecycle with EventPipe connect latency |
| `SingleFileBundleReaderBenchmarks` | Bundle signature scanning (file/span, positive/negative), manifest parsing, and full entry-assembly extraction |
| `SizeAnalyzerBenchmarks` | `BuildSizeTree` full traversal |
| `StringExtractorBenchmarks` | UserStrings, MetadataStrings, RawStrings, and RawUtf16Strings extraction |
| `TreemapLayoutBenchmarks` | Squarified treemap rectangle computation for assembly size trees |

## Test Assemblies

Benchmarks use BCL assemblies from the running .NET runtime directory:

- **System.Private.CoreLib.dll** (~16MB) — largest BCL assembly, stress tests all analyzers
- **System.Private.Xml.dll** (~8MB) — mid-size assembly near the hex search threshold

Benchmarks that require real .NET executables (apphost, single-file bundle, runtime tracing) build the frozen sample fixtures under `samples/` in GlobalSetup.

## Baseline Results

### macOS — Apple M4 Pro, .NET 10.0.2

#### ApphostDetector

| Benchmark | Mean | Allocated |
|---|---|---|
| Real apphost (HelloWorld) | 44.16 μs | 131 KB |
| Dotted-name apphost (Dotted.Name.App) | 44.27 μs | 131 KB |
| Fake exe full scan (no hostfxr) | 12.88 μs | 1.14 KB |
| .dll early exit (baseline) | 0.54 ns | — |
| Real single-file bundle (positive) | 10.28 ms | 76.3 MB |
| Apphost not a bundle (negative scan) | 54.79 μs | 122 KB |

#### AssemblyAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib Construction | 1.57 ms | 16.29 MB |
| Xml Construction | 0.82 ms | 8.60 MB |
| CoreLib from byte[] (bundle model) | 66.24 μs | 12.56 KB |
| CoreLib TypeDefs | 2.57 ms | 17.90 MB |
| Xml TypeDefs | 1.24 ms | 9.65 MB |
| CoreLib MethodDefs | 25.50 ms | 61.25 MB |
| Xml MethodDefs | 11.23 ms | 21.49 MB |
| CoreLib AssemblyRefs | 1.55 ms | 16.29 MB |
| CoreLib TypeRefs | 1.52 ms | 16.29 MB |
| CoreLib MemberRefs (signature decoding) | 5.23 ms | 20.80 MB |
| CoreLib FieldDefs (signature decoding) | 3.45 ms | 19.85 MB |
| CoreLib CustomAttributes | 17.11 ms | 37.65 MB |
| CoreLib Resources | 1.43 ms | 16.29 MB |
| CoreLib Sections | 1.37 ms | 16.29 MB |
| ResolveToken (MethodDef → name) | 1.40 ms | 16.29 MB |
| ResolveAssembly step 3 (runtime dir) | 28.84 μs | 3.53 KB |
| ResolveAssembly step 7 (shared framework) | 203.30 μs | 5.28 KB |
| ResolveAssembly miss (all 7 steps) | 203.79 μs | 5.40 KB |

ResolveAssembly now includes a NuGet `.deps.json` probe between app-local and runtime-dir so library projects (RichLibrary, etc.) find their NuGet dependencies. The direct base-name lookup is a single `File.Exists` per resolve; when no manifest sits next to the referencing assembly the probe returns in microseconds. The ~23 μs delta vs. the prior baseline is the cost of that check on every resolve.

#### AssemblyDiffer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib vs Xml (max diff) | 40.25 ms | 9.03 MB |
| CoreLib vs CoreLib (identity) | 151.03 ms | 293.56 MB |
| CoreLib vs CoreLib (distinct) | 155.46 ms | 293.56 MB |
| RichLibrary v1 vs v2 (body diff) | 149.9 μs | 358 KB |

Identity and distinct benchmarks are worst-case: every method matches, forcing normalized token resolution across 12K+ bodies. Cross-assembly is flat because few methods share keys. RichLibrary is a realistic version diff.

#### DependencyGraphBuilder

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib graph | 229.9 ns | 2 KB |
| Xml graph | 20.24 ms | 78.60 MB |

The builder now produces a full transitive closure: every AssemblyRef is resolved by full identity, opened, and recursed into. CoreLib has no outbound refs so its graph is a single root node (the baseline reflects pure BFS + layout overhead). Xml refs most of the BCL, so its closure walks ~dozens of additional assemblies — each one opened via `AssemblyAnalyzer`, which dominates both time and allocation. The prior 8 μs baseline measured only the root's direct-ref star without any resolution or traversal; the new number is the cost of doing the thing the name actually claims.

#### DotNetRuntimeLocator

| Benchmark | Mean | Allocated |
|---|---|---|
| FindAssembly (cold) | 199.5 μs | 13.63 KB |
| FindBasePath (cold) | 116.3 μs | 8.44 KB |
| FindAssembly (warm cache hit) | 23.06 ns | — |

#### Hex Search (FindBytePattern)

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib short pattern (2B) | 12.13 ms | 1,144 B |
| CoreLib long pattern (14B) | 13.39 ms | 33 KB |
| CoreLib no-match (full scan) | 13.36 ms | 32 B |
| Xml short pattern (2B) | 6.31 ms | 608 B |
| Xml long pattern (14B) | 7.04 ms | 176 B |
| Xml no-match (full scan) | 7.05 ms | 32 B |

#### Hex Search Threshold (8ms crossover)

| SizeMB | Mean |
|---|---|
| 4 | 3.33 ms |
| 8 | 6.61 ms |
| 9 | 7.40 ms |
| **10** | **8.29 ms** |
| 11 | 9.33 ms |
| 12 | 10.05 ms |
| 16 | 13.35 ms |

The 8ms adaptive threshold (`HexDumpView` line 44) crosses at ~10MB on this machine. Scaling is linear at ~0.83 ms/MB.

#### IlDisassembler

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib DisassembleAll | 50.59 ms | 160.26 MB |
| Xml DisassembleAll | 43.90 ms | 147.06 MB |
| CoreLib FormatAll | 87.72 ms | 325.10 MB |
| Xml FormatAll | 73.79 ms | 279.28 MB |
| CoreLib DisassembleWithText single method | 3.43 μs | 16.39 KB |
| CoreLib GetHeaderLineCount single method | 11.37 ns | 56 B |

#### IlNavigationResolver

| Benchmark | Mean | Allocated |
|---|---|---|
| MethodDef | 6.97 ns | 48 B |
| TypeDef | 6.97 ns | 48 B |
| FieldDef | 13.62 ns | 64 B |
| MemberRef (method) | 103.71 ns | 472 B |
| MemberRef (field) | 63.76 ns | 256 B |
| TypeSpec | 214.34 ns | 672 B |
| MethodSpec | 258.06 ns | 1,344 B |
| Batch method body | 37.50 ns | 176 B |

#### ImplementationAssemblyResolver

| Benchmark | Mean | Allocated |
|---|---|---|
| Known mapping cold (System.Runtime → CoreLib) | 33.13 μs | 10.59 KB |
| Type forwarder (mscorlib → System.Object) | 33.64 μs | 10.61 KB |
| Direct usable (System.Private.Xml) | 36.25 μs | 10.80 KB |
| Known mapping warm cache hit | 67.62 ns | — |

#### NativeAotDetector

| Benchmark | Mean | Allocated |
|---|---|---|
| Detect NativeAOT exe (positive) | — | — |
| Detect CoreLib (R2R negative) | — | — |
| Detect apphost (negative) | — | — |

#### NuGetPackageAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| Construction (2 DLLs, ~24MB) | 42.98 μs | 42.66 KB |
| OpenDll (CoreLib ~16MB) | 32.82 ms | 16,335 KB |
| Construction (120+ entries) | 115.41 μs | 143.17 KB |
| OpenDll from large package (CoreLib) | 30.35 ms | 16,434 KB |

#### PeDirectoryReader

| Benchmark | Mean | Allocated |
|---|---|---|
| NativeAOT Imports | — | — |
| NativeAOT Exports | — | — |
| NativeAOT LoadConfig | — | — |
| CoreLib Imports | — | — |

#### RuntimeTracer — Data Retrieval (populated)

| Benchmark | Mean | Allocated |
|---|---|---|
| GetEvents (populated ring buffer) | 343.0 μs | 2,432 B |
| GetSummary (populated accumulators) | 2.15 ms | 408 B |
| GetOutput (populated queue) | 1.05 ms | 552 B |
| GetLatestCounters (populated snapshot) | 151.3 μs | — |

#### RuntimeTracer — Throughput (under load)

| Benchmark | Mean | Allocated |
|---|---|---|
| GetEvents under load (lock contention) | 444.7 μs | 78.15 KB |
| GetLatestCounters under load (volatile read) | 100.3 μs | — |
| GetSummary under load (dict copy + aggregation) | 1.00 ms | 328 B |

#### RuntimeTracer — Write Path

| Benchmark | Mean | Allocated |
|---|---|---|
| Event collection throughput (2s trace) | 2.18 s | 140.28 MB |
| Counter acquisition pipeline (3s trace) | 3.15 s | 205.48 MB |
| Start/Stop lifecycle (EventPipe connect latency) | 2.70 s | 703.63 KB |

#### SingleFileBundleReader

| Benchmark | Mean | Allocated |
|---|---|---|
| IsBundle (CoreLib, negative full scan) | 4.98 ms | 586 B |
| IsBundle (bundle, positive) | 2.38 ms | 569 B |
| IsBundle span (CoreLib, negative) | 4.42 ms | — |
| IsBundle span (bundle, positive) | 2.17 ms | — |
| ReadManifest | 20.67 μs | 29.47 KB |
| FindEntryAssembly (full pipeline) | 2.40 ms | 39.90 KB |

#### StringExtractor

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib UserStrings | 59.15 μs | 368 KB |
| CoreLib MetadataStrings | 1.79 ms | 2,878 KB |
| CoreLib RawStrings | 27.76 ms | 6,490 KB |
| Xml UserStrings | 62.56 μs | 375 KB |
| Xml MetadataStrings | 1.02 ms | 1,519 KB |
| Xml RawStrings | 14.97 ms | 3,962 KB |
| CoreLib RawUtf16Strings | — | — |
| Xml RawUtf16Strings | — | — |

#### SizeAnalyzer

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib BuildSizeTree | 9.09 ms | 20.48 MB |
| Xml BuildSizeTree | 3.66 ms | 10.24 MB |

#### TreemapLayout

| Benchmark | Mean | Allocated |
|---|---|---|
| CoreLib layout (120x30) | 1,564.0 ns | 7.3 KB |
| Xml layout (120x30) | 451.0 ns | 2.91 KB |
| CoreLib layout (240x60) | 1,567.2 ns | 7.3 KB |

### MCP Server

Full tool call round-trip through the in-process pipe transport — includes JSON-RPC framing, DI resolution, filter execution, tool dispatch, analysis, JSON serialization, and response framing.

#### McpToolBenchmarks

| Benchmark | Mean | Allocated |
|---|---|---|
| GetAssemblyInfo (CoreLib) | 31.29 ms | 62,849 KB |
| ListTypes (CoreLib) | 11.27 ms | 25,486 KB |
| GetSizeBreakdown (CoreLib) | 133.88 ms | 159,729 KB |
| DisassembleMethod (single) | 32.08 ms | 61,255 KB |
| ExtractStrings (CoreLib) | 137.59 ms | 114,283 KB |
| DiscoverSessions (5 sockets) | 0.22 ms | 144 KB |

Session discovery exercises the full path: directory scan, UDS connect to each socket, assembly-info round-trip, stale socket cleanup, and JSON serialization. Benchmarks use an isolated temp directory for reproducibility.
