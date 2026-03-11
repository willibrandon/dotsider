#!/usr/bin/env bash
# Shared helpers for deploy tests

DEPLOY_DIR="/opt/deploy"

# Run a command and capture both stdout and stderr
run_script() {
    local script="$1"
    shift
    run bash "$DEPLOY_DIR/$script" "$@"
}

# Assert a file exists
assert_file_exists() {
    local path="$1"
    if [[ ! -f "$path" ]]; then
        echo "Expected file to exist: $path" >&2
        return 1
    fi
}

# Assert a file contains a string
assert_file_contains() {
    local path="$1"
    local pattern="$2"
    if ! grep -q "$pattern" "$path" 2>/dev/null; then
        echo "Expected '$path' to contain: $pattern" >&2
        echo "Actual contents:" >&2
        cat "$path" >&2
        return 1
    fi
}

# Assert a command exists on PATH
assert_command_exists() {
    local cmd="$1"
    if ! command -v "$cmd" &>/dev/null; then
        echo "Expected command to exist: $cmd" >&2
        return 1
    fi
}

# Assert a user exists
assert_user_exists() {
    local user="$1"
    if ! id "$user" &>/dev/null; then
        echo "Expected user to exist: $user" >&2
        return 1
    fi
}

# Assert a directory exists with correct owner
assert_dir_owned_by() {
    local dir="$1"
    local owner="$2"
    if [[ ! -d "$dir" ]]; then
        echo "Expected directory to exist: $dir" >&2
        return 1
    fi
    local actual_owner
    actual_owner=$(stat -c '%U' "$dir" 2>/dev/null)
    if [[ "$actual_owner" != "$owner" ]]; then
        echo "Expected '$dir' owned by '$owner', got '$actual_owner'" >&2
        return 1
    fi
}
