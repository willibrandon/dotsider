import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import {
  detectMuslRuntime,
  extractArchive,
  listArchiveEntries,
  parseChecksum,
  resolveRid,
  validateArchiveEntries,
} from "../src/acquisition";
import { parseBudgets, parseMode, parseTop } from "../src/input";
import { buildSizeCheckArguments, formatSizeCheckSummary } from "../src/report";
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

test("formatSizeCheckSummary reports the measured total, delta, and violations", () => {
  assert.equal(
    formatSizeCheckSummary({
      mode: "compare",
      totalBasis: "fileSize",
      baselineTotal: "26214400",
      currentTotal: "36029560",
      delta: "9815160",
      violationCount: "1",
    }),
    "compared with baseline; 34.4 MB total (fileSize); +9.4 MB from baseline; 1 budget violation",
  );
});

test("parseMode accepts current without a baseline and compare with a baseline", () => {
  assert.equal(parseMode("current", undefined), "current");
  assert.equal(parseMode("compare", "/work/baseline.mstat"), "compare");
});

test("parseMode rejects missing, unknown, and contradictory inputs", () => {
  assert.throws(() => parseMode(undefined, undefined), /mode is required/u);
  assert.throws(() => parseMode("automatic", undefined), /'current' or 'compare'/u);
  assert.throws(() => parseMode("current", "/work/baseline.mstat"), /must not be supplied/u);
  assert.throws(() => parseMode("compare", undefined), /baseline is required/u);
});

test("parseBudgets repeats nonempty budget lines in order", () => {
  assert.deepEqual(
    parseBudgets(" total:growth=10kb\r\n\r\n ns=Example:growth=2kb \n"),
    ["total:growth=10kb", "ns=Example:growth=2kb"],
  );
});

test("parseTop accepts only complete non-negative integers", () => {
  assert.equal(parseTop(undefined), 10);
  assert.equal(parseTop(" 0 "), 0);
  assert.equal(parseTop("17"), 17);
  assert.equal(parseTop("0004"), 4);
});

test("parseTop rejects malformed and unsafe numeric strings without accepting prefixes", () => {
  for (const value of ["", " ", "-1", "+1", "10oops", "1.5", "1e2", "9007199254740992"]) {
    assert.throws(
      () => parseTop(value),
      error => error instanceof Error
        && error.message === `top must be a non-negative integer; received '${value}'.`,
      `Expected '${value}' to be rejected in full`,
    );
  }
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

test("detectMuslRuntime uses the Node runtime libc report instead of a distro marker", () => {
  assert.equal(detectMuslRuntime("linux", undefined, { header: { glibcVersionRuntime: "2.39" } }), false);
  assert.equal(detectMuslRuntime("linux", undefined, { header: {} }), true);
  assert.equal(detectMuslRuntime("linux", "1", { header: { glibcVersionRuntime: "2.39" } }), true);
  assert.equal(detectMuslRuntime("linux", "0", { header: {} }), false);
  assert.equal(detectMuslRuntime("darwin", "1", { header: {} }), false);
  assert.equal(detectMuslRuntime("linux", undefined, undefined), undefined);
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

test("archive helpers handle absolute archive paths", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-archive-path-"));
  try {
    const source = path.join(directory, "source");
    const destination = path.join(directory, "destination");
    const archivePath = path.join(directory, "fixture.tar.gz");
    await fs.mkdir(source);
    await fs.mkdir(destination);
    await fs.writeFile(path.join(source, "payload.txt"), "verified archive payload", "utf8");
    await runTar(["-czf", path.basename(archivePath), "-C", path.basename(source), "."], directory);

    const entries = await listArchiveEntries(archivePath);
    assert.ok(entries.some(entry => entry.replaceAll("\\", "/").endsWith("/payload.txt")));

    await extractArchive(archivePath, destination);
    assert.equal(
      await fs.readFile(path.join(destination, "payload.txt"), "utf8"),
      "verified archive payload",
    );
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("archive helpers use Windows system tar for zip archives", {
  skip: process.platform !== "win32",
}, async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-zip-tool-"));
  const originalPath = process.env.PATH;
  try {
    const source = path.join(directory, "source");
    const destination = path.join(directory, "destination");
    const archivePath = path.join(directory, "fixture.zip");
    await fs.mkdir(source);
    await fs.mkdir(destination);
    await fs.writeFile(path.join(source, "payload.txt"), "verified zip payload", "utf8");

    const windowsDirectory = process.env.SystemRoot ?? process.env.WINDIR ?? "C:\\Windows";
    const systemTar = path.join(windowsDirectory, "System32", "tar.exe");
    await runTar(
      ["-a", "-cf", path.basename(archivePath), "-C", path.basename(source), "."],
      directory,
      systemTar,
    );

    process.env.PATH = directory;
    const entries = await listArchiveEntries(archivePath);
    assert.ok(entries.some(entry => entry.replaceAll("\\", "/").endsWith("/payload.txt")));

    await extractArchive(archivePath, destination);
    assert.equal(
      await fs.readFile(path.join(destination, "payload.txt"), "utf8"),
      "verified zip payload",
    );
  } finally {
    if (originalPath === undefined) {
      delete process.env.PATH;
    } else {
      process.env.PATH = originalPath;
    }
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure logging commands escape untrusted paths and messages", () => {
  assert.equal(escapeVsoMessage("100%\r\nnext"), "100%AZP25%0D%0Anext");
  assert.equal(escapeVsoProperty("a;b]c"), "a%3Bb%5Dc");
});

async function runTar(
  args: readonly string[],
  workingDirectory: string,
  executable = "tar",
): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const child = spawn(executable, [...args], {
      cwd: workingDirectory,
      shell: false,
      windowsHide: true,
      stdio: ["ignore", "ignore", "pipe"],
    });
    let stderr = "";
    child.stderr.setEncoding("utf8");
    child.stderr.on("data", (value: string) => stderr += value);
    child.on("error", reject);
    child.on("close", exitCode => {
      if (exitCode === 0) {
        resolve();
      } else {
        reject(new Error(`${executable} ${args.join(" ")} failed with exit code ${exitCode}:\n${stderr}`));
      }
    });
  });
}
