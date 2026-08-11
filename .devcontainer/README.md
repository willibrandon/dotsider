# Development container

Open the repository in Visual Studio Code and run **Dev Containers: Reopen in
Container**. The first creation restores the .NET and documentation dependencies
and installs the Hex1b CLI.

The container includes the .NET 10 SDK, Native AOT prerequisites, the
`wasm-tools` workload, Node.js 24, pnpm, GitHub CLI, LLVM, and an isolated Docker
daemon. The Hex1b CLI is installed at the version used by dotsider and is
available through the `hex1b` command.

Docker-in-Docker requires the development container to run privileged. It uses
its own Docker storage and does not mount the host Docker socket.

MSBuild outputs use `bin/devcontainer` and `obj/devcontainer`, while test
fixtures use dedicated container configurations. Windows and Linux builds do
not share generated files, and existing host build outputs are left alone.

The normal build and test workflow is unchanged:

```console
dotnet build Dotsider.slnx
dotnet test --solution Dotsider.slnx
```

GitHub Action and Azure Pipelines development uses the same pinned pnpm workspaces restored
during container creation:

```console
pnpm --dir integrations/size-check build
pnpm --dir integrations/size-check test:unit
pnpm --dir integrations/size-check test:integration:local
pnpm --dir integrations/size-check validate
pnpm --dir azure-devops package:vsix
dotnet run --file ./scripts/Validate-CiIntegrations.cs -- -Vsix artifacts/azure-devops/willibrandon.dotsider-0.1.0.vsix
```

The committed runtime is plain `tsc` output split into small modules. Validation rejects
bundled output, oversized generated files, unexpected runtime files, and `node_modules` in
the VSIX.

Integrated terminals limit .NET to four processors and disable reusable MSBuild
nodes so full builds and tests fit within the container's 8 GB memory budget.

Run the deployment integration tests explicitly:

```console
DOTSIDER_RUN_DEPLOY_INTEGRATION=1 dotnet test tests/Dotsider.Deploy.Tests/Dotsider.Deploy.Tests.csproj
```

The container provides the Linux development environment. Windows-specific,
macOS-specific, and cross-platform release coverage remains in CI. CI also
scans the built container image for secrets with Picket.
