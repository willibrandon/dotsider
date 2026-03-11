# Deploy Tests

Integration tests for `deploy/setup.sh`, `deploy/preflight.sh`, and `deploy/caddy-report.sh`. Runs in a Debian 13 Docker container with real systemd, Caddy, and Prometheus.

## Requirements

- Docker

## Usage

```bash
# Run all test suites
bash tests/deploy/run.sh

# Run a single suite
bash tests/deploy/run.sh setup
bash tests/deploy/run.sh preflight
bash tests/deploy/run.sh caddy-report
```

## Test Suites

| Suite | Tests | What it verifies |
|-------|-------|------------------|
| `setup.bats` | 39 | .NET 10 installed, Caddy installed and running with metrics, Prometheus installed and healthy, brandon user with sudo, deploy directories, systemd units enabled, Caddyfile content (X-Forwarded-For, cache headers), prometheus.yml scrape targets, caddy-report timer active, logrotate config, ufw firewall rules |
| `preflight.bats` | 18 | All preflight checks pass on a configured system, zero failures reported, each service and directory detected, failure detection when dotnet is missing or a directory is removed |
| `caddy-report.bats` | 10 | Script exits successfully, writes to log file, log line format (ISO 8601 timestamp, req/s, err/s, p95, inflight, upstream_healthy), multiple runs append, graceful N/A output when Prometheus is down |

## How It Works

1. `run.sh` builds a Docker image from `Dockerfile` (Debian 13 + systemd + bats-core)
2. Starts the container with `--privileged` so systemd runs as PID 1
3. Executes `deploy/setup.sh` inside the container
4. Runs bats test suites against the resulting system state
5. Cleans up the container on exit

## Manual Docker Workflow

Build the image and run tests step by step:

```bash
# Build from repo root
docker build -t dotsider-deploy-tests -f tests/deploy/Dockerfile .

# Start container with systemd as PID 1
docker run -d --privileged --name dotsider-deploy-tests dotsider-deploy-tests

# Wait for systemd, then run setup.sh
docker exec dotsider-deploy-tests bash /opt/deploy/setup.sh

# Give services time to start
sleep 5

# Run individual test suites
docker exec dotsider-deploy-tests bats --tap /opt/tests/deploy/setup.bats
docker exec dotsider-deploy-tests bats --tap /opt/tests/deploy/preflight.bats
docker exec dotsider-deploy-tests bats --tap /opt/tests/deploy/caddy-report.bats

# Clean up
docker rm -f dotsider-deploy-tests
```

## Docker Image

The Dockerfile starts from a minimal `debian:13` and only preinstalls:

- **systemd** + dbus — required as PID 1 inside the container
- **curl** — required to fetch bats-core
- **dos2unix** — fixes Windows CRLF line endings on copied scripts

Everything else (sudo, gnupg, python3, rsync, ufw, .NET, Caddy, Prometheus) must be installed by `setup.sh` itself, matching a fresh Debian host.
