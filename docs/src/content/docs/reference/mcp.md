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

**28 tools** across:

- Assembly analysis
- IL disassembly
- Metadata inspection
- Dependency graphs
- Size analysis
- String extraction
- Diffing
- NuGet package analysis
- Runtime tracing

Tools work in two modes:

- **Direct mode** — pass an assembly path, get results (no TUI needed)
- **Session mode** — connect to a running dotsider TUI via Unix domain socket for live state, tracing, and navigation

## Guided prompts

**4 prompts** for common workflows:

| Prompt | Purpose |
|--------|---------|
| Security audit | Analyze an assembly for security concerns |
| API surface review | Map the public API surface |
| Breaking change detection | Compare two versions for breaking changes |
| Dependency health | Assess dependency risk and freshness |

## Generating AI skill files

```
dotsider agent init --ai claude
```

This writes a skill file to the provider's conventional location. Supported providers: `claude`, `gemini`, `copilot`, `cursor-agent`, `opencode`, `codex`, `windsurf`, `kilocode`, `amp`, `qwen`.
