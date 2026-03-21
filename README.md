# Dotsider

A TUI for analyzing .NET assemblies — structure, metadata, IL, strings, dependencies, and more. Inspired by [binsider](https://github.com/orhun/binsider) for ELF binaries, built for the .NET ecosystem.

```
dotsider HelloWorld.dll
```

## Installation

### dotnet tool (recommended)

```
dotnet tool install -g dotsider
```

### Homebrew (macOS / Linux)

```
brew install willibrandon/tap/dotsider
```

### WinGet (Windows)

```
winget install willibrandon.dotsider
```

### Scoop (Windows)

```
scoop bucket add dotsider https://github.com/willibrandon/scoop-bucket
scoop install dotsider
```

### Download binary

Grab a standalone binary from [Releases](https://github.com/willibrandon/dotsider/releases).

## What it does

dotsider opens any .NET DLL or EXE and lets you explore it across 8 tabs:

| Tab | What you see |
|-----|-------------|
| **1 General** | Assembly identity, target framework, architecture, dependency table. Press Enter on a reference to drill into it. |
| **2 PE/Metadata** | COFF headers, CLR header, sections, TypeDefs, MethodDefs, AssemblyRefs, custom attributes, resources. Press `g` on a TypeDef or MethodDef to jump to its IL. |
| **3 IL Inspector** | Namespace/Type/Method tree with IL disassembly. Select a method, read its bytecode. Press `x` to jump to the method body in the hex dump. |
| **4 Strings** | User strings, metadata strings, and raw binary string scan with configurable minimum length. |
| **5 Hex Dump** | Hex editor with vi-style modal editing (read-only by default), byte category coloring, data interpretation panel, jump-to-offset, and vim navigation. |
| **6 Dep Graph** | Visual dependency graph — your assembly at the root, references as nodes, edge weights by TypeRef count. Press Enter on a node to open that assembly. |
| **7 Size Map** | Treemap of code size — Assembly > Namespace > Type > Method, sized by IL byte count. Click to drill in; Enter on a method leaf jumps to its IL. |
| **8 Dynamic** | Launch the assembly and trace it live via EventPipe — GC events, JIT compilations, exceptions, performance counters, stdout. Press Enter on a JIT event to jump to that method's IL. |

### Additional modes

```
dotsider diff v1.dll v2.dll     # side-by-side assembly comparison
dotsider package.nupkg          # browse NuGet package contents, inspect any DLL inside
```

## Building

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build
```

The binary lands at `src/Dotsider/bin/Debug/net10.0/dotsider`.

## Usage

```
dotsider <assembly.dll|.exe>    # TUI mode — interactive assembly explorer
dotsider <package.nupkg>        # TUI mode — browse NuGet package contents
dotsider diff <left> <right>    # TUI mode — side-by-side assembly comparison

dotsider analyze <file> [opts]  # CLI mode — headless analysis to stdout or file
dotsider sessions <command>     # CLI mode — interact with running dotsider instances
dotsider agent init [opts]      # CLI mode — generate AI skill file for a provider
dotsider agent mcp              # CLI mode — launch the dotsider MCP server

TUI options:
  -t, --tab <1-8>               start on a specific tab
  -n, --min-len <n>             minimum raw string length (default: 4)
  -v, --version                 show version
  -h, --help                    show help
```

### CLI: `dotsider analyze`

Run analysis without the TUI — pipe to other tools, write to files, or output JSON for scripting.

```
dotsider analyze MyLib.dll                  # assembly info (default)
dotsider analyze MyLib.dll --types          # list type definitions
dotsider analyze MyLib.dll --methods        # list method definitions
dotsider analyze MyLib.dll --il Type.Method # disassemble a method
dotsider analyze MyLib.dll --deps           # assembly references
dotsider analyze MyLib.dll --strings        # extract strings
dotsider analyze MyLib.dll --size           # size breakdown
dotsider analyze MyLib.dll --json           # any of the above as JSON
dotsider analyze MyLib.dll --types -o out.txt  # write to file
```

### CLI: `dotsider sessions`

Interact with running dotsider TUI instances. Each instance exposes a Unix domain socket for programmatic access.

```
dotsider sessions list                          # list running instances
dotsider sessions info <pid>                    # assembly info + current view
dotsider sessions view <pid>                    # current tab and view state
dotsider sessions navigate <pid> <tab>          # switch to tab (0-7)
dotsider sessions capture <pid>                 # capture screen as plain text
dotsider sessions trace start <pid>             # start tracing the loaded assembly
dotsider sessions trace events <pid>            # get JIT, GC, exception events
dotsider sessions trace counters <pid>          # get performance counters
dotsider sessions trace output <pid>            # get stdout/stderr from traced process
dotsider sessions trace stop <pid>              # stop the active trace
```

### CLI: `dotsider agent`

MCP server management and AI skill file generation.

```
dotsider agent mcp                              # launch the dotsider MCP server
dotsider agent init --ai claude                 # generate skill file for a provider
dotsider agent init --path ./SKILL.md           # write to an explicit path
dotsider agent init --stdout                    # print skill content to stdout
```

Supported `--ai` providers: claude, gemini, copilot, cursor-agent, opencode, codex, windsurf, kilocode, amp, qwen. Each resolves to the provider's conventional skill path relative to the current directory.

### Keyboard

| Key | Action |
|-----|--------|
| `1`-`8` | Switch tabs |
| `Enter` | Drill into selected item (assembly ref, method, DLL in package) |
| `Backspace` | Go back (assembly stack, breadcrumb, or cross-view jump) |
| `g` | Go to IL Inspector for the focused TypeDef/MethodDef (PE/Metadata tab) |
| `x` | Jump to method body in Hex Dump (IL Inspector tab, when a method with RVA is selected) |
| `/` | Search (highlights matches inline) |
| `n` / `N` | Next / previous search match |
| `s` | Toggle human-readable sizes |
| `q` | Quit |

**Hex Dump tab** uses vi-style modal editing — the editor starts in normal mode (read-only) to prevent accidental writes:

| Key | Mode | Action |
|-----|------|--------|
| `i` | Normal | Enter insert mode (enables byte editing) |
| `Esc` | Insert | Return to normal mode |
| `h` `j` `k` `l` | Normal | Vim-style cursor movement |
| `g` | Normal | Jump to hex offset |
| `e` | Normal | Toggle endianness (LE/BE) |
| `Ctrl+T` | Any | Toggle text/hex search mode |
| `Ctrl+S` | Normal | Save edited bytes (only when modified) |

In insert mode, type two hex digits (0-9, a-f) to overwrite one byte. The first digit sets the high nibble; the second commits the edit. Saving validates the PE image before writing — invalid edits are rejected.

Diff mode adds `f` to cycle filters (All / Added / Removed / Changed).

## How it works

dotsider reads assemblies using APIs that ship with the .NET runtime itself — no third-party analysis libraries needed:

- **`System.Reflection.Metadata`** provides `MetadataReader` for traversing the metadata tables (types, methods, references, custom attributes, string heaps)
- **`System.Reflection.PortableExecutable`** provides `PEReader` for the PE structure (COFF header, sections, CLR header, method bodies)
- **`System.IO.Compression`** handles NuGet packages (which are just ZIP files containing a `.nuspec` manifest and DLLs)

The dynamic analysis tab uses `Microsoft.Diagnostics.NETCore.Client` to connect to a running .NET process via EventPipe — the same diagnostic infrastructure that powers `dotnet-trace` and `dotnet-counters`. It launches your assembly with a reverse-connect diagnostic port, so events are captured from the very first instruction.

The TUI is built on [Hex1b](https://github.com/mitchdenny/hex1b), a .NET terminal UI library with a React-inspired declarative API, constraint-based layout, theming, and efficient widget reconciliation.

## MCP server

`dotsider-mcp` is a standalone [Model Context Protocol](https://modelcontextprotocol.io) server that exposes dotsider's analysis engine to AI coding assistants like Claude Code, VS Code Copilot, and others.

### Install

#### dotnet tool (recommended)

```
dotnet tool install -g Dotsider.Mcp
```

#### Homebrew (macOS / Linux)

```
brew install willibrandon/tap/dotsider-mcp
```

#### WinGet (Windows)

```
winget install willibrandon.dotsider-mcp
```

#### Scoop (Windows)

```
scoop install dotsider-mcp
```

#### Download binary

Grab a standalone binary from [Releases](https://github.com/willibrandon/dotsider/releases).

### Configure

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

### What it provides

**28 tools** across assembly analysis, IL disassembly, metadata inspection, dependency graphs, size analysis, string extraction, diffing, NuGet package analysis, and runtime tracing. Tools work in two modes:

- **Direct mode** — pass an assembly path, get results (no TUI needed)
- **Session mode** — connect to a running dotsider TUI instance via Unix domain socket for live state, tracing, and navigation

**4 guided prompts** for common workflows: security audit, API surface review, breaking change detection, and dependency health analysis.

## Project structure

```
src/Dotsider.Core/
  Analysis/           PE reading, metadata extraction, IL disassembly,
                      diffing, dependency graphs, size analysis, runtime tracing
  Analysis/Models/    Data types for analysis results
  Protocol/           Request/response types and JSON options for the UDS protocol

src/Dotsider/
  Commands/           CLI subcommands (analyze, sessions, agent)
  Diagnostics/        Unix domain socket listener for TUI state access
  Infrastructure/     Output formatting, session discovery
  Views/              One file per tab — widget trees built each frame
  DotsiderApp.cs      Main app shell (tab panel, key bindings, hints bar)
  DotsiderState.cs    All mutable UI state in one place
  DiffApp.cs          Diff mode shell
  NuGetApp.cs         NuGet mode shell
  Program.cs          CLI entry point and mode routing

src/Dotsider.Mcp/
  Tools/              MCP tool classes (assembly, IL, metadata, deps, size, etc.)
  Prompts/            Guided analysis prompts (security, API review, breaking changes)
  Program.cs          MCP server entry point (stdio transport)

samples/
  HelloWorld/         Minimal console app
  ComplexApp/         Async pipeline with embedded resources
  RichLibrary/        Library with NuGet deps (Newtonsoft.Json, System.Text.Json)
  RichLibraryV2/      Same library with deliberate API changes (for diff testing)
  MinimalApi/         ASP.NET Core minimal API (web SDK, hosted entry point)
  NativeLib/          Unsafe code, P/Invoke, pointer operations
  EmptyLib/           Minimal library (edge case testing)
  NetFxConsole/       .NET Framework 4.8 console app (Dynamic tab guard testing)
  NativeAotConsole/   NativeAOT-published console app (Dynamic tab tracing tests)

tests/Dotsider.Tests/
  SampleAssemblyFixture.cs   Builds all 9 samples once, shared across tests
  *Tests.cs                  Integration tests against real assemblies

tests/Dotsider.Mcp.Tests/
  McpServerTestBase.cs       In-memory MCP server setup for testing
  *Tests.cs                  MCP tool and prompt integration tests
```

## Testing

```
dotnet test
```

Integration tests run against real .NET assemblies. The test fixture builds all sample projects automatically. First run takes longer due to NuGet restore; subsequent runs use cache.

## Samples

Build the sample assemblies to have something to analyze:

```
dotnet build samples/RichLibrary
dotsider samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll
```

Try diff mode with the two library versions:

```
dotnet build samples/RichLibraryV2
dotsider diff \
  samples/RichLibrary/bin/Debug/net10.0/RichLibrary.dll \
  samples/RichLibraryV2/bin/Debug/net10.0/RichLibrary.dll
```

## License

MIT
