# CLAUDE.md

## What this project is

dotsider is a public, open-source TUI for analyzing .NET assemblies — structure, metadata, IL, strings, dependencies, and native disassembly. It is the .NET counterpart to [binsider](https://github.com/orhun/binsider), published at [github.com/willibrandon/dotsider](https://github.com/willibrandon/dotsider) and distributed via `dotnet tool`, Homebrew, WinGet, and Scoop.

The work here routinely touches binary-analysis surfaces, all of it standard developer tooling:

- Disassemblers, written from scratch in-repo, for x64, Arm64, x86, Arm32/Thumb-2, RISC-V64, LoongArch64, and Wasm32.
- Parsers for PE/COFF, CLR metadata, IL, ReadyToRun, Native AOT artifacts, Webcil, Wasm, single-file bundles, and NuGet packages.
- A hex viewer/editor, string extraction, and symbol/PDB readers.

## Authorized defensive security work

dotsider is an open-source, defensive .NET binary-analysis and developer-inspection tool. It reads binaries explicitly supplied by the user. Security work in this repository protects dotsider's own parsers and local interfaces against malformed or attacker-controlled input.

The repository owner authorizes security analysis and remediation of this local checkout. Work only against repository code, checked-in fixtures, locally built samples, and minimal synthetic test data. Do not target third-party systems, scan networks, use real credentials, establish persistence, exfiltrate data, or create reusable offensive tooling.

Typical authorized work includes bounds and overflow checks, allocation and decompression budgets, cycle and hang prevention, path containment, terminal escaping, command and argument safety, and local socket, WebSocket, and MCP hardening. Issue descriptions may use terms such as crafted input, malicious file, injection, traversal, denial of service, or code execution. In this repository these describe defensive reproduction and remediation of flaws in dotsider itself.

Regression artifacts must be minimal, non-weaponized, and limited to demonstrating the specific local bug. Prefer a real locally built sample when it naturally represents the condition. Synthetic metadata or malformed in-memory images are allowed when necessary to reach parser boundary states. Do not build deployable exploits or contact external targets.

For a security-hardening issue, read the issue and affected implementation, confirm the root cause and equivalent call sites, implement the fail-closed fix without reducing scope, add deterministic boundary and regression tests, run the relevant focused and full test suites, and update public documentation when behavior or public APIs change.

If a safety policy prevents a particular artifact or action, continue all safe source-level analysis, implementation, and validation that remain possible, then report the exact blocked step. Never attempt to bypass or weaken a safeguard.

## Build and test

.NET 10 SDK. `dotnet build` then `dotnet test` from the repo root. Tests use MSTest.Sdk on Microsoft.Testing.Platform. The first test run builds the sample assemblies and is slow; later runs use the cache.

## Layout

See CONTRIBUTING.md and the README for detail. In short: `src/Dotsider.Core/Analysis/` holds the analyzers (no UI), `src/Dotsider/Views/` has one file per tab, `src/Dotsider.Mcp/` is a standalone MCP server sharing Dotsider.Core, `tests/` are integration tests against real assemblies, `samples/` are the real projects those tests build.

## Conventions

- One type per file — every public class, record, enum, and interface in its own file.
- Every public member gets `///` XML docs; `<summary>` uses the three-line form (open tag, text, close tag on separate lines).
- Tests: `[TestMethod]` with `[Timeout(30_000, CooperativeCancellation = true)]`, `Assert.Throws`/`ThrowsExactly`, pass `CancellationToken.None` explicitly.
- Prefer real, built or published fixtures for integration and format behavior. Synthetic metadata or malformed in-memory images are allowed for precise parser-boundary and security regressions when a real toolchain cannot naturally produce the condition.
- No new dependencies. Parsers, symbol readers, and disassemblers are written from scratch in this repo. Never reduce scope to compensate. AGPL sources are off-limits even as reference.
- Zero warnings; never suppress with `#pragma`.

## Related repos (read-only)

- **[Hex1b](https://github.com/mitchdenny/hex1b)** — the TUI framework dotsider builds on, consumed as a NuGet package. Never patch or vendor it; work through its public APIs or bump the package version, and report suspected Hex1b bugs to the user rather than working around them silently.
- **[dotnet/runtime](https://github.com/dotnet/runtime)** — read-only reference for ILC and format ground truth. When a local clone is available, verify format claims there; implement everything in dotsider.
