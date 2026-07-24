# Dotsider.Website

WebSocket server that powers the live demo on [dotsider.dev](https://dotsider.dev). Accepts browser connections, launches sandboxed dotsider TUI sessions, and streams terminal output over WebSocket.

## Endpoints

| Path | Description |
|------|-------------|
| `/ws` | WebSocket — spawns a dotsider TUI session for the connecting client |
| `/health` | JSON health check — accepted active sessions, max sessions |

## Configuration

All settings are read from `IConfiguration` under the `Demo:` prefix:

| Key | Default | Description |
|-----|---------|-------------|
| `Demo:MaxSessions` | 50 | Positive global WebSocket limit covering handshakes and accepted sessions |
| `Demo:SessionTimeoutMinutes` | 10 | Max session duration before forced disconnect |
| `Demo:SampleAssembly` | `sample.dll` | Path to the assembly loaded in the TUI |
| `Demo:AllowedOrigins` | `*` | CORS allowed origins |

All sessions are logged with structured audit events (`CONNECT`, `DISCONNECT`).
Connections above `Demo:MaxSessions` are rejected immediately with HTTP 503 rather than queued.

## Deployment

Published as a self-contained `linux-x64` binary and deployed to a Hetzner VM behind Caddy. See `deploy/` for the Caddyfile, systemd unit, and setup script.
