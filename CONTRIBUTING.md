# Contributing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git
- A terminal with decent ANSI support

## Setup

```
git clone https://github.com/willibrandon/dotsider.git
cd dotsider
dotnet build
dotnet test
```

First test run is slower — the test setup prepares the 38-project sample matrix and restores its NuGet packages. Platform-specific fixtures are built where supported, and subsequent runs use cache.

## Project layout

The README covers the full structure. The short version:

- `src/Dotsider.Core/Analysis/` — analyzers. Each one reads an assembly and returns a model. No UI concerns here.
- `src/Dotsider/Views/` — one file per tab. Each view builds a widget tree every frame; Hex1b reconciles it against the previous node tree.
- `src/Dotsider/DotsiderState.cs` — all mutable UI state lives in one place.
- `src/Dotsider.Mcp/` — standalone MCP server. Shares `Dotsider.Core` with the TUI but runs as its own process.
- `tests/Dotsider.Tests/` — integration tests against real assemblies.
- `tests/Dotsider.Mcp.Tests/` — MCP tool and prompt tests.
- `benchmarks/Dotsider.Benchmarks/` — BenchmarkDotNet harness for the core analyzers.

## Code conventions

**C# 14 / .NET 10.** Nullable reference types are enabled everywhere. The `.editorconfig` enforces style rules as warnings — a clean build has zero warnings.

A few rules that aren't in the editorconfig:

- One type per file. Interfaces, enums, records — each gets its own file.
- Every public member needs `///` XML doc comments. The projects generate documentation files, so undocumented public APIs produce CS1591 warnings.
- Never suppress warnings with `#pragma`. Fix the root cause.
- Prefer composition over inheritance. Most views are just functions that return widget trees.

## Testing

Tests use MSTest on Microsoft Testing Platform. They exercise compiler-produced assemblies alongside focused synthetic images for malformed-input and boundary cases. `SampleAssemblyHost` initializes the shared `SampleAssemblyFixture` once for the test assembly.

```
dotnet test                                             # everything
dotnet test --filter "FullyQualifiedName~IlDisassembler" # one class
dotnet test --verbosity normal                           # see individual test names
```

CI runs on Ubuntu, Windows, and macOS. If it passes locally on one OS but fails on another, the most common culprits are path separators and line endings.

## Benchmarks

```
dotnet run -c Release --project benchmarks/Dotsider.Benchmarks
```

If you're changing anything in `Dotsider.Core/Analysis/`, run the relevant benchmark before and after to make sure performance doesn't regress.

## Commit messages

Format: `type(scope): description`

```
feat(yank): add vim text objects iw/iW/yiw/yiW
fix(dynamic): detect .NET Framework and allow NativeAOT tracing
test(diff): add decoration provider coverage
docs(readme): fix sessions capture usage
chore(release): bump version to 0.6.0
```

Common types: `feat`, `fix`, `test`, `docs`, `chore`, `ci`, `refactor`, `perf`.

The scope is usually the tab or subsystem: `yank`, `dynamic`, `il-inspector`, `hex-dump`, `diff`, `nupkg`, `mcp`, `demo`, `release`.

## Pull requests

1. Branch from `main`.
2. Keep the diff focused — one concern per PR.
3. Make sure `dotnet build` produces zero warnings and `dotnet test` passes.
4. Open a PR with a clear title and description. Reference the issue if there is one.
5. CI must be green on all three platforms before merge.
6. PRs are squash-merged and the branch is deleted afterward.

### Labels

Apply whichever labels fit. The full set:

`accessibility`, `bug`, `ci`, `cli`, `demo`, `deploy`, `diff`, `documentation`, `enhancement`, `infrastructure`, `input`, `mcp`, `navigation`, `nupkg`, `observability`, `packaging`, `performance`, `release`, `safety`, `search`, `testing`, `theme`, `ux`, `windows`

## Reporting issues

Include:

1. What happened vs. what you expected.
2. Steps to reproduce, ideally with a specific assembly or a sample project.
3. .NET version (`dotnet --version`), OS, and terminal.
4. If it's a rendering issue, a screenshot or recording helps.

## License

Contributions are under the [MIT license](LICENSE), same as the project.
