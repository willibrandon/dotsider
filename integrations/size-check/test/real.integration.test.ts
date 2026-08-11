import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import { parseChecksum, prepareTool, verifyChecksum } from "../src/acquisition";
import { executeSizeCheck } from "../src/process";
import { createStableOutputs } from "../src/report";
import { SizeCheckInputs } from "../src/types";

const executable = required("DOTSIDER_INTEGRATION_EXE");
const baseline = required("DOTSIDER_INTEGRATION_BASELINE");
const target = required("DOTSIDER_INTEGRATION_TARGET");
const targetMstat = required("DOTSIDER_INTEGRATION_TARGET_MSTAT");
const targetDgml = required("DOTSIDER_INTEGRATION_TARGET_DGML");
const archive = required("DOTSIDER_INTEGRATION_ARCHIVE");
const checksumSidecar = required("DOTSIDER_INTEGRATION_CHECKSUM");
const githubRuntime = path.resolve(__dirname, "../../../integrations/size-check/dist/github.js");

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

test("GitHub adapter keeps real reports and writes error outputs when summary publishing fails", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-summary-error-"));
  const reportDirectory = path.join(directory, "reports");
  const outputPath = path.join(directory, "github-output.txt");
  const summaryDirectory = path.join(directory, "summary-directory");
  await fs.mkdir(summaryDirectory);

  const child = await runGitHub("run", {
    GITHUB_OUTPUT: outputPath,
    GITHUB_STEP_SUMMARY: summaryDirectory,
    RUNNER_TEMP: directory,
    DOTSIDER_INPUT_MODE: "compare",
    DOTSIDER_INPUT_TARGET: target,
    DOTSIDER_INPUT_BASELINE: baseline,
    DOTSIDER_INPUT_TOP: "10",
    DOTSIDER_INPUT_WHY: "false",
    DOTSIDER_INPUT_VERSION: "not-a-release",
    DOTSIDER_INPUT_PATH: executable,
    DOTSIDER_INPUT_REPORT_DIRECTORY: reportDirectory,
    DOTSIDER_INPUT_PUBLISH_SUMMARY: "true",
    DOTSIDER_INPUT_PUBLISH_REPORTS: "true",
    DOTSIDER_INPUT_ARTIFACT_NAME: "dotsider-summary-error",
    DOTSIDER_PREPARED_VERSION: "custom",
    DOTSIDER_PREPARED_RID: `unused-${process.arch}`,
    DOTSIDER_PREPARED_CACHE_DIRECTORY: path.dirname(executable),
    DOTSIDER_PREPARED_EXECUTABLE_PATH: executable,
    DOTSIDER_PREPARED_CACHE_KEY: "dotsider-custom",
    DOTSIDER_PREPARED_EXPLICIT: "true",
  });

  assert.equal(child.exitCode, 1);
  const outputs = parseGitHubOutputs(await fs.readFile(outputPath, "utf8"));
  assert.equal(outputs.get("result"), "error");
  assert.equal(outputs.get("exit-code"), "1");
  assert.equal(outputs.get("artifact-name"), "dotsider-summary-error");
  const jsonReportPath = outputs.get("json-report-path");
  const markdownReportPath = outputs.get("markdown-report-path");
  assert.ok(jsonReportPath);
  assert.ok(markdownReportPath);
  assert.equal(jsonReportPath, path.resolve(reportDirectory, "dotsider-size-check.json"));
  assert.equal(markdownReportPath, path.resolve(reportDirectory, "dotsider-size-check.md"));
  const report = JSON.parse(await fs.readFile(jsonReportPath, "utf8")) as { schemaVersion?: number };
  assert.equal(report.schemaVersion, 1);
  assert.equal((await fs.stat(markdownReportPath)).isFile(), true);
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

  const enforcement = await runGitHubEnforcement(execution);
  assert.equal(enforcement.exitCode, 1);
  assert.match(enforcement.stdout, /::error::Dotsider size budgets were exceeded: compared with baseline; .* from baseline; 1 budget violation\./u);
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

  const enforcement = await runGitHubEnforcement(execution);
  assert.equal(enforcement.exitCode, 1);
  assert.match(enforcement.stdout, /::error::Dotsider size check failed with exit code 1 \(error\)\./u);
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
  const mode = overrides.baseline ? "compare" : "current";
  return {
    mode,
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

async function runGitHubEnforcement(execution: Awaited<ReturnType<typeof executeSizeCheck>>): Promise<ChildResult> {
  const outputs = createStableOutputs(execution, "dotsider-size-check", "custom");
  return await runGitHub("enforce", {
    DOTSIDER_EXIT_CODE: outputs.exitCode,
    DOTSIDER_RESULT: outputs.result,
    DOTSIDER_MODE: outputs.mode,
    DOTSIDER_TOTAL_BASIS: outputs.totalBasis,
    DOTSIDER_BASELINE_TOTAL: outputs.baselineTotal,
    DOTSIDER_CURRENT_TOTAL: outputs.currentTotal,
    DOTSIDER_DELTA: outputs.delta,
    DOTSIDER_VIOLATION_COUNT: outputs.violationCount,
    DOTSIDER_ARTIFACT_NAME: outputs.artifactName,
  });
}

async function runGitHub(mode: string, environment: NodeJS.ProcessEnv): Promise<ChildResult> {
  return await new Promise<ChildResult>((resolve, reject) => {
    const child = spawn(process.execPath, [githubRuntime, mode], {
      shell: false,
      windowsHide: true,
      env: {
        ...process.env,
        ...environment,
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (value: string) => stdout += value);
    child.stderr.on("data", (value: string) => stderr += value);
    child.on("error", reject);
    child.on("close", childExitCode => resolve({ exitCode: childExitCode ?? 1, stdout, stderr }));
  });
}

function parseGitHubOutputs(source: string): Map<string, string> {
  const lines = source.split(/\r?\n/u);
  const outputs = new Map<string, string>();
  for (let index = 0; index < lines.length; index++) {
    const match = /^([^<]+)<<(.+)$/u.exec(lines[index] ?? "");
    if (!match?.[1] || !match[2]) {
      continue;
    }
    const value: string[] = [];
    index++;
    while (index < lines.length && lines[index] !== match[2]) {
      value.push(lines[index] ?? "");
      index++;
    }
    outputs.set(match[1], value.join("\n"));
  }
  return outputs;
}

interface ChildResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required; integration tests never substitute a fake executable or report.`);
  }
  return path.resolve(value);
}
