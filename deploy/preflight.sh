#!/usr/bin/env bash
# ------------------------------------------------------------------
# preflight.sh — Verify a target machine is ready for dotsider deploy
#
# Usage:
#   ssh brandon@host 'bash -s' < deploy/preflight.sh
#   — or run directly on the VM —
#   bash deploy/preflight.sh
# ------------------------------------------------------------------
set -euo pipefail

PASS=0
FAIL=0
WARN=0

pass() { PASS=$((PASS + 1)); printf '  \033[32m✓\033[0m %s\n' "$1"; }
fail() { FAIL=$((FAIL + 1)); printf '  \033[31m✗\033[0m %s\n' "$1"; }
warn() { WARN=$((WARN + 1)); printf '  \033[33m!\033[0m %s\n' "$1"; }

section() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# ── OS & Architecture ──────────────────────────────────────────────
section "System"

if [[ "$(uname -s)" == "Linux" ]]; then
  pass "Linux detected ($(uname -r))"
else
  fail "Expected Linux, got $(uname -s)"
fi

ARCH=$(uname -m)
if [[ "$ARCH" == "x86_64" || "$ARCH" == "aarch64" ]]; then
  pass "Architecture: $ARCH"
else
  fail "Unsupported architecture: $ARCH"
fi

# ── .NET Runtime ───────────────────────────────────────────────────
section ".NET"

if command -v dotnet &>/dev/null; then
  DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
  if dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App 10\."; then
    pass ".NET 10 ASP.NET Core runtime installed ($DOTNET_VERSION)"
  elif dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.NETCore.App 10\."; then
    warn ".NET 10 runtime found but ASP.NET Core runtime missing — install aspnetcore-runtime-10.0"
  else
    fail ".NET 10 runtime not found (have: $DOTNET_VERSION)"
  fi
else
  fail "dotnet CLI not found — install dotnet-runtime-10.0 and aspnetcore-runtime-10.0"
fi

# ── Caddy ──────────────────────────────────────────────────────────
section "Caddy"

if command -v caddy &>/dev/null; then
  CADDY_VERSION=$(caddy version 2>/dev/null | head -1)
  pass "Caddy installed ($CADDY_VERSION)"
else
  fail "Caddy not found — https://caddyserver.com/docs/install"
fi

if systemctl is-active --quiet caddy 2>/dev/null; then
  pass "Caddy service is running"
elif systemctl is-enabled --quiet caddy 2>/dev/null; then
  warn "Caddy service is enabled but not running"
else
  warn "Caddy service is not enabled"
fi

# ── Tools ──────────────────────────────────────────────────────────
section "Tools"

if command -v rsync &>/dev/null; then
  pass "rsync installed"
else
  fail "rsync not found — apt install rsync"
fi

if command -v systemctl &>/dev/null; then
  pass "systemd available"
else
  fail "systemd not found"
fi

if command -v journalctl &>/dev/null; then
  pass "journalctl available"
else
  warn "journalctl not found"
fi

# ── User ───────────────────────────────────────────────────────────
section "User"

DEPLOY_USER="brandon"

if id "$DEPLOY_USER" &>/dev/null; then
  pass "User '$DEPLOY_USER' exists"
else
  fail "User '$DEPLOY_USER' does not exist"
fi

SSH_KEYS="/home/$DEPLOY_USER/.ssh/authorized_keys"
if [[ -f "$SSH_KEYS" ]] && [[ -s "$SSH_KEYS" ]]; then
  KEY_COUNT=$(wc -l < "$SSH_KEYS")
  pass "SSH authorized_keys has $KEY_COUNT key(s)"
else
  warn "No SSH authorized_keys for '$DEPLOY_USER'"
fi

if sudo -n true 2>/dev/null; then
  pass "User '$DEPLOY_USER' has sudo access"
else
  warn "User '$DEPLOY_USER' does not have passwordless sudo"
fi

# ── Directories ────────────────────────────────────────────────────
section "Directories"

DIRS=(
  "/var/www/dotsider-docs"
  "/opt/dotsider-website"
)

for dir in "${DIRS[@]}"; do
  if [[ -d "$dir" ]]; then
    OWNER=$(stat -c '%U' "$dir" 2>/dev/null || echo "unknown")
    if [[ "$OWNER" == "$DEPLOY_USER" ]]; then
      pass "$dir exists (owned by $DEPLOY_USER)"
    else
      warn "$dir exists but owned by '$OWNER' — chown $DEPLOY_USER:$DEPLOY_USER $dir"
    fi
  else
    fail "$dir does not exist — mkdir -p $dir && chown $DEPLOY_USER:$DEPLOY_USER $dir"
  fi
done

# ── systemd Unit ───────────────────────────────────────────────────
section "systemd Service"

if systemctl list-unit-files dotsider-website.service &>/dev/null 2>&1; then
  if systemctl is-enabled --quiet dotsider-website 2>/dev/null; then
    pass "dotsider-website.service is enabled"
  else
    warn "dotsider-website.service exists but is not enabled — systemctl enable dotsider-website"
  fi
else
  warn "dotsider-website.service not installed yet"
fi

# ── Firewall ───────────────────────────────────────────────────────
section "Firewall"

