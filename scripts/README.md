# Scripts

dotsider repository utilities are .NET file-based apps. They require the .NET 10
SDK.

The implementation follows the Microsoft file-based app guidance:

https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

Run a utility with:

```powershell
dotnet run --file ./scripts/Capture-DisasmOracle.cs -- -Architecture riscv64 -Fixture path/to/blob.bin -OraclePath llvm-objdump -OutputDirectory artifacts/oracles/disasm -- -D -b binary -m riscv:rv64 path/to/blob.bin
```

Large disassembly tools can emit very large streams. Use
`-MaxOutputCharacters` to cap retained stdout/stderr while still draining the
process to completion, and `-AllowOracleFailure` when the capture should upload
diagnostics from a failing oracle tool instead of failing before artifacts are
saved.

For automation or repeated invocations, build first and run without rebuilding:

```powershell
dotnet build ./scripts/Capture-DisasmOracle.cs --nologo --verbosity quiet
dotnet run --file ./scripts/Capture-DisasmOracle.cs --no-build -- -Architecture riscv64 -Fixture path/to/blob.bin -OraclePath llvm-objdump -OutputDirectory artifacts/oracles/disasm -- -D -b binary -m riscv:rv64 path/to/blob.bin
```

The build-first pattern avoids file-based app cache contention when multiple
processes might run the same utility at the same time.

If the SDK cache gets stale or contested, clear all file-based app cache output:

```powershell
dotnet clean file-based-apps
```

To force a clean build for one utility:

```powershell
dotnet clean ./scripts/Capture-DisasmOracle.cs
dotnet build ./scripts/Capture-DisasmOracle.cs
```

The executable utilities have a Unix shebang:

```text
#!/usr/bin/env -S dotnet --
```

The repository uses LF line endings through `.gitattributes`, which is required
for direct Unix execution. On Unix-like systems, executable files can also be run
directly after checkout when the executable bit is present:

```bash
./scripts/Capture-DisasmOracle.cs -Architecture riscv64 -Fixture path/to/blob.bin -OraclePath llvm-objdump -OutputDirectory artifacts/oracles/disasm -- -D -b binary -m riscv:rv64 path/to/blob.bin
```

Keep file-based apps under `scripts/`, outside project directories. The local
`scripts/Directory.Build.props` intentionally isolates utility app settings from
package metadata and project settings used by the shipped dotsider projects.

Repository tests build every executable utility app with `dotnet build` and run
stable fake-input coverage for the disassembly oracle capture app. When adding a
utility, keep its top-level launcher thin and put behavior in a documented app
class or documented helper methods so XML documentation and convention tests
cover hoverable code.

dotsider decoder tests prefer real sample assemblies over hand-written byte
fixtures. The shared test fixture publishes cross-RID ReadyToRun samples for
architecture-specific coverage when the SDK provides the RID packs. Use
`Capture-DisasmOracle.cs` for architectures that require runtime-built inputs or
external oracle captures, then review and promote the normalized artifacts into
committed fixtures.

Normal `dotnet publish` cannot always produce every decoder target on every
machine. Current public SDK feeds may not include runtime/host packs for
`linux-riscv64` or `linux-loongarch64`; those ReadyToRun fixture paths are null
when restore cannot resolve the packs. Browser and WASI Wasm do not support
ReadyToRun publishing; the browser-wasm fixture publishes with
`PublishReadyToRun=false` and decodes the real SDK-produced
`dotnet.native.wasm` module instead. If a runtime-built input or an external
oracle is needed, capture it with the file-based utility and commit the reviewed
fixture metadata, including the runtime source files used as ground truth.
The .NET runtime repo validates RISC-V64 and LoongArch64 through its
cross-target runtime pipeline and SuperPMI/crossgen2 collections, so dotsider CI
does not try to build private runtime packs as part of normal test execution.

The `Native architecture oracles` workflow is the outer-loop capture pipeline.
It can be run manually from GitHub Actions and also refreshes SDK-backed oracle
captures on a weekly schedule. The default path installs `wasm-tools`, publishes
the browser-wasm fixture, opportunistically tries SDK ReadyToRun publishes for
`linux-riscv64` and `linux-loongarch64`, captures any available oracle output
with `Capture-DisasmOracle.cs`, and uploads the unreviewed artifacts.

When public SDK packs are unavailable, run the workflow manually with
`run-runtime-cross-target=true`. That opt-in path checks out `dotnet/runtime` at
the requested ref and uses the same pinned Azure Linux cross-build containers
and rootfs paths that runtime uses for RISC-V64 and LoongArch64. The resulting
runtime logs and artifact locations are uploaded for review; promoted fixtures
should still flow through `Capture-DisasmOracle.cs` so every committed oracle
records the producer ref, fixture hash, and command line.

For local development, the same shape applies:

```powershell
$env:DOTSIDER_RUNTIME_ROOT = "D:\SRC\runtime"
dotnet build ./scripts/Capture-DisasmOracle.cs --nologo --verbosity quiet
dotnet run --file ./scripts/Capture-DisasmOracle.cs --no-build -- `
  -Architecture loongarch64 `
  -Fixture artifacts/oracles/input/loongarch64-smoke.bin `
  -OraclePath llvm-objdump `
  -RuntimeRoot $env:DOTSIDER_RUNTIME_ROOT `
  -OutputDirectory artifacts/oracles/disasm `
  -- -d artifacts/oracles/input/loongarch64-smoke.bin
```

Current utilities:

| App | Purpose |
| --- | --- |
| `Capture-DisasmOracle.cs` | Capture external native-disassembly oracle output and metadata. |

Oracle captures stay under ignored `artifacts/` paths until they have been
reviewed and normalized into committed test fixtures.
