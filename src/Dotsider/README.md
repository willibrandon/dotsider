# Dotsider

.NET assembly analysis TUI and CLI. Inspect types, methods, IL bytecode, dependencies, strings, size breakdowns, and PE metadata — interactively or from scripts.

## Modes

- **TUI mode** — `dotsider MyApp.dll` opens an interactive terminal UI with tabbed navigation
- **Diff mode** — `dotsider diff Left.dll Right.dll` compares two assemblies side-by-side
- **NuGet mode** — `dotsider MyPackage.nupkg` inspects package contents and embedded assemblies
- **CLI mode** — `dotsider analyze`, `dotsider sessions`, `dotsider agent` for headless scripting

## CLI Commands

### analyze

Headless assembly analysis. Defaults to assembly info; use flags to select output.

```
dotsider analyze MyApp.dll                  # assembly info
dotsider analyze MyApp.dll --types          # type definitions
dotsider analyze MyApp.dll --methods        # method definitions
dotsider analyze MyApp.dll --il Type.Method # disassemble a method
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

Compare two assemblies in an interactive TUI.

```
dotsider diff Left.dll Right.dll
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
