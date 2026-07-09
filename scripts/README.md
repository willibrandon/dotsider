# Scripts

dotsider repository utilities are .NET file-based apps. They require the .NET 10
SDK and follow the Microsoft file-based app guidance:

https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

Run a utility with `dotnet run --file`:

```powershell
dotnet run --file ./scripts/Run-Tests.cs
dotnet run --file ./scripts/Capture-DisasmOracle.cs -- -Architecture riscv64 -Fixture path/to/blob.bin -OraclePath llvm-objdump -OutputDirectory artifacts/oracles/disasm -- -D -b binary -m riscv:rv64 path/to/blob.bin
```

Keep file-based apps under `scripts/`, outside project directories. The local
`scripts/Directory.Build.props` intentionally isolates utility app settings from
package metadata and project settings used by shipped dotsider projects.

The executable utilities start with `#!/usr/bin/env -S dotnet --`, and the
repository enforces LF line endings for script files so direct Unix execution
works when the executable bit is present.

Current utilities:

| App | Purpose |
| --- | --- |
| `Capture-DisasmOracle.cs` | Capture external native-disassembly oracle output and metadata. |
| `Run-Tests.cs` | Run `dotnet test` once or repeatedly with forwarded test arguments. |

Use each script's XML documentation and command-line help for option details.