if command -v ufw &>/dev/null || [[ -x /usr/sbin/ufw ]]; then
  UFW_STATUS=$(sudo ufw status 2>/dev/null)
  if echo "$UFW_STATUS" | grep -q "inactive"; then
    warn "ufw is inactive"
  else
    for port in 80 443; do
      if echo "$UFW_STATUS" | grep -q "$port"; then
        pass "Port $port allowed in ufw"
      else
        fail "Port $port not in ufw — ufw allow $port/tcp"
      fi
    done
    if echo "$UFW_STATUS" | grep -q "22"; then
      pass "Port 22 (SSH) allowed in ufw"
    else
      warn "Port 22 not explicitly in ufw"
    fi
  fi
elif command -v firewall-cmd &>/dev/null; then
  for svc in http https; do
    if firewall-cmd --list-services 2>/dev/null | grep -q "$svc"; then
      pass "$svc allowed in firewalld"
    else
      fail "$svc not in firewalld"
    fi
  done
else
  warn "No firewall detected (ufw/firewalld) — verify ports 80, 443, 22 are open at the host level"
fi

# ── Prometheus ────────────────────────────────────────────────────
section "Prometheus"

if command -v prometheus &>/dev/null || command -v /usr/bin/prometheus &>/dev/null; then
  PROM_VERSION=$(prometheus --version 2>&1 | head -1 || echo "unknown")
  pass "Prometheus installed ($PROM_VERSION)"
else
  fail "Prometheus not found — apt install prometheus"
fi

if systemctl is-active --quiet prometheus 2>/dev/null; then
  pass "Prometheus service is running"
elif systemctl is-enabled --quiet prometheus 2>/dev/null; then
  warn "Prometheus service is enabled but not running"
else
  warn "Prometheus service is not enabled"
fi

if curl -sf --max-time 3 http://localhost:9090/-/healthy &>/dev/null; then
  pass "Prometheus API is reachable"
else
  warn "Prometheus API not reachable at localhost:9090"
fi

if curl -sf --max-time 3 http://localhost:2019/metrics &>/dev/null; then
  pass "Caddy metrics endpoint is reachable"
else
  warn "Caddy metrics endpoint not reachable at localhost:2019/metrics"
fi

# ── Metrics Report Timer ─────────────────────────────────────────
section "Metrics Report"

if systemctl is-active --quiet caddy-report.timer 2>/dev/null; then
  pass "caddy-report.timer is active"
elif systemctl is-enabled --quiet caddy-report.timer 2>/dev/null; then
  warn "caddy-report.timer is enabled but not active"
else
  warn "caddy-report.timer not installed yet"
fi

if [[ -f /opt/dotsider-website/caddy-report.sh ]]; then
  pass "caddy-report.sh is deployed"
else
  warn "caddy-report.sh not found"
fi

# ── Disk & Memory ──────────────────────────────────────────────────
section "Resources"

DISK_AVAIL=$(df -BG --output=avail / 2>/dev/null | tail -1 | tr -d ' G')
if [[ -n "$DISK_AVAIL" ]] && (( DISK_AVAIL >= 2 )); then
  pass "Disk: ${DISK_AVAIL}G available"
elif [[ -n "$DISK_AVAIL" ]]; then
  warn "Disk: only ${DISK_AVAIL}G available (recommend 2G+)"
fi

MEM_TOTAL=$(awk '/MemTotal/ {printf "%.0f", $2/1024}' /proc/meminfo 2>/dev/null)
if [[ -n "$MEM_TOTAL" ]] && (( MEM_TOTAL >= 512 )); then
  pass "Memory: ${MEM_TOTAL}MB total"
elif [[ -n "$MEM_TOTAL" ]]; then
  warn "Memory: only ${MEM_TOTAL}MB (recommend 512MB+)"
fi

# ── Caddy Config ───────────────────────────────────────────────────
section "Caddy Config"

CADDYFILE="/etc/caddy/Caddyfile"
if [[ -f "$CADDYFILE" ]]; then
  if grep -q "dotsider" "$CADDYFILE" 2>/dev/null; then
    pass "Caddyfile references dotsider"
  else
    warn "Caddyfile exists but doesn't mention dotsider"
  fi
  if grep -q "metrics" "$CADDYFILE" 2>/dev/null; then
    pass "Caddyfile has metrics enabled"
  else
    warn "Caddyfile missing metrics global option — add { metrics } block"
  fi
else
  warn "No Caddyfile at $CADDYFILE"
fi

# ── Summary ────────────────────────────────────────────────────────
section "Summary"
printf '  \033[32m%d passed\033[0m, \033[33m%d warnings\033[0m, \033[31m%d failed\033[0m\n\n' "$PASS" "$WARN" "$FAIL"

if (( FAIL > 0 )); then
  printf '\033[31mPreflight failed — fix the issues above before deploying.\033[0m\n'
  exit 1
elif (( WARN > 0 )); then
  printf '\033[33mPreflight passed with warnings — review before deploying.\033[0m\n'
  exit 0
else
  printf '\033[32mAll checks passed — ready to deploy.\033[0m\n'
  exit 0
fi
