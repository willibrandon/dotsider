---
title: CLI Reference
description: Complete command reference for the dotsider CLI.
---

## Synopsis

```
dotsider <assembly.dll|.exe>    # TUI — interactive assembly explorer
dotsider <package.nupkg>        # TUI — browse NuGet package contents
dotsider diff <left> <right>    # TUI — assembly comparison, or AOT size diff for mstat inputs

dotsider analyze <file> [opts]  # CLI — headless analysis
dotsider size-check <target>    # CLI — AOT size regression report and CI budget gate
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
dotsider analyze MyAotApp.exe --disasm 'Program.<Main>$'  # disassemble a native function by name
dotsider analyze MyAotApp.exe --disasm 0x140001300        # disassemble a native function by address
```

If the file is a native apphost with a companion `.dll`, `analyze` auto-redirects. If it's a self-contained single-file bundle, `analyze` extracts the entry assembly from the bundle. Both cases print a note to stderr.

If the file is a Native AOT executable (a validated ReadyToRun header with no CLR metadata), the default output adds `Kind`, `RTR Format`, `Runtime`, `Imports`, `R2R`, `Recovered`, and `Frozen` lines, and JSON output gains `binaryKind`, `nativeAotInfo`, `readyToRunSections`, `recoveredTypeCount`, and `frozenStringCount`. `--types` falls back to the types recovered from the embedded metadata, `--strings` adds the raw ASCII and UTF-16 scans plus the frozen string literals, since AOT binaries have no metadata string heaps.

