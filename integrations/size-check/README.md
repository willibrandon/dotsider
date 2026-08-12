# Size-check integration development

The GitHub Action and Azure Pipelines task share the TypeScript code in `src`.
Run these commands from the repository root after installing .NET 10, Node.js 24,
and pnpm 10.28.0. The development container already includes them.

Restore the two pinned dependency graphs:

```console
pnpm --dir integrations/size-check install --frozen-lockfile
pnpm --dir azure-devops install --frozen-lockfile
```

Build both adapters, run the fast tests, and validate the committed JavaScript:

```console
pnpm --dir integrations/size-check build
pnpm --dir integrations/size-check test
pnpm --dir integrations/size-check validate
```

The local integration command publishes Dotsider and two real NativeAOT sample
applications for the host RID, creates and verifies a real release-shaped archive,
then executes both adapters. It does not substitute an executable or report.

```console
pnpm --dir integrations/size-check test:integration:local
```

Package and inspect the Azure DevOps extension:

```console
pnpm --dir azure-devops package:vsix
dotnet run --file ./scripts/Validate-CiIntegrations.cs -- -Vsix artifacts/azure-devops/willibrandon.dotsider-0.1.0.vsix
```

CI repeats the real adapter suite on Windows, Linux, and macOS for x64 and ARM64.
It also runs the Azure handler under Node.js 20 to verify the fallback handler. Baseline
lifecycle tests use local provider API servers and real files: they exercise successful-run
selection, first-run deferral, manifest hashing, provenance, token removal, and safe ZIP
extraction without substituting a Dotsider executable or size report.

`examples/github-aot-size.yml` is the reference on-demand workflow. It consumes NativeAOT
size-input artifacts from a normal build and supports both manual dispatch and trusted
`/aot-size` pull-request comments.
