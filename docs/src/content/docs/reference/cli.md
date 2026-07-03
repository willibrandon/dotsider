---
title: CLI Reference
description: Complete command reference for the dotsider CLI.
---

## Synopsis

```
dotsider <assembly.dll|.exe>    # TUI — interactive assembly explorer
dotsider <package.nupkg>        # TUI — browse NuGet package contents
dotsider diff <left> <right>    # TUI — side-by-side assembly comparison

dotsider analyze <file> [opts]  # CLI — headless analysis
dotsider sessions <command>     # CLI — interact with running instances
dotsider agent <command>        # CLI — MCP server and AI skill generation
```

## TUI options

| Option | Description |
|--------|-------------|
| `-t`, `--tab <1-8>` | Start on a specific tab |
| `-n`, `--min-len <n>` | Minimum raw string length (default: 4) |
| `-v`, `--version` | Show version |
| `-h`, `--help` | Show help |

## `dotsider analyze`

Run analysis without the TUI — pipe to other tools, write to files, or output JSON.

```
dotsider analyze MyLib.dll                    # assembly info (default)
dotsider analyze MyLib.dll --types            # list type definitions
dotsider analyze MyLib.dll --methods          # list method definitions
dotsider analyze MyLib.dll --il Type.Method   # disassemble a method
dotsider analyze MyLib.dll --embedded-source Type.Method # print embedded source
dotsider analyze MyLib.dll --deps             # assembly references
dotsider analyze MyLib.dll --strings          # extract strings
dotsider analyze MyLib.dll --fields           # list field definitions
dotsider analyze MyLib.dll --size             # size breakdown
dotsider analyze MyLib.dll --bundle           # show single-file bundle manifest
dotsider analyze MyLib.dll --json             # any of the above as JSON
dotsider analyze MyLib.dll --types -o out.txt # write to file
dotsider analyze MyApp.exe                    # apphost .exe → auto-redirects to MyApp.dll
dotsider analyze MyApp                        # single-file bundle → extracts entry assembly
dotsider analyze MyAotApp.exe                 # Native AOT → binary kind, RTR format, imports
dotsider analyze MyAotApp.exe --why System.Text.Json.JsonSerializer  # AOT dependency chain
dotsider analyze MyAotApp.exe --symbols       # native symbols with addresses, sizes, and file:line
```

If the file is a native apphost with a companion `.dll`, `analyze` auto-redirects. If it's a self-contained single-file bundle, `analyze` extracts the entry assembly from the bundle. Both cases print a note to stderr.

If the file is a Native AOT executable (a validated ReadyToRun header with no CLR metadata), the default output adds `Kind`, `RTR Format`, `Runtime`, `Imports`, `R2R`, `Recovered`, and `Frozen` lines, and JSON output gains `binaryKind`, `nativeAotInfo`, `readyToRunSections`, `recoveredTypeCount`, and `frozenStringCount`. `--types` falls back to the types recovered from the embedded metadata, `--strings` adds the raw ASCII and UTF-16 scans plus the frozen string literals, since AOT binaries have no metadata string heaps.

With the ILC sidecars next to the binary (publish with `IlcGenerateMstatFile` and `IlcGenerateDgmlFile`, then copy the `.mstat` and `.codegen.dgml.xml` out of `obj/.../native/`), `--size` prints the compiler's own per-assembly breakdown with the binary's data categories (without an mstat it falls back to the binary's native symbols), `--symbols` lists native symbols with their provenance — the platform's PDB, `.dbg`, or dSYM, or unwind-data boundaries when none exists, `--deps` shows the compiled-in assemblies and native import modules, and `--why <name>` prints the dependency chain that kept a type or method in the binary — root first, one step per line with the compiler's reason. Names match exactly first, then by unambiguous substring.

When portable PDB data is available, default output reports where it came from, and `--il` includes source spans, local names, and Source Link markers. Use `--json` when you need the exact URLs. `--embedded-source` prints source embedded in the PDB.

| Option | Description |
|--------|-------------|
| `--types` | List type definitions |
| `--methods` | List method definitions |
| `--il <name>` | Disassemble a specific method |
| `--embedded-source <name>` | Print embedded source for a method |
| `--deps` | Show assembly references |
| `--strings` | Extract strings |
| `-n`, `--min-len <n>` | Minimum length for raw string extraction (default: 4) |
| `--fields` | List field definitions |
| `--size` | Show size breakdown |
| `--symbols` | List native symbols with provenance (Native AOT and other native binaries) |
| `--why <name>` | Explain why a type or method is in a Native AOT binary |
| `--bundle` | Show single-file bundle manifest |
| `--json` | Output as JSON |
| `-o`, `--output <file>` | Write output to a file |

## `dotsider sessions`

Interact with running dotsider TUI instances. Each instance exposes a Unix domain socket for programmatic access.

```
dotsider sessions list                            # list running instances
dotsider sessions info <pid>                      # assembly info + current view
dotsider sessions view <pid>                      # current tab and view state
dotsider sessions navigate <pid> <tab>            # switch to tab (1-8)
dotsider sessions capture <pid>                   # capture screen as text
dotsider sessions trace start <pid>               # start tracing
dotsider sessions trace events <pid>              # get JIT, GC, exception events
dotsider sessions trace counters <pid>            # get performance counters
dotsider sessions trace output <pid>              # get stdout/stderr
dotsider sessions trace stop <pid>                # stop tracing
```

## `dotsider agent`

MCP server management and AI skill file generation.

```
dotsider agent mcp                                # launch the MCP server
dotsider agent init --ai claude                   # generate skill file
dotsider agent init --path ./SKILL.md             # write to explicit path
dotsider agent init --stdout                      # print to stdout
```

### Supported `--ai` providers

`claude`, `gemini`, `copilot`, `cursor-agent`, `opencode`, `codex`, `windsurf`, `kilocode`, `amp`, `qwen`

Each resolves to the provider's conventional skill path relative to the current directory.
