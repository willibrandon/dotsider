#!/usr/bin/env bash
# integrity-check.sh — Verify RichLibrary.dll hasn't been corrupted
#
# Compares the current SHA256 against a known-good hash stored at deploy time.
# If the hash doesn't match, restores from the backup copy and restarts the service.
set -euo pipefail

DIR="/opt/dotsider-website"
DLL="$DIR/RichLibrary.dll"
BACKUP="$DIR/.RichLibrary.dll.bak"
HASH_FILE="$DIR/.RichLibrary.dll.sha256"
LOG="/var/log/integrity-check.log"

if [[ ! -f "$DLL" || ! -f "$BACKUP" || ! -f "$HASH_FILE" ]]; then
    exit 0
fi

EXPECTED=$(cat "$HASH_FILE")
ACTUAL=$(sha256sum "$DLL" | cut -d' ' -f1)

if [[ "$ACTUAL" != "$EXPECTED" ]]; then
    NOW=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
    echo "$NOW | CORRUPTED expected=$EXPECTED actual=$ACTUAL — restoring from backup" >> "$LOG"
    cp "$BACKUP" "$DLL"
    systemctl reset-failed dotsider-website 2>/dev/null
    systemctl restart dotsider-website
    echo "$NOW | RESTORED and restarted dotsider-website" >> "$LOG"
fi
