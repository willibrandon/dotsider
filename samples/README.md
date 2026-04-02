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

All managed samples target .NET 10 with nullable reference types enabled unless noted otherwise.
