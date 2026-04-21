#!/usr/bin/env bats
# Tests for deploy/integrity-check.sh
#
# The sample is shipped as a directory unit (RichLibrary.dll, RichLibrary.deps.json,
# Newtonsoft.Json.dll, etc.). The checker verifies every file against a deploy-time
# manifest and restores the whole directory from backup when any file drifts.

load 'helpers/common'

DIR="/opt/dotsider-website"
SAMPLE="$DIR/sample"
BACKUP="$DIR/sample.bak"
MANIFEST="$DIR/sample.sha256"
LOG="/var/log/integrity-check.log"

setup() {
    rm -f "$LOG"
    rm -rf "$SAMPLE" "$BACKUP"
    mkdir -p "$SAMPLE"
    # Known-good payload with multiple files so the test exercises the whole
    # directory contract, not just one file.
    echo "good-assembly-content" > "$SAMPLE/RichLibrary.dll"
    echo '{"targets":{}}'          > "$SAMPLE/RichLibrary.deps.json"
    echo "newtonsoft-bytes"        > "$SAMPLE/Newtonsoft.Json.dll"
    cp -r "$SAMPLE" "$BACKUP"
    (cd "$SAMPLE" && find . -type f -print0 \
        | LC_ALL=C sort -z \
        | xargs -0 sha256sum) > "$MANIFEST"
}

teardown() {
    rm -rf "$SAMPLE" "$BACKUP"
    rm -f "$MANIFEST" "$LOG"
}

# ── Clean payload ─────────────────────────────────────────────────

@test "exits cleanly when manifest matches" {
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
    [[ ! -f "$LOG" ]]
}

@test "does not modify sample files when manifest matches" {
    local before after
    before=$(stat -c '%Y' "$SAMPLE/RichLibrary.dll")
    bash "$DEPLOY_DIR/integrity-check.sh"
    after=$(stat -c '%Y' "$SAMPLE/RichLibrary.dll")
    [[ "$before" == "$after" ]]
}

# ── Corrupted file ───────────────────────────────────────────────

@test "detects corrupted RichLibrary.dll and restores from backup" {
    echo "corrupted-bytes" > "$SAMPLE/RichLibrary.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -q "$SAMPLE/RichLibrary.dll" "$BACKUP/RichLibrary.dll"
}

@test "detects corrupted Newtonsoft.Json.dll and restores the whole payload" {
    echo "tampered" > "$SAMPLE/Newtonsoft.Json.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -rq "$SAMPLE" "$BACKUP"
}

@test "detects corrupted RichLibrary.deps.json and restores from backup" {
    echo '{"targets":{"evil":{}}}' > "$SAMPLE/RichLibrary.deps.json"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -q "$SAMPLE/RichLibrary.deps.json" "$BACKUP/RichLibrary.deps.json"
}

@test "logs corruption event" {
    echo "corrupted-bytes" > "$SAMPLE/RichLibrary.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    assert_file_exists "$LOG"
    grep -q "CORRUPTED" "$LOG"
}

@test "logs restoration event" {
    echo "corrupted-bytes" > "$SAMPLE/RichLibrary.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    grep -q "RESTORED" "$LOG"
}

# ── Missing listed file ──────────────────────────────────────────

@test "detects missing Newtonsoft.Json.dll and restores from backup" {
    rm -f "$SAMPLE/Newtonsoft.Json.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -q "$SAMPLE/Newtonsoft.Json.dll" "$BACKUP/Newtonsoft.Json.dll"
}

# ── Missing payload directories / manifest ───────────────────────

@test "exits cleanly when sample directory is missing" {
    rm -rf "$SAMPLE"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

@test "exits cleanly when backup is missing" {
    rm -rf "$BACKUP"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

@test "exits cleanly when manifest is missing" {
    rm -f "$MANIFEST"
    run bash "$DEPLOY_DIR/integrity-check.sh"
    [[ "$status" -eq 0 ]]
}

# ── Ownership preservation ──────────────────────────────────────
# Deploys rsync the sample payload as `brandon`; the checker runs as root. If
# the restore path creates files owned by root, the next deploy cannot overwrite
# them and every subsequent sample deploy fails with permission-denied. The
# backup directory is authoritative for ownership, so the restore must preserve
# whatever uid:gid it holds at deploy time.

@test "restored sample directory retains backup's ownership" {
    # Simulate a deploy-time backup owned by the deploy user. The test harness
    # runs as root inside the container, so we can force any ownership we want
    # on the backup and verify the restore carries it across.
    chown -R nobody:nogroup "$BACKUP"
    echo "corrupted-bytes" > "$SAMPLE/RichLibrary.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"

    local owner group
    owner=$(stat -c '%U' "$SAMPLE")
    group=$(stat -c '%G' "$SAMPLE")
    [[ "$owner" == "nobody" ]]
    [[ "$group" == "nogroup" ]]

    owner=$(stat -c '%U' "$SAMPLE/RichLibrary.dll")
    group=$(stat -c '%G' "$SAMPLE/RichLibrary.dll")
    [[ "$owner" == "nobody" ]]
    [[ "$group" == "nogroup" ]]
}

# ── Repeated corruption ──────────────────────────────────────────

@test "recovers from repeated corruption" {
    echo "bad1" > "$SAMPLE/RichLibrary.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    echo "bad2" > "$SAMPLE/Newtonsoft.Json.dll"
    bash "$DEPLOY_DIR/integrity-check.sh"
    diff -rq "$SAMPLE" "$BACKUP"
    local line_count
    line_count=$(grep -c "CORRUPTED" "$LOG")
    [[ "$line_count" -eq 2 ]]
}
