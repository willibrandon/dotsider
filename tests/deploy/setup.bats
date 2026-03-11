#!/usr/bin/env bats
# Tests for deploy/setup.sh
#
# Runs setup.sh in a Debian 13 container with real systemd, Caddy, and
# Prometheus. Verifies the full resulting state — packages installed,
# services running, config files correct.

load 'helpers/common'

# setup.sh is run by run.sh before bats is invoked.
# These tests verify the resulting system state.

# ── Distro ────────────────────────────────────────────────────────

@test "running on Debian" {
    source /etc/os-release
    [[ "$ID" == "debian" ]]
}

# ── .NET ──────────────────────────────────────────────────────────

@test ".NET ASP.NET Core 10 runtime is installed" {
    dotnet --list-runtimes | grep -q "Microsoft.AspNetCore.App 10\."
}

# ── Caddy ─────────────────────────────────────────────────────────

@test "Caddy is installed" {
    assert_command_exists caddy
}

@test "Caddy service is running" {
    systemctl is-active --quiet caddy
}

@test "Caddy metrics endpoint responds" {
    curl -sf http://localhost:2019/metrics | grep -q "caddy_"
}

# ── Caddyfile ─────────────────────────────────────────────────────

@test "Caddyfile is installed" {
    assert_file_exists /etc/caddy/Caddyfile
}

@test "Caddyfile enables metrics" {
    assert_file_contains /etc/caddy/Caddyfile "metrics"
}

@test "Caddyfile forwards X-Forwarded-For for /ws" {
    assert_file_contains /etc/caddy/Caddyfile "header_up X-Forwarded-For"
}

@test "Caddyfile has immutable cache for astro assets" {
    assert_file_contains /etc/caddy/Caddyfile "max-age=31536000, immutable"
}

@test "Caddyfile has revalidation cache for static assets" {
    assert_file_contains /etc/caddy/Caddyfile "max-age=3600, must-revalidate"
}

@test "Caddyfile serves docs from /var/www/dotsider-docs" {
    assert_file_contains /etc/caddy/Caddyfile "/var/www/dotsider-docs"
}

# ── Prometheus ────────────────────────────────────────────────────

@test "Prometheus is installed" {
    assert_command_exists prometheus
}

@test "Prometheus service is running" {
    systemctl is-active --quiet prometheus
}

@test "Prometheus API is healthy" {
    curl -sf http://localhost:9090/-/healthy | grep -q "Healthy"
}

@test "prometheus.yml scrapes Caddy at localhost:2019" {
    assert_file_contains /etc/prometheus/prometheus.yml "localhost:2019"
}

@test "prometheus.yml scrapes Prometheus at localhost:9090" {
    assert_file_contains /etc/prometheus/prometheus.yml "localhost:9090"
}

@test "prometheus.yml uses 15s scrape interval" {
    assert_file_contains /etc/prometheus/prometheus.yml "scrape_interval: 15s"
}

# ── User ──────────────────────────────────────────────────────────

@test "brandon user exists" {
    assert_user_exists brandon
}

@test ".ssh directory has 700 permissions" {
    local perms
    perms=$(stat -c '%a' /home/brandon/.ssh)
    [[ "$perms" == "700" ]]
}

@test "authorized_keys has 600 permissions" {
    assert_file_exists /home/brandon/.ssh/authorized_keys
    local perms
    perms=$(stat -c '%a' /home/brandon/.ssh/authorized_keys)
    [[ "$perms" == "600" ]]
}

@test ".ssh is owned by brandon" {
    assert_dir_owned_by /home/brandon/.ssh brandon
}

@test "brandon has passwordless sudo" {
    assert_file_exists /etc/sudoers.d/brandon
    assert_file_contains /etc/sudoers.d/brandon "brandon ALL=(ALL) NOPASSWD: ALL"
    local perms
    perms=$(stat -c '%a' /etc/sudoers.d/brandon)
    [[ "$perms" == "440" ]]
}

# ── Directories ───────────────────────────────────────────────────

@test "/var/www/dotsider-docs exists and is owned by brandon" {
    assert_dir_owned_by /var/www/dotsider-docs brandon
}

