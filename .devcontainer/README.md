# Development container

Open the repository in Visual Studio Code and run **Dev Containers: Reopen in
Container**. The first creation restores the .NET and documentation dependencies
and installs the Hex1b CLI.

The container includes the .NET 10 SDK, Native AOT prerequisites, the
`wasm-tools` workload, Node.js, pnpm, GitHub CLI, LLVM, and an isolated Docker
daemon. The Hex1b CLI is installed at the version used by dotsider and is
available through the `hex1b` command.

Docker-in-Docker requires the development container to run privileged. It uses
its own Docker storage and does not mount the host Docker socket.

MSBuild outputs use `bin/devcontainer` and `obj/devcontainer`, while test
fixtures use dedicated container configurations. Windows and Linux builds do
not share generated files, and existing host build outputs are left alone.

The normal build and test workflow is unchanged:

```console
dotnet build
dotnet test
```

Run the deployment integration tests explicitly:

```console
DOTSIDER_RUN_DEPLOY_INTEGRATION=1 dotnet test tests/Dotsider.Deploy.Tests/Dotsider.Deploy.Tests.csproj
```

The container provides the Linux development environment. Windows-specific,
macOS-specific, and cross-platform release coverage remains in CI. CI also
scans the built container image for secrets with Picket.
