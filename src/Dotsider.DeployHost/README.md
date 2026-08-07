# Dotsider Deploy Host

Dotsider Deploy Host is the self-contained Native AOT helper used to provision
and maintain dotsider.dev. It replaces the server-side deployment scripts and
does not require a .NET runtime on the Debian host.

The `scripts/Deploy-Website.cs` file-based app publishes the Linux executable,
uploads it over SSH, and invokes one of these commands:

| Command | Purpose |
| --- | --- |
| `provision` | Install packages, the deployment account, directories, configuration, services, timers, and firewall rules. |
| `preflight` | Report whether the host is ready for deployment without changing it. |
| `activate` | Install the helper and configuration, refresh sample recovery data, restart the website, and verify health. |
| `report` | Append the five Caddy metrics read from Prometheus. |
| `integrity` | Verify the sample manifest and restore the sample backup after corruption. |

The installed executable is `/usr/local/libexec/dotsider-deploy-host`, owned by
`root:root`, and not writable by the deployment account. Systemd runs `report`
and `integrity` from that location.

Configuration under `deploy/` is embedded at publish time. The install manifest
restricts every destination, owner, group, and mode. Caddy, Prometheus, and
systemd candidates are validated before installed files are replaced.

External tools are launched with `ProcessStartInfo.ArgumentList` and shell
execution disabled. Privileged commands use fixed paths and fixed service names.

Build the managed project with:

```console
dotnet build src/Dotsider.DeployHost/Dotsider.DeployHost.csproj
```

Publish the production Linux executable through the repository utility:

```console
dotnet run --file scripts/Deploy-Website.cs -- -Mode Package -DeployHost publish/deploy-host/dotsider-deploy-host
```
