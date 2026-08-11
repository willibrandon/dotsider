import assert from "node:assert/strict";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import { parseChecksum, prepareTool, verifyChecksum } from "../src/acquisition";
import { executeSizeCheck } from "../src/process";
import { SizeCheckInputs } from "../src/types";

const executable = required("DOTSIDER_INTEGRATION_EXE");
const baseline = required("DOTSIDER_INTEGRATION_BASELINE");
const target = required("DOTSIDER_INTEGRATION_TARGET");
const targetMstat = required("DOTSIDER_INTEGRATION_TARGET_MSTAT");
const targetDgml = required("DOTSIDER_INTEGRATION_TARGET_DGML");
const archive = required("DOTSIDER_INTEGRATION_ARCHIVE");
const checksumSidecar = required("DOTSIDER_INTEGRATION_CHECKSUM");

test("real NativeAOT fixtures and their required sidecars exist", async () => {
  for (const filePath of [executable, baseline, target, targetMstat, targetDgml, archive, checksumSidecar]) {
    assert.equal((await fs.stat(filePath)).isFile(), true, `Expected real test asset ${filePath}`);
  }
});

test("explicit Dotsider path bypasses release resolution", async () => {
  const tool = await prepareTool("not-a-release", executable);
  assert.equal(tool.explicit, true);
  assert.equal(tool.version, "custom");
  assert.equal(tool.executablePath, path.resolve(executable));
});

test("real comparison writes schema 1 JSON and Markdown", async () => {
  const execution = await executeSizeCheck(executable, await inputs({ baseline }));
  assert.equal(execution.exitCode, 0);
  assert.equal(execution.result, "passed");
  assert.equal(execution.report?.schemaVersion, 1);
  assert.equal(execution.report?.target, path.resolve(target));
  assert.equal(execution.report?.baseline, path.resolve(baseline));
  assert.notEqual(execution.report?.summary.delta, 0);
  assert.match(await fs.readFile(execution.markdownReportPath, "utf8"), /Size check/u);
});

test("real warning budget returns passed-with-warnings", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-warning-"));
  const budgetFile = path.join(directory, "budgets.json");
  await fs.writeFile(budgetFile, JSON.stringify({
    budgets: [{
      name: "telemetry-watch",
      scope: "ns=NativeAotConsole.Telemetry",
      growth: "0",
      severity: "warning",
    }],
  }));

  const execution = await executeSizeCheck(executable, await inputs({ baseline, budgetFile }));
  assert.equal(execution.exitCode, 0);
  assert.equal(execution.result, "passed-with-warnings");
  assert.equal(execution.report?.budgets?.hasWarnings, true);
});

test("real error budget returns budget-failed and keeps both reports", async () => {
  const execution = await executeSizeCheck(executable, await inputs({
    baseline,
    budgets: ["ns=NativeAotConsole.Telemetry:growth=0"],
  }));
  assert.equal(execution.exitCode, 2);
  assert.equal(execution.result, "budget-failed");
  assert.equal(execution.report?.budgets?.passed, false);
  assert.equal((await fs.stat(execution.jsonReportPath)).isFile(), true);
  assert.equal((await fs.stat(execution.markdownReportPath)).isFile(), true);
});

test("real absolute budget without a baseline passes", async () => {
  const execution = await executeSizeCheck(executable, await inputs({ budgets: ["max=1gb"] }));
  assert.equal(execution.exitCode, 0);
  assert.equal(execution.result, "passed");
  assert.equal(execution.report?.baseline, undefined);
  assert.ok((execution.report?.rightTotal ?? 0) > 0);
});

test("real why report contains a dependency chain", async () => {
  const execution = await executeSizeCheck(executable, await inputs({ baseline, why: true, top: 25 }));
  assert.equal(execution.exitCode, 0);
  assert.match(await fs.readFile(execution.markdownReportPath, "utf8"), /Why did these appear\?/u);
  assert.match(await fs.readFile(execution.markdownReportPath, "utf8"), /kept by \(root first\):/u);
});

test("real invalid budget maps exit 1 to error", async () => {
  const execution = await executeSizeCheck(executable, await inputs({ baseline, budgets: ["invalid"] }));
  assert.equal(execution.exitCode, 1);
  assert.equal(execution.result, "error");
  assert.match(execution.stderr, /budget|expected|invalid/iu);
});

test("real packaged archive checksum accepts original and rejects modified bytes", async () => {
  const digest = parseChecksum(await fs.readFile(checksumSidecar, "utf8"));
  await verifyChecksum(archive, digest);

  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-checksum-"));
  const modified = path.join(directory, path.basename(archive));
  await fs.copyFile(archive, modified);
  const handle = await fs.open(modified, "r+");
  try {
    const first = Buffer.alloc(1);
    await handle.read(first, 0, 1, 0);
    first[0] = (first[0] ?? 0) ^ 0xff;
    await handle.write(first, 0, 1, 0);
  } finally {
    await handle.close();
  }
  await assert.rejects(verifyChecksum(modified, digest), /Checksum verification failed/u);
});

async function inputs(overrides: Partial<SizeCheckInputs>): Promise<SizeCheckInputs> {
  return {
    target,
    budgets: [],
    top: 10,
    why: false,
    dotsiderVersion: "custom",
    dotsiderPath: executable,
    reportDirectory: await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-size-check-")),
    publishSummary: true,
    publishReports: true,
    artifactName: "dotsider-size-check-test",
    ...overrides,
  };
}

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required; integration tests never substitute a fake executable or report.`);
  }
  return path.resolve(value);
}
