# Dotsider

.NET assembly analysis TUI and CLI. Inspect types, methods, IL bytecode, dependencies, strings, size breakdowns, and PE metadata — interactively or from scripts.

## Modes

- **TUI mode** — `dotsider MyApp.dll` opens an interactive terminal UI with tabbed navigation. Tab 3 shows managed IL, native disassembly, or paired IL/native code depending on the loaded image, with portable PDB source spans, local names, and compact Source Link markers when available.
- **Diff mode** — `dotsider diff Left.dll Right.dll` compares two assemblies side-by-side; two mstat-backed inputs (Native AOT) open the size-diff view with a delta treemap instead
- **NuGet mode** — `dotsider MyPackage.nupkg` inspects package contents and embedded assemblies
- **CLI mode** — `dotsider analyze`, `dotsider size-check`, `dotsider sessions`, `dotsider agent` for headless scripting

## CLI Commands

### analyze

Headless assembly analysis. Defaults to assembly info; use flags to select output.

```
dotsider analyze MyApp.dll                  # assembly info
dotsider analyze MyApp.dll --types          # type definitions
dotsider analyze MyApp.dll --methods        # method definitions
dotsider analyze MyApp.dll --il Type.Method # disassemble a method
dotsider analyze MyApp.dll --embedded-source Type.Method # print embedded source
dotsider analyze MyApp.dll --deps           # assembly references
dotsider analyze MyApp.dll --strings        # extract strings
dotsider analyze MyApp.dll --fields         # field definitions
dotsider analyze MyApp.dll --size           # size breakdown
dotsider analyze MyApp.dll --bundle         # single-file bundle manifest
dotsider analyze MyApp.dll --json           # JSON output
dotsider analyze MyApp.dll -o report.txt    # write to file
```

### sessions

Discover and interact with running dotsider TUI instances over Unix domain sockets.

```
dotsider sessions list                      # list running instances
dotsider sessions info <pid>                # assembly info + current view
dotsider sessions view <pid>                # current view state
dotsider sessions navigate <pid> <tab>      # switch to a tab (1-8)
dotsider sessions capture <pid>             # capture screen as text
dotsider sessions trace events <pid>        # trace events
dotsider sessions trace counters <pid>      # performance counters
dotsider sessions trace output <pid>        # process stdout/stderr
dotsider sessions trace start <pid>         # start tracing
dotsider sessions trace stop <pid>          # stop tracing
```

### agent

MCP server and AI skill file management.

```
dotsider agent mcp                          # launch MCP server (dotsider-mcp)
dotsider agent init --stdout                # print skill file to stdout
dotsider agent init --ai claude             # create .claude/skills/dotsider/SKILL.md
dotsider agent init --ai claude --force     # overwrite existing
dotsider agent init --path /tmp/SKILL.md    # write to explicit path
```

### diff

Compare two assemblies in an interactive TUI — or two Native AOT builds by size when both
inputs are mstat-backed (a bare `.mstat` size report or an AOT binary with the sidecar
beside it).

```
dotsider diff Left.dll Right.dll                # metadata diff
dotsider diff before.mstat after.mstat          # AOT size diff (delta treemap)
dotsider diff before.mstat after.mstat --json   # headless size-diff document
```

### size-check

Headless Native AOT size-regression report and CI budget gate. Exit codes: 0 pass, 1 error,
2 budget exceeded.

```
dotsider size-check out/pr/app --baseline baseline/app.mstat
dotsider size-check out/pr/app --baseline baseline/app.mstat \
  --budget total:growth=1% --budget ns=MyApp.Generated:growth=0
dotsider size-check out/pr/app --baseline baseline/app.mstat \
  --format markdown --summary-file "$GITHUB_STEP_SUMMARY"
```

## Global Options

| Option | Description |
|--------|-------------|
| `--json` | Output results as JSON (applies to all subcommands) |

## Installation

```bash
# .NET tool (cross-platform)
dotnet tool install -g dotsider

# Homebrew (macOS/Linux)
brew install willibrandon/tap/dotsider

# WinGet (Windows)
winget install willibrandon.dotsider
```

## TUI Options

```
dotsider MyApp.dll --tab 3        # open on a specific tab
dotsider MyApp.dll --min-len 6    # minimum string length filter
```
