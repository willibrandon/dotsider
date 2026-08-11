import assert from "node:assert/strict";
import { test } from "node:test";
import { parseChecksum, resolveRid, validateArchiveEntries } from "../src/acquisition";
import { parseBudgets } from "../src/input";
import { buildSizeCheckArguments } from "../src/report";
import { escapeVsoMessage, escapeVsoProperty } from "../src/azure";

test("buildSizeCheckArguments forwards every typed input as separate arguments", () => {
  const args = buildSizeCheckArguments(
    "/work/current app",
    "/work/base app",
    ["total:growth=10kb", "ns=Example:growth=2kb"],
    "/work/budgets.json",
    17,
    true,
    "/reports/report.json",
    "/reports/report.md",
  );

  assert.deepEqual(args, [
    "size-check", "/work/current app",
    "--format", "json",
    "--output", "/reports/report.json",
    "--summary-file", "/reports/report.md",
    "--top", "17",
    "--baseline", "/work/base app",
    "--budget", "total:growth=10kb",
    "--budget", "ns=Example:growth=2kb",
    "--budget-file", "/work/budgets.json",
    "--why",
  ]);
});

test("parseBudgets repeats nonempty budget lines in order", () => {
  assert.deepEqual(
    parseBudgets(" total:growth=10kb\r\n\r\n ns=Example:growth=2kb \n"),
    ["total:growth=10kb", "ns=Example:growth=2kb"],
  );
});

test("resolveRid maps every supported operating-system and architecture pair", () => {
  assert.equal(resolveRid("win32", "x64", false), "win-x64");
  assert.equal(resolveRid("win32", "arm64", false), "win-arm64");
  assert.equal(resolveRid("linux", "x64", false), "linux-x64");
  assert.equal(resolveRid("linux", "arm64", false), "linux-arm64");
  assert.equal(resolveRid("linux", "x64", true), "linux-musl-x64");
  assert.equal(resolveRid("linux", "arm64", true), "linux-musl-arm64");
  assert.equal(resolveRid("darwin", "x64", false), "osx-x64");
  assert.equal(resolveRid("darwin", "arm64", false), "osx-arm64");
});

test("resolveRid rejects unsupported platforms and architectures", () => {
  assert.throws(() => resolveRid("freebsd", "x64", false), /platform 'freebsd'/u);
  assert.throws(() => resolveRid("linux", "ia32", false), /architecture 'ia32'/u);
});

test("parseChecksum accepts release sidecars", () => {
  const digest = "a".repeat(64);
  assert.equal(parseChecksum(`${digest}  ./dotsider-linux-x64.tar.gz\n`), digest);
  assert.equal(parseChecksum(`${digest} *dotsider-win-x64.zip`), digest);
});

test("validateArchiveEntries rejects parent and absolute paths", () => {
  validateArchiveEntries(["./dotsider", "./dotsider.mstat"]);
  assert.throws(() => validateArchiveEntries(["../secret"]), /unsafe path/u);
  assert.throws(() => validateArchiveEntries(["/etc/passwd"]), /unsafe path/u);
  assert.throws(() => validateArchiveEntries(["C:\\Windows\\system.ini"]), /unsafe path/u);
});

test("Azure logging commands escape untrusted paths and messages", () => {
  assert.equal(escapeVsoMessage("100%\r\nnext"), "100%AZP25%0D%0Anext");
  assert.equal(escapeVsoProperty("a;b]c"), "a%3Bb%5Dc");
});
