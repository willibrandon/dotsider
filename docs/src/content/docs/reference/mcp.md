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

Grab a standalone binary from [Releases](https://github.com/willibrandon/dotsider/releases). Binaries are self-contained — no .NET SDK needed.

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

**42 tools** across:

| Category | Tools |
|----------|-------|
| Assembly analysis | `get_assembly_info`, `list_types`, `list_methods`, `find_members` |
| Field analysis | `list_fields` |
| IL disassembly | `disassemble_method`, `get_method_debug_info`, `get_source_link`, `search_il_opcodes` |
| Metadata inspection | `get_pe_headers`, `get_clr_header`, `get_sections`, `get_custom_attributes`, `get_resources`, `resolve_token` |
| Dependencies | `get_assembly_refs`, `get_dependency_graph`, `get_type_refs` |
| Size analysis | `get_size_breakdown`, `get_largest_methods` |
| Native symbols | `get_native_symbols`, `get_native_disassembly` |
| String extraction | `extract_strings` |
| Diffing | `diff_assemblies` |
| NuGet packages | `analyze_nupkg` |
| Bundle inspection | `get_bundle_info`, `list_bundle_entries` |
| Runtime discovery | `find_framework_assembly`, `resolve_assembly` |
| Navigation | `get_current_view`, `navigate_to`, `capture_screen`, `navigate_to_il_definition`, `navigate_back`, `push_assembly` |
| Sessions | `discover_dotsider_sessions`, `get_session_info` |
| Tracing | `get_trace_events`, `get_trace_counters`, `get_process_output`, `start_trace`, `stop_trace` |

Tools work in two modes:

- **Direct mode** — pass an assembly path, get results (no TUI needed)
- **Session mode** — connect to a running dotsider TUI via Unix domain socket for live state, tracing, and navigation

Single-file executables and native apphosts are handled transparently in direct mode — the server extracts the entry assembly from bundles and redirects apphosts to their companion DLLs, matching CLI and TUI behavior. Portable PDB data is exposed when present, including provenance, Source Link mappings, sequence points, and local names.

Native AOT executables are recognized by their embedded ReadyToRun header: `get_assembly_info` reports `binaryKind` (`managed`, `nativeAot`, or `native`), a `nativeAotInfo` object with the RTR format version, section count, and heuristically recovered runtime version, plus `readyToRunSectionCount`, `recoveredTypeCount`, and `frozenStringCount`. `list_types` falls back to the types recovered from the embedded NativeFormat metadata, so it names a stripped binary's own types. `extract_strings` returns `rawStrings` (ASCII), `rawUtf16Strings`, and `frozenStrings` alongside the metadata heaps — for AOT binaries the raw scans and frozen literals are the populated categories, and the frozen strings are the AOT counterpart of the #US heap.

Session sockets are access-controlled. The socket directory and socket file are restricted to the current user on all platforms, connections are verified against the process owner, and a versioned protocol rejects mismatched clients. Concurrent connections are capped at four per session.

## Guided prompts

**5 prompts** for common workflows:

| Prompt | Purpose |
|--------|---------|
| Security audit | Analyze an assembly for security concerns |
| API surface review | Map the public API surface |
| Breaking change detection | Compare two versions for breaking changes |
| Dependency health | Assess dependency risk and freshness |
| Bundle analysis | Inspect a single-file bundle: structure, entry assembly, and dependency resolution |

## Generating AI skill files

```
dotsider agent init --ai claude
```

This writes a skill file to the provider's conventional location. Supported providers: `claude`, `gemini`, `copilot`, `cursor-agent`, `opencode`, `codex`, `windsurf`, `kilocode`, `amp`, `qwen`.
