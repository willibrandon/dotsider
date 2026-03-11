#!/usr/bin/env bash
# ------------------------------------------------------------------
# setup.sh — One-time setup for a fresh Hetzner VM
#
# Run as root:
#   ssh root@host 'bash -s' < deploy/setup.sh
# ------------------------------------------------------------------
set -euo pipefail

echo "── Installing packages ──"
apt-get update
apt-get install -y rsync ufw curl

echo "── Installing .NET 10 ASP.NET Core runtime ──"
DISTRO=$(. /etc/os-release && echo "$ID")
DISTRO_VERSION=$(. /etc/os-release && echo "$VERSION_ID")
curl -fsSL "https://packages.microsoft.com/config/${DISTRO}/${DISTRO_VERSION}/packages-microsoft-prod.deb" -o /tmp/packages-microsoft-prod.deb
dpkg -i /tmp/packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-10.0

echo "── Installing Caddy ──"
apt-get install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt-get update
apt-get install -y caddy

echo "── Creating brandon user ──"
if ! id brandon &>/dev/null; then
  adduser --disabled-password --gecos "" brandon
fi

echo "── Setting up SSH for brandon ──"
mkdir -p /home/brandon/.ssh
chmod 700 /home/brandon/.ssh
touch /home/brandon/.ssh/authorized_keys
chmod 600 /home/brandon/.ssh/authorized_keys
chown -R brandon:brandon /home/brandon/.ssh

echo "── Configuring passwordless sudo ──"
echo "brandon ALL=(ALL) NOPASSWD: ALL" > /etc/sudoers.d/brandon
chmod 440 /etc/sudoers.d/brandon
visudo -c

echo "── Creating directories ──"
mkdir -p /var/www/dotsider-docs
mkdir -p /opt/dotsider-website
chown brandon:brandon /var/www/dotsider-docs
chown brandon:brandon /opt/dotsider-website

echo "── Installing systemd service ──"
cp /dev/stdin /etc/systemd/system/dotsider-website.service << 'UNIT'
[Unit]
Description=Dotsider.Website — docs site WebSocket server
After=network.target

[Service]
Type=simple
User=brandon
Group=brandon
WorkingDirectory=/opt/dotsider-website
ExecStart=/opt/dotsider-website/Dotsider.Website
Restart=always
RestartSec=5

Environment=ASPNETCORE_URLS=http://localhost:5100
Environment=DOTNET_ENVIRONMENT=Production
Environment=Demo__SampleAssembly=/opt/dotsider-website/RichLibrary.dll
Environment=Demo__MaxSessions=10
Environment=Demo__SessionTimeoutMinutes=10
Environment=Demo__AllowedOrigins__0=https://dotsider.dev

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable dotsider-website

echo "── Installing Caddyfile ──"
cat > /etc/caddy/Caddyfile << 'CADDY'
{
	metrics
}

dotsider.dev {
	handle /ws {
		reverse_proxy localhost:5100 {
			header_up X-Forwarded-For {remote_host}
		}
	}

	handle /health {
		reverse_proxy localhost:5100
	}

	handle {
		root * /var/www/dotsider-docs

		@astro path /_astro/*
		header @astro Cache-Control "public, max-age=31536000, immutable"

		@static path *.js *.css *.png *.webp *.avif *.gif *.svg *.woff2
		header @static Cache-Control "public, max-age=3600, must-revalidate"

		file_server
	}
}
CADDY
systemctl reload caddy

echo "── Installing Prometheus ──"
apt-get install -y prometheus
cp /dev/stdin /etc/prometheus/prometheus.yml << 'PROMYML'
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: caddy
    static_configs:
      - targets: ['localhost:2019']

  - job_name: prometheus
    static_configs:
      - targets: ['localhost:9090']
PROMYML
systemctl daemon-reload
systemctl enable prometheus
systemctl restart prometheus

echo "── Installing metrics report timer ──"
cp /dev/stdin /opt/dotsider-website/caddy-report.sh << 'REPORT'
#!/usr/bin/env bash
set -euo pipefail

PROM="http://localhost:9090"
LOG="/var/log/caddy-metrics.log"
NOW=$(date -u '+%Y-%m-%dT%H:%M:%SZ')

query() {
  curl -sf --max-time 5 "${PROM}/api/v1/query?query=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$1'))")" \
    | python3 -c "import sys,json; r=json.load(sys.stdin); print(r['data']['result'][0]['value'][1] if r.get('data',{}).get('result') else 'N/A')" 2>/dev/null || echo "N/A"
}

REQ_RATE=$(query 'sum(rate(caddy_http_requests_total[5m]))')
ERR_RATE=$(query 'sum(rate(caddy_http_request_errors_total[5m]))')
P95_LATENCY=$(query 'histogram_quantile(0.95, sum(rate(caddy_http_request_duration_seconds_bucket[5m])) by (le))')
IN_FLIGHT=$(query 'sum(caddy_http_requests_in_flight)')
UPSTREAMS=$(query 'caddy_reverse_proxy_upstreams_healthy')

printf '%s | req/s=%-8s err/s=%-8s p95=%-10s inflight=%-4s upstream_healthy=%s\n' \
  "$NOW" "$REQ_RATE" "$ERR_RATE" "$P95_LATENCY" "$IN_FLIGHT" "$UPSTREAMS" >> "$LOG"
REPORT
chmod +x /opt/dotsider-website/caddy-report.sh

cat > /etc/systemd/system/caddy-report.service << 'SVC'
[Unit]
Description=Caddy metrics report
After=prometheus.service

[Service]
Type=oneshot
ExecStart=/opt/dotsider-website/caddy-report.sh
User=root
SVC

cat > /etc/systemd/system/caddy-report.timer << 'TMR'
[Unit]
Description=Run Caddy metrics report every 5 minutes

[Timer]
OnBootSec=1min
OnUnitActiveSec=5min

[Install]
WantedBy=timers.target
TMR

systemctl daemon-reload
systemctl enable caddy-report.timer
systemctl start caddy-report.timer

echo "── Installing metrics log rotation ──"
cat > /etc/logrotate.d/caddy-metrics << 'LOGR'
/var/log/caddy-metrics.log {
    weekly
    rotate 4
    compress
    missingok
    notifempty
}
LOGR

echo "── Configuring firewall ──"
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo ""
echo "── Setup complete ──"
