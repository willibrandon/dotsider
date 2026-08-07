# Deployment

The dotsider.dev deployment uses a .NET file-based app on the runner and a
self-contained Native AOT helper on Debian. The server does not need a .NET
runtime.

`install-manifest.json` is the authoritative map from the checked-in Caddy,
Prometheus, systemd, timer, and logrotate files to their installed paths. The
helper embeds those files, validates them before installation, and installs
them as `root:root` with mode `0644`.

Package the Linux helper with:

```console
dotnet run --file scripts/Deploy-Website.cs -- -Mode Package -DeployHost publish/deploy-host/dotsider-deploy-host
```

For first-time provisioning, set `DEPLOY_HOST` and `DEPLOY_SSH_KEY`, then run:

```console
dotnet run --file scripts/Deploy-Website.cs -- -Mode Provision -DeployHost publish/deploy-host/dotsider-deploy-host
```

Provisioning installs Caddy, Prometheus, rsync, UFW, and the `brandon` account;
creates the existing deployment directories; installs the helper and
configuration; enables services and timers; and allows ports 22, 80, and 443.

The GitHub workflow runs `Preflight` before `Deploy`. Deployment retains the
existing rsync paths, deletion rules, and exclusions; refreshes the complete
sample backup and SHA-256 manifest; restarts the website; and verifies
`/health`. If deployment fails, an integrity timer that was active beforehand
is started again.

The installed layout remains:

```text
Internet → Caddy (:443) → /ws, /health → Dotsider.Website (:5100)
                        → /*           → /var/www/dotsider-docs/ (static)
                        → :2019        → Prometheus scrape (internal only)
```

Caddy replaces `X-Forwarded-For` with the direct client's address. The website
trusts one forwarded hop from the local loopback proxy.
