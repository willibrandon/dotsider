---
title: MCP Server
description: Model Context Protocol server for AI coding assistants.
---

`dotsider-mcp` is a standalone [Model Context Protocol](https://modelcontextprotocol.io) server that exposes dotsider's analysis engine to AI coding assistants.

## Install

### dotnet tool (recommended)

```
dotnet tool install -g Dotsider.Mcp
```

The .NET SDK selects the Native AOT package for supported Windows, Linux, and macOS runtime identifiers. Other supported environments use the framework-dependent `any` package. See [Installation](/getting-started/installation/) for the platform matrix.

### Homebrew (macOS / Linux)

```
brew install willibrandon/tap/dotsider-mcp
```

### WinGet (Windows)

```
winget install willibrandon.dotsider-mcp
```

### Scoop (Windows)

```
scoop install dotsider-mcp
```

### Download binary

Grab a Native AOT archive from [Releases](https://github.com/willibrandon/dotsider/releases). Release binaries are self-contained, so they do not need the .NET SDK or runtime. Native symbols are published separately.

## Configure

Add to your MCP client configuration (e.g. `.mcp.json` for Claude Code):

```json
{
  "mcpServers": {
    "dotsider": {
      "command": "dotsider-mcp"
    }
  }
}
```

## What it provides

**52 tools** across:

| Category | Tools |
|----------|-------|
| Assembly analysis | `get_assembly_info`, `list_types`, `list_methods`, `find_members` |
| Field analysis | `list_fields` |
| IL disassembly | `disassemble_method`, `get_method_debug_info`, `get_source_link`, `search_il_opcodes` |
| Metadata inspection | `get_pe_headers`, `get_clr_header`, `get_sections`, `get_custom_attributes`, `get_resources`, `resolve_token` |
| Dependencies | `get_assembly_refs`, `get_dependency_graph`, `get_type_refs` |
| Size analysis | `get_size_breakdown`, `get_largest_methods` |
| Native symbols | `get_native_symbols`, `get_native_disassembly` |
| Native AOT | `get_native_aot_info`, `list_native_aot_sections`, `get_native_aot_size_contributors`, `explain_native_aot_size` |
| WebAssembly | `list_wasm_sections`, `list_wasm_functions` |
| Correlation | `correlate_method`, `correlate_r2r_method` |
| String extraction | `extract_strings` |
| Diffing | `diff_assemblies`, `diff_size`, `check_size_budgets` |
| NuGet packages | `analyze_nupkg` |
| Bundle inspection | `get_bundle_info`, `list_bundle_entries` |
| Runtime discovery | `find_framework_assembly`, `resolve_assembly` |
| Navigation | `get_current_view`, `navigate_to`, `capture_screen`, `navigate_to_il_definition`, `navigate_back`, `push_assembly` |
| Sessions | `discover_dotsider_sessions`, `get_session_info` |
| Tracing | `get_trace_events`, `get_trace_counters`, `get_process_output`, `start_trace`, `stop_trace` |

Tools work in two modes:

- **Direct mode** — pass an assembly or supported native module path, get results (no TUI needed)
- **Session mode** — connect to a running dotsider TUI via Unix domain socket for live state, tracing, and navigation

Single-file executables and native apphosts are handled transparently in direct mode — the server extracts the entry assembly from bundles and redirects apphosts to their companion DLLs, matching CLI and TUI behavior. Portable PDB data is exposed when present, including provenance, Source Link mappings, sequence points, and local names. Embedded portable PDBs are limited to 256 MiB after decompression and individual embedded source documents to 16 MiB. Oversized or malformed debug data leaves assembly inspection available; `get_assembly_info` reports `pdbProvenance.kind` as `invalidEmbeddedPdb` when no valid sidecar fallback is available.

Native AOT executables are recognized by their embedded ReadyToRun header: `get_assembly_info` reports `binaryKind` (`managed`, `nativeAot`, `readyToRun`, `wasm`, or `native`), a `nativeAotInfo` object with the RTR format version, section count, and heuristically recovered runtime version, plus `readyToRunSectionCount`, `recoveredTypeCount`, and `frozenStringCount`. `list_types` falls back to the types recovered from the embedded NativeFormat metadata, so it names a stripped binary's own types. `extract_strings` returns `rawStrings` (ASCII), `rawUtf16Strings`, and `frozenStrings` alongside the metadata heaps — for AOT binaries the raw scans and frozen literals are the populated categories, and the frozen strings are the AOT counterpart of the #US heap.

NativeFormat recovery is bounded to 1,048,576 encoded handles, 256 relationship levels, 4,096 bytes per name component, and 16,777,216 decoded or composed name characters. For cyclic, malformed, or oversized embedded metadata, `list_types` returns the fully decoded prefix and the remaining Native AOT tools stay available.

Native DWARF materialization from ELF images and dSYM bundles is limited to 256 MiB total per symbol read. Line-table prologues and entry counts are bounded; malformed line metadata omits file-and-line attribution from otherwise readable symbols. Oversized or wholly unreadable DWARF leaves the remaining analysis available; `get_native_symbols` reports corrupt symbol data and returns `.eh_frame` boundaries when present.

Native PDB MSF directories and CodeView module ranges are validated before allocation or decoding. A malformed companion PDB is reported as corrupt symbol data, and `get_native_symbols` returns `.pdata` boundaries when the image provides them.

ReadyToRun (crossgen2) images keep their full metadata: `get_assembly_info` reports `binaryKind` `readyToRun` and a `readyToRun` object (version, status, architecture, composite/component, method counts). `correlate_r2r_method` takes a `Type.Method`, a `0x06…` token, or a `0x…` native address and returns the method's IL beside its precompiled native code ranges with import-resolved call targets; an overloaded name raises an error listing the candidates. Method-map NativeFormat containers share a 1,048,576-unit traversal budget across MethodDef indices, instance buckets, and instance entries; import sections have a separate 1,048,576-slot budget. Malformed or oversized method maps preserve assembly metadata while correlation reports the map diagnostic, and import decoding preserves the complete named prefix accepted within its budget. It follows a composite across its component assemblies in both directions, resolving them by name and MVID.

Raw `dotnet.native.wasm` modules report `binaryKind` `wasm` and a `wasm` object from `get_assembly_info` with section, type/table/memory/global, function, code, data, import/export, and symbol-map counts. Structured vectors must fit their containing region and share a 1,048,576-item decoding budget; `wasm.diagnostic` explains where malformed standard data stopped parsing while the preceding facts remain available. `get_native_symbols` lists defined Wasm functions and `get_native_disassembly` decodes Wasm32 bodies by name, `func:N`, decimal function index, or `0x…` file/code offset, with direct-call target names from the module function index plus local/global/table/type annotations from the parsed standard sections. Webcil app assemblies such as `WasmConsole.wasm` report `binaryKind` `managed` with a `webcil` object; metadata, IL, PDB, Source Link, and member-search tools work against the unwrapped managed assembly.

For deeper raw Wasm inspection, `list_wasm_sections` returns the section table with payload offsets and sizes, and `list_wasm_functions` returns imported plus defined functions in the same function-index order used by Wasm `call` operands, symbol maps, and `get_native_disassembly`.

Native disassembly tools return structured instructions for x64, Arm64, x86, Arm32/Thumb-2, RISC-V64, LoongArch64, and Wasm32. For ReadyToRun, `get_native_disassembly` accepts a method name or address and renders all of that method's native ranges together.

`diff_size` compares two Native AOT builds via their mstat size reports (bare `.mstat` files or binaries with sidecars): summary, per-assembly and per-namespace deltas, top contributors, and — only on request — the delta tree, pruned to a node cap with explicit truncation metadata. `check_size_budgets` evaluates size budgets against a build (optionally versus a baseline) and returns the per-budget report; budgets arrive as grammar strings, an inline budgets JSON document, or a budgets file path, at full parity with the CLI including named budgets and warning severity. See [Size Regression](/usage/size-regression/).

`start_trace` accepts `arguments` as an array of literal strings. Argument boundaries are
preserved through the session protocol and `ProcessStartInfo.ArgumentList`.

Session sockets are access-controlled. The socket directory and socket file are restricted to the current user on all platforms, connections are verified against the process owner, and protocol version 2 rejects mismatched clients. Concurrent connections are capped at four per session, and each UTF-8 request payload is capped at 1 MiB.

## Guided prompts

**5 prompts** for common workflows:

| Prompt | Purpose |
|--------|---------|
| Security audit | Analyze an assembly for security concerns |
| API surface review | Map the public API surface |
| Breaking change detection | Compare two versions for breaking changes |
| Dependency health | Assess dependency risk and freshness |
| Bundle analysis | Inspect a single-file bundle: structure, entry assembly, and dependency resolution |

## Generating agent skill files

```
dotsider agent init
```

This writes `SKILL.md` in the current directory. Use `--path` to write it directly to the location expected by your agent.
