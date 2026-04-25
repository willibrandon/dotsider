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
| [NetFxBindingRedirects](NetFxBindingRedirects/) | Console app (.NET Fx) | Runtime oracle for `NetFxBinder`: GAC + framework runtime + binding redirects + `<probing privatePath>` + `<codeBase>` (success and missing) + culture-aware probing |
| [NetFxBindingRedirects.OldDep](NetFxBindingRedirects.OldDep/) | Library (.NET Fx) | Compiled against Newtonsoft.Json 12.0.1 — drives transitive binding-redirect tests |
| [NetFxBindingRedirects.NewDep](NetFxBindingRedirects.NewDep/) | Library (.NET Fx) | Compiled against Newtonsoft.Json 13.0.3 — pairs with OldDep to prove two requested versions collapse to one bound graph node |
| [NetFxBindingRedirects.PrivatePathLib](NetFxBindingRedirects.PrivatePathLib/) | Library (.NET Fx) | Deployed under `bin\…\lib\` and reached only via `<probing privatePath="lib">` |
| [NetFxBindingRedirects.CodeBaseLib](NetFxBindingRedirects.CodeBaseLib/) | Library (.NET Fx, strong-named) | Deployed under `bin\…\external\` and reached only via configured `<codeBase href>` |
| [NetFxBindingRedirects.CulturedLib](NetFxBindingRedirects.CulturedLib/) | Library (.NET Fx) | Neutral + French satellite resources for culture-aware binder probing |

All managed samples target .NET 10 with nullable reference types enabled unless noted otherwise.
The six `NetFxBindingRedirects*` projects target `net48` and build only on Windows.