@test "/opt/dotsider-website exists and is owned by brandon" {
    assert_dir_owned_by /opt/dotsider-website brandon
}

# ── systemd unit ──────────────────────────────────────────────────

@test "dotsider-website.service is installed" {
    assert_file_exists /etc/systemd/system/dotsider-website.service
}

@test "dotsider-website.service is enabled" {
    systemctl is-enabled --quiet dotsider-website
}

@test "dotsider-website.service has correct ExecStart" {
    assert_file_contains /etc/systemd/system/dotsider-website.service \
        "ExecStart=/opt/dotsider-website/Dotsider.Website"
}

@test "dotsider-website.service runs as brandon" {
    assert_file_contains /etc/systemd/system/dotsider-website.service "User=brandon"
    assert_file_contains /etc/systemd/system/dotsider-website.service "Group=brandon"
}

@test "dotsider-website.service binds to port 5100" {
    assert_file_contains /etc/systemd/system/dotsider-website.service \
        "ASPNETCORE_URLS=http://localhost:5100"
}

@test "dotsider-website.service points to RichLibrary.dll" {
    assert_file_contains /etc/systemd/system/dotsider-website.service \
        "Demo__SampleAssembly=/opt/dotsider-website/RichLibrary.dll"
}

@test "dotsider-website.service restricts CORS to dotsider.dev" {
    assert_file_contains /etc/systemd/system/dotsider-website.service \
        "Demo__AllowedOrigins__0=https://dotsider.dev"
}

# ── Metrics report ────────────────────────────────────────────────

@test "caddy-report.sh is installed and executable" {
    assert_file_exists /opt/dotsider-website/caddy-report.sh
    [[ -x /opt/dotsider-website/caddy-report.sh ]]
}

@test "caddy-report.service is installed" {
    assert_file_exists /etc/systemd/system/caddy-report.service
    assert_file_contains /etc/systemd/system/caddy-report.service \
        "ExecStart=/opt/dotsider-website/caddy-report.sh"
}

@test "caddy-report.timer is installed and active" {
    assert_file_exists /etc/systemd/system/caddy-report.timer
    assert_file_contains /etc/systemd/system/caddy-report.timer "OnUnitActiveSec=5min"
    systemctl is-active --quiet caddy-report.timer
}

# ── Integrity checker ─────────────────────────────────────────────

@test "integrity-check.sh is installed and executable" {
    assert_file_exists /opt/dotsider-website/integrity-check.sh
    [[ -x /opt/dotsider-website/integrity-check.sh ]]
}

@test "integrity-check.service is installed" {
    assert_file_exists /etc/systemd/system/integrity-check.service
    assert_file_contains /etc/systemd/system/integrity-check.service \
        "ExecStart=/opt/dotsider-website/integrity-check.sh"
}

@test "integrity-check.timer is installed and active" {
    assert_file_exists /etc/systemd/system/integrity-check.timer
    assert_file_contains /etc/systemd/system/integrity-check.timer "OnUnitActiveSec=1min"
    systemctl is-active --quiet integrity-check.timer
}

# ── Logrotate ─────────────────────────────────────────────────────

@test "caddy-metrics logrotate is configured" {
    assert_file_exists /etc/logrotate.d/caddy-metrics
    assert_file_contains /etc/logrotate.d/caddy-metrics "/var/log/caddy-metrics.log"
    assert_file_contains /etc/logrotate.d/caddy-metrics "weekly"
    assert_file_contains /etc/logrotate.d/caddy-metrics "rotate 4"
    assert_file_contains /etc/logrotate.d/caddy-metrics "compress"
}

# ── Firewall ──────────────────────────────────────────────────────

@test "ufw is active" {
    ufw status | grep -q "Status: active"
}

@test "ufw allows SSH (port 22)" {
    ufw status | grep -q "22/tcp"
}

@test "ufw allows HTTP (port 80)" {
    ufw status | grep -q "80/tcp"
}

@test "ufw allows HTTPS (port 443)" {
    ufw status | grep -q "443/tcp"
}
