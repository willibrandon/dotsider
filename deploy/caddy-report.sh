#!/usr/bin/env bash
# ------------------------------------------------------------------
# caddy-report.sh — Query Prometheus for key Caddy metrics and log them
#
# Intended to run via systemd timer (every 5 minutes).
# Logs to /var/log/caddy-metrics.log
# ------------------------------------------------------------------
set -euo pipefail

PROM="http://localhost:9090"
LOG="/var/log/caddy-metrics.log"
NOW=$(date -u '+%Y-%m-%dT%H:%M:%SZ')

query() {
  curl -sf --max-time 5 "${PROM}/api/v1/query?query=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$1'))")" \
    | python3 -c "import sys,json; r=json.load(sys.stdin); print(r['data']['result'][0]['value'][1] if r.get('data',{}).get('result') else 'N/A')" 2>/dev/null || echo "N/A"
}

# Key metrics
REQ_RATE=$(query 'sum(rate(caddy_http_requests_total[5m]))')
ERR_RATE=$(query 'sum(rate(caddy_http_request_errors_total[5m]))')
P95_LATENCY=$(query 'histogram_quantile(0.95, sum(rate(caddy_http_request_duration_seconds_bucket[5m])) by (le))')
IN_FLIGHT=$(query 'sum(caddy_http_requests_in_flight)')
UPSTREAMS=$(query 'caddy_reverse_proxy_upstreams_healthy')

{
  printf '%s | req/s=%-8s err/s=%-8s p95=%-10s inflight=%-4s upstream_healthy=%s\n' \
    "$NOW" "$REQ_RATE" "$ERR_RATE" "$P95_LATENCY" "$IN_FLIGHT" "$UPSTREAMS"
} >> "$LOG"
