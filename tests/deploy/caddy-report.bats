#!/usr/bin/env bats
# Tests for deploy/caddy-report.sh
#
# Runs caddy-report.sh against real Prometheus and Caddy instances.
# Verifies log output format and graceful handling when Prometheus is down.

load 'helpers/common'

setup() {
    # Clean log before each test
    rm -f /var/log/caddy-metrics.log
}

# ── Successful report ─────────────────────────────────────────────

@test "caddy-report.sh exits successfully" {
    run bash "$DEPLOY_DIR/caddy-report.sh"
    echo "$output"
    [[ "$status" -eq 0 ]]
}

@test "caddy-report.sh writes to /var/log/caddy-metrics.log" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    assert_file_exists /var/log/caddy-metrics.log
    [[ -s /var/log/caddy-metrics.log ]]
}

@test "log line contains ISO 8601 timestamp" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    # Format: 2026-03-11T01:23:45Z
    grep -qP '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z' /var/log/caddy-metrics.log
}

@test "log line contains req/s field" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    grep -q 'req/s=' /var/log/caddy-metrics.log
}

@test "log line contains err/s field" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    grep -q 'err/s=' /var/log/caddy-metrics.log
}

@test "log line contains p95 field" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    grep -q 'p95=' /var/log/caddy-metrics.log
}

@test "log line contains inflight field" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    grep -q 'inflight=' /var/log/caddy-metrics.log
}

@test "log line contains upstream_healthy field" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    grep -q 'upstream_healthy=' /var/log/caddy-metrics.log
}

@test "multiple runs append to log" {
    bash "$DEPLOY_DIR/caddy-report.sh"
    bash "$DEPLOY_DIR/caddy-report.sh"
    local line_count
    line_count=$(wc -l < /var/log/caddy-metrics.log)
    [[ "$line_count" -eq 2 ]]
}

# ── Prometheus down ───────────────────────────────────────────────

@test "caddy-report.sh handles Prometheus being down" {
    # Stop Prometheus
    systemctl stop prometheus
    sleep 1

    run bash "$DEPLOY_DIR/caddy-report.sh"
    [[ "$status" -eq 0 ]]

    # All fields should be N/A
    assert_file_exists /var/log/caddy-metrics.log
    grep -q 'req/s=N/A' /var/log/caddy-metrics.log
    grep -q 'err/s=N/A' /var/log/caddy-metrics.log
    grep -q 'p95=N/A' /var/log/caddy-metrics.log

    # Restart Prometheus for subsequent tests
    systemctl start prometheus
    sleep 2
}
