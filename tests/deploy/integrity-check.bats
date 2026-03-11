#!/usr/bin/env bats
# Tests for deploy/integrity-check.sh
#
# Simulates a corrupt RichLibrary.dll and verifies the integrity checker
# detects the corruption and restores from backup.

load 'helpers/common'

DIR="/opt/dotsider-website"
DLL="$DIR/RichLibrary.dll"
BACKUP="$DIR/.RichLibrary.dll.bak"
HASH_FILE="$DIR/.RichLibrary.dll.sha256"
LOG="/var/log/integrity-check.log"

setup() {
    rm -f "$LOG"
    # Create a known-good DLL, backup, and hash
    echo "good-assembly-content" > "$DLL"
    cp "$DLL" "$BACKUP"
    sha256sum "$DLL" | cut -d' ' -f1 > "$HASH_FILE"
}

teardown() {
    rm -f "$DLL" "$BACKUP" "$HASH_FILE" "$LOG"
}

# ── Clean file ────────────────────────────────────────────────────

@test "exits cleanly when DLL matches hash" {
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
    [[ ! -f "$LOG" ]]
}

@test "does not overwrite DLL when hash matches" {
    local before after
    before=$(stat -c '%Y' "$DLL")
    bash "$DEPLOY_DIR/integrity-check.sh"
    after=$(stat -c '%Y' "$DLL")
    [[ "$before" == "$after" ]]
}

# ── Corrupted file ───────────────────────────────────────────────

@test "detects corrupted DLL and restores from backup" {
    echo "corrupted-bytes" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    local actual expected
    actual=$(sha256sum "$DLL" | cut -d' ' -f1)
    expected=$(cat "$HASH_FILE")
    [[ "$actual" == "$expected" ]]
}

@test "logs corruption event with expected and actual hashes" {
    echo "corrupted-bytes" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    assert_file_exists "$LOG"
    grep -q "CORRUPTED" "$LOG"
    grep -q "expected=" "$LOG"
    grep -q "actual=" "$LOG"
}

@test "logs restoration event" {
    echo "corrupted-bytes" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    grep -q "RESTORED" "$LOG"
}

@test "restored DLL content matches backup" {
    echo "corrupted-bytes" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -q "$DLL" "$BACKUP"
}

# ── Missing files ────────────────────────────────────────────────

@test "exits cleanly when DLL is missing" {
    rm -f "$DLL"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

@test "exits cleanly when backup is missing" {
    rm -f "$BACKUP"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

@test "exits cleanly when hash file is missing" {
    rm -f "$HASH_FILE"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

# ── Multiple corruptions ─────────────────────────────────────────

@test "recovers from repeated corruption" {
    echo "bad1" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    echo "bad2" > "$DLL"
    bash "$DEPLOY_DIR/integrity-check.sh"
    local actual expected
    actual=$(sha256sum "$DLL" | cut -d' ' -f1)
    expected=$(cat "$HASH_FILE")
    [[ "$actual" == "$expected" ]]
    local line_count
    line_count=$(grep -c "CORRUPTED" "$LOG")
    [[ "$line_count" -eq 2 ]]
}
