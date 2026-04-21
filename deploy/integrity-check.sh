#!/usr/bin/env bash
# integrity-check.sh — Verify the sample payload hasn't been corrupted
#
# The sample is shipped as a directory unit (RichLibrary.dll, RichLibrary.deps.json,
# Newtonsoft.Json.dll, and any other files the publish output contains). A single
# manifest of "<sha256>  <relative-path>" per file is written at deploy time and
# verified each minute; on mismatch or missing entries the whole sample/ is
# restored from the deploy-time backup and the service is restarted.
set -euo pipefail

DIR="/opt/dotsider-website"
SAMPLE="$DIR/sample"
BACKUP="$DIR/sample.bak"
MANIFEST="$DIR/sample.sha256"
LOG="/var/log/integrity-check.log"

if [[ ! -d "$SAMPLE" || ! -d "$BACKUP" || ! -f "$MANIFEST" ]]; then
    exit 0
fi

# sha256sum -c reads the manifest and reports per-file status. Running from
# inside sample/ resolves the recorded relative paths. --quiet only prints
# mismatches; any non-zero exit means at least one file drifted or vanished.
if (cd "$SAMPLE" && sha256sum --quiet -c "$MANIFEST") >/dev/null 2>&1; then
    exit 0
fi

NOW=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
echo "$NOW | CORRUPTED sample payload — restoring from backup" >> "$LOG"
rm -rf "$SAMPLE"
# cp -a preserves ownership. The backup was created during deploy as brandon,
# so restoring with -a keeps sample/ owned by brandon. Plain cp -r would run
# as root (the service user), leaving sample/ root-owned and blocking every
# subsequent deploy's rsync (brandon cannot overwrite root-owned files).
cp -a "$BACKUP" "$SAMPLE"
systemctl reset-failed dotsider-website 2>/dev/null
systemctl restart dotsider-website
echo "$NOW | RESTORED sample/ and restarted dotsider-website" >> "$LOG"
