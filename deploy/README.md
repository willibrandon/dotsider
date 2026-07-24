# deploy

Infrastructure for deploying dotsider.dev to a Hetzner VM running Debian behind Caddy.

## Files

| File | Description |
|------|-------------|
| `setup.sh` | One-time VM bootstrap — installs .NET, Caddy, Prometheus, creates user, configures firewall, deploys all systemd units |
| `preflight.sh` | Pre-deploy validation — checks system, runtime, services, directories, firewall, metrics, disk/memory |
| `Caddyfile` | Reverse proxy config — routes `/ws` and `/health` to the WebSocket server, serves static docs, cache headers, metrics |
| `dotsider-website.service` | systemd unit for the WebSocket server (port 5100, auto-restart) |
| `prometheus.yml` | Scrape config — Caddy metrics (`:2019`) and Prometheus self-monitoring (`:9090`) at 15s intervals |
| `caddy-report.sh` | Queries Prometheus for 5 key Caddy metrics (req/s, err/s, p95 latency, in-flight, upstream health) and appends to a log |
| `caddy-report.service` | systemd oneshot unit for the metrics report |
| `caddy-report.timer` | Runs `caddy-report.service` every 5 minutes |
| `caddy-metrics-logrotate` | Weekly log rotation for `/var/log/caddy-metrics.log` (4 compressed archives) |
| `integrity-check.sh` | SHA256-checks every file under `sample/` against a deploy-time manifest; if any file is missing or altered, restores the whole directory from `sample.bak/` and restarts the service |
| `integrity-check.service` | systemd oneshot unit for the integrity check |
| `integrity-check.timer` | Runs `integrity-check.service` every minute |

## First-Time Setup

```bash
ssh root@host 'bash -s' < deploy/setup.sh
```

Installs everything: .NET 10, Caddy, Prometheus, `brandon` user with sudo, firewall (ports 22/80/443), systemd units, metrics timer, and log rotation.

## Preflight Check

```bash
ssh brandon@host 'bash -s' < deploy/preflight.sh
```

Runs automatically before each deploy in CI. Validates ~27 checks across system, .NET, Caddy, Prometheus, directories, firewall, and resources.

## Deploy Flow

Handled by `.github/workflows/deploy.yml`:

1. **Build** — Astro docs site + self-contained `linux-x64` website binary
2. **Preflight** — runs `preflight.sh` on the VM
3. **Deploy** — rsync docs to `/var/www/dotsider-docs/`, binary to `/opt/dotsider-website/`
4. **Restart** — `systemctl restart dotsider-website` + health check

## Architecture

```
Internet → Caddy (:443) → /ws, /health → Dotsider.Website (:5100)
                        → /*           → /var/www/dotsider-docs/ (static)
                        → :2019        → Prometheus scrape (internal only)
```

Caddy replaces `X-Forwarded-For` with the direct client's address. The website
trusts one forwarded hop from the local loopback proxy, which keeps per-client
WebSocket limits meaningful without accepting spoofed forwarding headers.
Deployments that move the proxy off-host must configure a specific trusted
proxy or network instead of broadening trust to all peers.
