# AppLocalRollForward

Library that reproduces the AppLocal framework-PKT roll-forward scenario for the dependency-graph resolver. Used as a regression fixture for `AssemblyAnalyzer.IsFrameworkRollForwardMatch`.

- Transitive `Microsoft.Diagnostics.Tracing.TraceEvent 3.2.2` carries a stale `AssemblyRef` to `Microsoft.Diagnostics.NETCore.Client v0.2.10.10501` (PKT `31bf3856ad364e35`)
- Direct `PackageReference` floats `Microsoft.Diagnostics.NETCore.Client` to `0.2.661903` so NuGet deploys the higher build (`v0.2.13.11903`) app-local
- `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` ensures the package assembly lands next to the library output
- The dep graph must collapse onto a single resolved node keyed at the deployed version with the older requested identity stamped on the edge — no `IdentityMismatch` leaf
