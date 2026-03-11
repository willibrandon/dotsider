#!/usr/bin/env bats
# Tests for deploy/preflight.sh
#
# Runs preflight.sh against a fully configured system (post-setup.sh)
# and against a bare system to verify it catches missing requirements.

load 'helpers/common'

# ── Post-setup: preflight should pass ─────────────────────────────

@test "preflight passes on a configured system" {
    run bash "$DEPLOY_DIR/preflight.sh"
    echo "$output"
    [[ "$status" -eq 0 ]]
}

@test "preflight reports zero failures on configured system" {
    run bash "$DEPLOY_DIR/preflight.sh"
    echo "$output"
    [[ "$output" =~ "0 failed" ]]
}

@test "preflight detects .NET 10 runtime" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ ".NET 10 ASP.NET Core runtime installed" ]]
}

@test "preflight detects Caddy" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Caddy installed" ]]
}

@test "preflight detects Caddy is running" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Caddy service is running" ]]
}

@test "preflight detects Prometheus" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Prometheus installed" ]]
}

@test "preflight detects Prometheus is running" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Prometheus service is running" ]]
}

@test "preflight detects Prometheus API is reachable" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Prometheus API is reachable" ]]
}

@test "preflight detects Caddy metrics endpoint" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Caddy metrics endpoint is reachable" ]]
}

@test "preflight detects brandon user" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "User 'brandon' exists" ]]
}

@test "preflight detects deploy directories" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "/var/www/dotsider-docs exists" ]]
    [[ "$output" =~ "/opt/dotsider-website exists" ]]
}

@test "preflight detects dotsider-website.service is enabled" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "dotsider-website.service is enabled" ]]
}

@test "preflight detects caddy-report.timer is active" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "caddy-report.timer is active" ]]
}

@test "preflight detects caddy-report.sh is deployed" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "caddy-report.sh is deployed" ]]
}

@test "preflight detects Caddyfile references dotsider" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Caddyfile references dotsider" ]]
}

@test "preflight detects Caddyfile has metrics enabled" {
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$output" =~ "Caddyfile has metrics enabled" ]]
}

# ── Failure detection ─────────────────────────────────────────────

@test "preflight fails when dotnet is missing" {
    # Temporarily hide dotnet
    local dotnet_path
    dotnet_path=$(which dotnet 2>/dev/null) || skip "dotnet not installed"
    mv "$dotnet_path" "${dotnet_path}.bak"

    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$status" -ne 0 ]]
    [[ "$output" =~ "dotnet CLI not found" ]]

    mv "${dotnet_path}.bak" "$dotnet_path"
}

@test "preflight fails when deploy directory is missing" {
    rmdir /var/www/dotsider-docs
    run bash "$DEPLOY_DIR/preflight.sh"
    [[ "$status" -ne 0 ]]
    [[ "$output" =~ "does not exist" ]]

    # Restore
    mkdir -p /var/www/dotsider-docs
    chown brandon:brandon /var/www/dotsider-docs
}
