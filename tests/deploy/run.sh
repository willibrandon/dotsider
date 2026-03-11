#!/usr/bin/env bash
# ------------------------------------------------------------------
# run.sh — Build and run deploy tests in a Debian 13 Docker container
#
# Usage:
#   bash tests/deploy/run.sh              # run all tests
#   bash tests/deploy/run.sh setup        # run only setup.bats
#   bash tests/deploy/run.sh preflight    # run only preflight.bats
#   bash tests/deploy/run.sh caddy-report # run only caddy-report.bats
# ------------------------------------------------------------------
set -euo pipefail

# Disable MSYS/Git-for-Windows automatic path conversion so Docker
# receives Unix-style paths like /opt/deploy instead of C:/Program Files/...
export MSYS_NO_PATHCONV=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONTAINER_NAME="dotsider-deploy-tests"
IMAGE_NAME="dotsider-deploy-tests"

# Determine which test suites to run
SUITE="${1:-all}"

echo "── Building test image ──"
docker build -t "$IMAGE_NAME" -f "$SCRIPT_DIR/Dockerfile" "$REPO_ROOT"

# Clean up any previous container
docker rm -f "$CONTAINER_NAME" 2>/dev/null || true

echo "── Starting container with systemd ──"
docker run -d --privileged --name "$CONTAINER_NAME" "$IMAGE_NAME"

# Wait for systemd to be ready
echo "── Waiting for systemd ──"
for i in $(seq 1 30); do
    if docker exec "$CONTAINER_NAME" systemctl is-system-running 2>/dev/null | grep -qE "running|degraded"; then
        break
    fi
    sleep 1
done

echo "── Running setup.sh ──"
docker exec "$CONTAINER_NAME" bash /opt/deploy/setup.sh

# Give services time to start
echo "── Waiting for services ──"
sleep 5

# Run requested test suites
EXIT_CODE=0

run_suite() {
    local suite="$1"
    echo ""
    echo "── Running $suite.bats ──"
    docker exec "$CONTAINER_NAME" bats --tap "/opt/tests/deploy/$suite.bats" || EXIT_CODE=1
}

if [[ "$SUITE" == "all" ]]; then
    run_suite setup
    run_suite preflight
    run_suite caddy-report
else
    run_suite "$SUITE"
fi

echo ""
echo "── Cleaning up ──"
docker rm -f "$CONTAINER_NAME" > /dev/null

exit $EXIT_CODE
