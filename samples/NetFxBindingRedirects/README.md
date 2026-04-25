# NetFxBindingRedirects

Six-project net48 fixture used by `tests/Dotsider.Tests/NetFxBinderTests.cs`,
`NetFxBindingContextTests.cs`, `NetFxBinderOracleTests.cs`, and the binder regression
tests in `DependencyGraphBuilderTests.cs`. Targets `net48` so it must build on Windows.

The root EXE plus its post-build orchestration produces a single `bin\Debug\net48\`
layout that exercises every code path in dotsider's CLR-accurate `NetFxBinder`:

| Path                                                      | Exercises                                                                  |
|-----------------------------------------------------------|----------------------------------------------------------------------------|
| `NetFxBindingRedirects.exe`                               | Root identity                                                              |
| `NetFxBindingRedirects.exe.config`                        | `<bindingRedirect>`, `<probing privatePath>`, `<codeBase>` (success + missing) |
| `Newtonsoft.Json.dll` (13.0.3)                            | App-local hit after Newtonsoft 12 → 13 binding redirect                    |
| `NetFxBindingRedirects.OldDep.dll` (refs Newtonsoft 12)   | Transitive redirect (root policy applied to refs from child DLL)           |
| `NetFxBindingRedirects.NewDep.dll` (refs Newtonsoft 13)   | Same loaded identity as OldDep's bound Newtonsoft — collapses to one node  |
| `lib\NetFxBindingRedirects.PrivatePathLib.dll`            | `<probing privatePath="lib"/>` rooted at the EXE's app base                |
| `external\NetFxBindingRedirects.CodeBaseLib.dll`          | `<codeBase href="external/..."/>` resolution                               |
| `external\Missing.dll` (intentionally absent)             | `<codeBase>` fail-fast → `Provenance.CodeBaseMissing`                      |
| `CulturedLib.dll` + `fr\CulturedLib.resources.dll`        | Culture-aware probing: app-base + private-path + culture sub-directory     |
| GAC-only references (`System.Drawing`, `System.Windows.Forms`) | GAC scan in CLR architecture order                                       |
| Framework runtime directory (`mscorlib`, `System`)        | `Framework[64]\v4.0.30319` probe                                           |

## Runtime oracle

`NetFxBindingRedirects.exe --oracle <path-to-json>` writes a JSON map of
`Assembly.FullName` and `Assembly.Location` for every load above. The dotsider test
fixture runs this once per session and uses the JSON as ground truth: any divergence
between the binder's `NetFxBindResult.Loaded` and the oracle entry is a bug.

The publisher-policy oracle test (`Bind_PublisherPolicy_OracleParity_RequiresAdmin`)
elevates further by installing a fixture-emitted synthetic policy assembly into the
real GAC via `gacutil`. That test skips when not running elevated.
