# Dotsider.Website

WebSocket server that powers the live demo on [dotsider.dev](https://dotsider.dev). Accepts browser connections, launches sandboxed dotsider TUI sessions, and streams terminal output over WebSocket.

## Endpoints

| Path | Description |
|------|-------------|
| `/ws` | WebSocket — spawns a dotsider TUI session for the connecting client |
| `/health` | JSON health check — active sessions, max sessions, circuit breaker state |

## Demo Protection

The `DemoGuard` class protects the live demo from abuse:

| Layer | Description |
|-------|-------------|
| Per-IP rate limiting | Max connections per IP within a sliding window |
| Concurrent session cap | Max simultaneous WebSocket sessions per IP |
| Rapid disconnect detection | Bans IPs that repeatedly connect and immediately disconnect |
| Escalating bans | Each offense doubles the ban duration, capped at 24 hours |
| Global circuit breaker | Disables the demo entirely if total connections exceed a threshold |

All guard actions are logged with structured audit events (`CONNECT`, `DISCONNECT`, `BLOCKED`, `BANNED`, `CIRCUIT TRIPPED`).

## Configuration

All settings are read from `IConfiguration` under the `Demo:` prefix:

| Key | Default | Description |
|-----|---------|-------------|
| `Demo:MaxSessions` | 10 | Global concurrent session limit |
| `Demo:SessionTimeoutMinutes` | 10 | Max session duration before forced disconnect |
| `Demo:SampleAssembly` | `sample.dll` | Path to the assembly loaded in the TUI |
| `Demo:AllowedOrigins` | `*` | CORS allowed origins |
| `Demo:Guard:MaxConnectionsPerIpPerWindow` | 10 | Rate limit per IP |
| `Demo:Guard:RateWindowSeconds` | 60 | Rate limit sliding window |
| `Demo:Guard:MaxConcurrentPerIp` | 3 | Max concurrent sessions per IP |
| `Demo:Guard:BanDurationMinutes` | 15 | Base ban duration (doubles per offense) |
| `Demo:Guard:MaxBanDurationHours` | 24 | Ban duration cap |
| `Demo:Guard:CircuitThreshold` | 50 | Global connection count to trip the circuit |
| `Demo:Guard:CircuitWindowSeconds` | 60 | Circuit breaker window |
| `Demo:Guard:CircuitCooldownMinutes` | 5 | How long the circuit stays open |

## Deployment

Published as a self-contained `linux-x64` binary and deployed to a Hetzner VM behind Caddy. See `deploy/` for the Caddyfile, systemd unit, and setup script.
