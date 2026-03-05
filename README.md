# dotsider

A TUI for analyzing .NET assemblies — structure, metadata, IL, strings, dependencies, and more. Inspired by [binsider](https://github.com/orhun/binsider) for ELF binaries, built for the .NET ecosystem.

```
dotsider HelloWorld.dll
```

[![dotsider demo](https://raw.githubusercontent.com/willibrandon/dotsider/main/assets/demo.svg)](https://asciinema.org/a/1LtrQnTDxJUUjCMq)

## Installation

### dotnet tool (recommended)

```
dotnet tool install -g dotsider
```

### Download binary

Grab a standalone binary from [Releases](https://github.com/willibrandon/dotsider/releases).

## What it does

dotsider opens any .NET DLL or EXE and lets you explore it across 8 tabs:

| Tab | What you see |
|-----|-------------|
| **1 General** | Assembly identity, target framework, architecture, dependency table. Press Enter on a reference to drill into it. |
| **2 PE/Metadata** | COFF headers, CLR header, sections, TypeDefs, MethodDefs, AssemblyRefs, custom attributes, resources. |
| **3 IL Inspector** | Namespace/Type/Method tree with IL disassembly. Select a method, read its bytecode. |
| **4 Strings** | User strings, metadata strings, and raw binary string scan with configurable minimum length. |
| **5 Hex Dump** | Hex editor with vi-style modal editing (read-only by default), byte category coloring, data interpretation panel, jump-to-offset, and vim navigation. |
| **6 Dep Graph** | Visual dependency graph — your assembly at the root, references as nodes, edge weights by TypeRef count. |
| **7 Size Map** | Treemap of code size — Assembly > Namespace > Type > Method, sized by IL byte count. Click to drill in. |
| **8 Dynamic** | Launch the assembly and trace it live via EventPipe — GC events, JIT compilations, exceptions, performance counters, stdout. |

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
dotsider <assembly.dll|.exe>    # analyze a .NET assembly
dotsider diff <left> <right>    # compare two assemblies
dotsider <package.nupkg>        # browse a NuGet package

Options:
  -t, --tab <1-8>               start on a specific tab
  -n, --min-len <n>             minimum raw string length (default: 4)
  -v, --version                 show version
  -h, --help                    show help
```

### Keyboard

| Key | Action |
|-----|--------|
| `1`-`8` | Switch tabs |
| `Enter` | Drill into selected item (assembly ref, method, DLL in package) |
| `Backspace` | Go back |
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

## Project structure

```
src/Dotsider/
  Analysis/           PE reading, metadata extraction, IL disassembly,
                      diffing, dependency graphs, size analysis, runtime tracing
  Views/              One file per tab — widget trees built each frame
  DotsiderApp.cs      Main app shell (tab panel, key bindings, hints bar)
  DotsiderState.cs    All mutable UI state in one place
  DiffApp.cs          Diff mode shell
  NuGetApp.cs         NuGet mode shell
  Program.cs          CLI entry point and mode routing

samples/
  HelloWorld/         Minimal console app
  ComplexApp/         Async pipeline with embedded resources
  RichLibrary/        Library with NuGet deps (Newtonsoft.Json, System.Text.Json)
  RichLibraryV2/      Same library with deliberate API changes (for diff testing)
  MinimalApi/         ASP.NET Core minimal API (web SDK, hosted entry point)
  NativeLib/          Unsafe code, P/Invoke, pointer operations
  EmptyLib/           Minimal library (edge case testing)

tests/Dotsider.Tests/
  SampleAssemblyFixture.cs   Builds all 7 samples once, shared across tests
  *Tests.cs                  Integration tests against real assemblies
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