With the ILC sidecars next to the binary (publish with `IlcGenerateMstatFile` and `IlcGenerateDgmlFile`, then copy the `.mstat` and `.codegen.dgml.xml` out of `obj/.../native/`), `--size` prints the compiler's own per-assembly breakdown with the binary's data categories (without an mstat it falls back to the binary's native symbols), `--symbols` lists native symbols with their provenance — the platform's PDB, `.dbg`, or dSYM, or unwind-data boundaries when none exists, `--deps` shows the compiled-in assemblies and native import modules, and `--why <name>` prints the dependency chain that kept a type or method in the binary — root first, one step per line with the compiler's reason. Names match exactly first, then by unambiguous substring.

When portable PDB data is available, default output reports where it came from, and `--il` includes source spans, local names, and Source Link markers. Use `--json` when you need the exact URLs. `--embedded-source` prints source embedded in the PDB.

`--disasm <name-or-0xVA>` disassembles one native function of a Native AOT (or other native) binary to real x86-64 or AArch64 assembly, resolving call and branch targets to names (`call Foo`, `Foo+0x12`, intra-function `loc_…` labels), RIP-relative loads to the referenced data symbol, and indirect calls through the import table to `MODULE!Function`. Identify the function by an exact managed name, its raw symbol name, a suffix, or a hex virtual address; an ambiguous name lists the candidates and exits non-zero. `--json` carries the structured operands and per-instruction metadata. A managed assembly (no native symbols) exits 1. For a ReadyToRun image, `--disasm <method>` resolves to the method and renders all its code ranges (hot, funclets, cold) as one body.

A ReadyToRun (crossgen2) image keeps its full metadata and adds precompiled native bodies. The default output reports a `ReadyToRun` line (version, status, architecture, composite/component). `--r2r-correlate` with no argument prints the precompiled-method stats; with a `Type.Method`, a `0x06…` token, or a `0x…` native address it prints the method's IL beside its native code ranges, resolving call targets through the import tables. An overloaded name lists the candidates and exits non-zero. A composite `*.r2r.dll` resolves its component assemblies by name and MVID from the siblings beside it; a component DLL routes its native code to the owner composite. `--json` carries the structured per-range IL and native arrays.

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
| `--disasm <name-or-0xVA>` | Disassemble a native function to assembly (Native AOT and other native binaries) |
| `--why <name>` | Explain why a type or method is in a Native AOT binary |
| `--r2r-correlate [name-or-0xVA]` | ReadyToRun stats, or a method's IL beside its precompiled native code |
| `--bundle` | Show single-file bundle manifest |
| `--json` | Output as JSON |
| `-o`, `--output <file>` | Write output to a file |

## `dotsider diff`

Compares two inputs side by side. The input kinds decide the mode:

- **Two managed assemblies** open the metadata diff TUI (types, methods, references) — see
  [Diff Mode](/usage/diff-mode/).
- **Two mstat-backed inputs** — bare `.mstat` size reports, or Native AOT binaries with mstat
  sidecars beside them — open the size-diff TUI (Summary + delta treemap). With the global
  `--json` flag the TUI is skipped and the machine-readable size-diff document prints instead.
- **A mixed pair** (mstat-backed against anything else) is an error and exits 1: the two
  sides would measure different things.

```
dotsider diff v1.dll v2.dll                      # metadata diff TUI
dotsider diff before.mstat after.mstat           # AOT size-diff TUI
dotsider diff bin/v1/publish/app bin/v2/publish/app   # same, via mstat sidecars
dotsider diff before.mstat after.mstat --json    # headless size-diff document
```

| Option | Description |
|--------|-------------|
| `-e`, `--escape-timeout <ms>` | Escape key timeout in milliseconds (default 100) |
| `--json` | For mstat-backed pairs: print the size-diff JSON document instead of the TUI |

## `dotsider size-check`

Headless size-regression checking for CI: compares a Native AOT build against a baseline via
their mstat size reports and enforces size budgets. See
[Size Regression](/usage/size-regression/) for the workflow and pipeline recipes.

```
dotsider size-check out/pr/app --baseline baseline/app.mstat --top 20
dotsider size-check out/pr/app --baseline baseline/app.mstat \
  --budget max=25mb --budget growth=1% --budget ns=System.Text.Json:growth=10kb
dotsider size-check out/pr/app --baseline baseline/app.mstat \
  --format markdown --summary-file "$GITHUB_STEP_SUMMARY"
```

The target and baseline are each a bare `.mstat` or a Native AOT binary with the sidecar
beside it. Binaries measure file size on disk; a bare `.mstat` anywhere makes both sides
measure mstat attributable totals — the report always states which basis applied. Namespace
and assembly budgets always measure mstat aggregates, with frozen objects attributed via
their owning type and ownerless bytes (string literals) in an explicit `(unattributed)`
bucket.

Budgets use the grammar `[scope:]limit(,limit)*` with scope `total` (default), `ns=<Namespace>`
(covers sub-namespaces), or `asm=<Assembly>`, and limits `max=SIZE` and `growth=SIZE|PERCENT`
(sizes like `25mb`, `10kb`, `4096`; 1 kb = 1024). Growth limits need `--baseline`; `max=`
works without one. `--budget-file` adds a JSON document whose entries are spec strings or
objects (`name`, `description`, `scope`, `max`, `growth`, `severity`, `topN`) — `severity:
"warning"` reports a breach without failing the gate.

| Option | Description |
|--------|-------------|
| `--baseline <file>` | Baseline binary or `.mstat` to diff against |
| `--budget <spec>` | Size budget in the grammar above; repeatable |
| `--budget-file <file>` | JSON budgets document (string and object entries) |
| `--top <n>` | Top contributors per section and per violated budget (default 10) |
| `--why` | Attach ILC dependency chains for top added contributors (needs the target's DGML sidecar) |
| `--format <text\|json\|markdown>` | Output format (default text; `--json` ≡ `--format json`) |
| `--summary-file <file>` | Additionally write the markdown report to a file (e.g. `$GITHUB_STEP_SUMMARY`) |
| `-o`, `--output <file>` | Write output to a file |

| Exit code | Meaning |
|-----------|---------|
| 0 | Report produced; every error-severity budget passed |
| 1 | Usage or input error |
| 2 | An error-severity budget was exceeded |

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
