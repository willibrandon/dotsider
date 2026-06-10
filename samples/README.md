# Samples

Sample .NET projects used as test fixtures for dotsider's analysis, diff, and tracing features. Each project targets a specific testing scenario.

| Sample | Type | Purpose |
|--------|------|---------|
| [HelloWorld](HelloWorld/) | Console app | Basic analysis and runtime tracing (GC events, exceptions, overloaded methods) |
| [ComplexApp](ComplexApp/) | Console app | Embedded resources, pipeline pattern, versioned assembly metadata |
| [EmptyLib](EmptyLib/) | Library | Minimal metadata edge cases (near-empty assembly) |
| [MinimalApi](MinimalApi/) | ASP.NET Web | Web SDK analysis, middleware, record types, route endpoints |
| [NativeLib](NativeLib/) | Library | P/Invoke, unsafe code, pointer arithmetic, fixed buffers |
| [RichLibrary](RichLibrary/) | Library (NuGet) | Feature-rich baseline: generics, attributes, extension methods, dual JSON serializers |
| [RichLibraryV2](RichLibraryV2/) | Library | Breaking changes from RichLibrary v1 for assembly diff testing |
| [NetFxConsole](NetFxConsole/) | Console app (.NET Fx) | .NET Framework 4.8 target for Dynamic tab guard testing |
| [NativeAotConsole](NativeAotConsole/) | Console app (NativeAOT) | NativeAOT-published binary for Dynamic tab tracing tests |
| [Dotted.Name.App](Dotted.Name.App/) | Console app | Dotted assembly name for cross-platform apphost detection testing |
| [SelfContainedConsole](SelfContainedConsole/) | Console app (single-file) | Self-contained single-file bundle for bundle reading and resolution testing |
| [AppLocalRollForward](AppLocalRollForward/) | Library | Reproduces the AppLocal framework-PKT roll-forward scenario: TraceEvent's stale `AssemblyRef` to `Microsoft.Diagnostics.NETCore.Client v0.2.10.10501` paired with a higher app-local deployment (`v0.2.13.11903`) under the same well-known framework PKT |
| [EmbeddedSourceLib](EmbeddedSourceLib/) | Library | Embedded portable PDB source fixture for source navigation and CLI extraction tests |
| [NetFxBindingRedirects](NetFxBindingRedirects/) | Console app (.NET Fx) | Runtime oracle for `NetFxBinder`: GAC + framework runtime + binding redirects + `<probing privatePath>` + `<codeBase>` (success and missing) + culture-aware probing |
| [NetFxBindingRedirects.OldDep](NetFxBindingRedirects.OldDep/) | Library (.NET Fx) | Compiled against Newtonsoft.Json 12.0.1 — drives transitive binding-redirect tests |
| [NetFxBindingRedirects.NewDep](NetFxBindingRedirects.NewDep/) | Library (.NET Fx) | Compiled against Newtonsoft.Json 13.0.3 — pairs with OldDep to prove two requested versions collapse to one bound graph node |
| [NetFxBindingRedirects.PrivatePathLib](NetFxBindingRedirects.PrivatePathLib/) | Library (.NET Fx) | Deployed under `bin\…\lib\` and reached only via `<probing privatePath="lib">` |
| [NetFxBindingRedirects.CodeBaseLib](NetFxBindingRedirects.CodeBaseLib/) | Library (.NET Fx, strong-named) | Deployed under `bin\…\external\` and reached only via configured `<codeBase href>` |
| [NetFxBindingRedirects.CulturedLib](NetFxBindingRedirects.CulturedLib/) | Library (.NET Fx) | Neutral + French satellite resources for culture-aware binder probing |
| [NetFxBindingRedirects.Clr2](NetFxBindingRedirects.Clr2/) | Console app (.NET Fx 3.5 / CLR 2) | Runtime oracle for `NetFxBinder`'s CLR 2.0 path: `%WINDIR%\assembly\GAC*` (no `v4.0_` prefix), framework runtime `v2.0.50727`, `appliesTo="v2.0.50727"` redirects, `<probing privatePath>` + `<codeBase>` (success and missing) + culture-aware probing. `<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>` reproduces the no-TFA bug shape from issue #158 |
| [NetFxBindingRedirects.Clr2.SharedDep.V1](NetFxBindingRedirects.Clr2.SharedDep.V1/) | Library (.NET Fx 3.5, strong-named) | Same-name/same-key dependency at `AssemblyVersion=1.0.0.0` — paired with the V2 sibling to drive the bindingRedirect collapse test |
| [NetFxBindingRedirects.Clr2.SharedDep.V2](NetFxBindingRedirects.Clr2.SharedDep.V2/) | Library (.NET Fx 3.5, strong-named) | Same identity as V1 at `AssemblyVersion=2.0.0.0` — the redirect target staged app-local |
| [NetFxBindingRedirects.Clr2.UsesSharedV1](NetFxBindingRedirects.Clr2.UsesSharedV1/) | Library (.NET Fx 3.5) | Holds a metadata reference to SharedDep v1.0.0.0 (`<Private>false</Private>` so it isn't propagated). The runtime oracle calls its accessor to force a transitive bind through this edge |
| [NetFxBindingRedirects.Clr2.UsesSharedV2](NetFxBindingRedirects.Clr2.UsesSharedV2/) | Library (.NET Fx 3.5) | Mirror of UsesSharedV1, referencing SharedDep v2.0.0.0 — both accessors bind to the same loaded V2 assembly |
| [NetFxBindingRedirects.Clr2.PrivatePathLib](NetFxBindingRedirects.Clr2.PrivatePathLib/) | Library (.NET Fx 3.5) | Deployed under `bin\…\lib\` and reached only via `<probing privatePath="lib">` |
| [NetFxBindingRedirects.Clr2.CodeBaseLib](NetFxBindingRedirects.Clr2.CodeBaseLib/) | Library (.NET Fx 3.5, strong-named) | Deployed under `bin\…\external\` and reached only via configured `<codeBase href>` |
| [NetFxBindingRedirects.Clr2.CulturedLib](NetFxBindingRedirects.Clr2.CulturedLib/) | Library (.NET Fx 3.5) | Neutral + French satellite for CLR 2 culture-aware probing. Satellite is built via the v3.5 framework `csc.exe` (the SDK's `al.exe` only emits CLR4 metadata); the build target skips on hosts without the legacy framework toolchain |

All managed samples target .NET 10 with nullable reference types enabled unless noted otherwise.
The `NetFxBindingRedirects*` projects build only on Windows: the six original projects target `net48` (CLR 4); the eight `NetFxBindingRedirects.Clr2*` siblings target `net35` (CLR 2). Both cohorts compile against the `Microsoft.NETFramework.ReferenceAssemblies` package so neither requires a Windows SDK install; the CLR 2 satellite-build step further requires .NET Framework 3.5 to be enabled on the build host (skipped cleanly otherwise).
