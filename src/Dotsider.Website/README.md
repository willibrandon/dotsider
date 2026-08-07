# Dotsider.Website

WebSocket server that powers the live demo on [dotsider.dev](https://dotsider.dev). Accepts browser connections, launches sandboxed dotsider TUI sessions, and streams terminal output over WebSocket.

## Endpoints

| Path | Description |
|------|-------------|
| `/ws` | WebSocket — spawns a dotsider TUI session for the connecting client |
| `/health` | Readiness check returning `{"status":"ok"}` |

## Configuration

All settings are read from `IConfiguration` under the `Demo:` prefix:

| Key | Default | Description |
|-----|---------|-------------|
| `Demo:MaxSessions` | 50 | Positive global WebSocket limit covering handshakes and accepted sessions |
| `Demo:MaxSessionsPerClient` | 3 | Positive concurrent WebSocket limit per resolved client address; cannot exceed `Demo:MaxSessions` |
| `Demo:SessionTimeoutMinutes` | 10 | Max session duration before forced disconnect |
| `Demo:SampleAssembly` | `sample.dll` | Path to the assembly loaded in the TUI |
| `Demo:AllowedOrigins` | `*` | HTTP and WebSocket origins; `*` is development mode and must be the only entry |
| `Demo:TrustedProxies` | `127.0.0.1`, `::1` | Exact IP addresses allowed to supply one `X-Forwarded-For` hop |

All sessions are logged with structured audit events (`CONNECT`, `DISCONNECT`).
Connections above `Demo:MaxSessionsPerClient` are rejected immediately with HTTP 429,
while site-wide exhaustion returns HTTP 503. Neither limit queues connections.

An explicit origin allowlist rejects foreign and missing WebSocket origins with HTTP
403. Wildcard mode accepts arbitrary and missing origins for local tools and
non-browser clients. Origin validation protects the browser boundary but is not
authentication.

## Deployment

Published as a self-contained `linux-x64` binary and deployed to a Hetzner VM
behind Caddy. See `deploy/` and `src/Dotsider.DeployHost/` for the authoritative
configuration and deployment helper.

The application accepts one `X-Forwarded-For` hop from an exact address in
`Demo:TrustedProxies`. Caddy runs on loopback, so the defaults match the supplied
deployment. A higher-precedence configuration source replaces the complete proxy
list from lower sources. Set `Demo__TrustedProxies__0` and subsequent indexes for a
different proxy topology. Set `Demo__TrustedProxies` to an empty value to disable
forwarded-address processing.
