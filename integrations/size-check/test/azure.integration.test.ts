import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";

const executable = required("DOTSIDER_INTEGRATION_EXE");
const baseline = required("DOTSIDER_INTEGRATION_BASELINE");
const target = required("DOTSIDER_INTEGRATION_TARGET");
const runtime = path.resolve(__dirname, "../../../azure-devops/tasks/DotsiderSizeCheckV1/runtime/azure.js");

test("Azure handler emits outputs from a real comparison", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-pass-"));
  const result = await runAzure(directory, "");
  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(result.stdout, /variable=result;isOutput=true;\]passed/u);
  assert.match(result.stdout, /variable=exitCode;isOutput=true;\]0/u);
  assert.match(result.stdout, /##vso\[task.uploadsummary /u);
  assert.match(result.stdout, /##vso\[artifact.upload artifactname=dotsider-azure-test;/u);

  const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
    schemaVersion: number;
    summary: { delta: number };
  };
  assert.equal(report.schemaVersion, 1);
  assert.notEqual(report.summary.delta, 0);
});

test("Azure handler publishes real reports before a budget failure", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-fail-"));
  const result = await runAzure(directory, "ns=NativeAotConsole.Telemetry:growth=0");
  assert.equal(result.exitCode, 1);
  assert.match(result.stdout, /variable=result;isOutput=true;\]budget-failed/u);
  assert.match(result.stdout, /variable=exitCode;isOutput=true;\]2/u);
  assert.match(result.stdout, /Dotsider size check: .* budget violation/u);
  assert.match(result.stdout, /##vso\[task\.logissue type=error;\]Dotsider size budgets were exceeded:/u);
  const publishIndex = result.stdout.indexOf("##vso[artifact.upload");
  const issueIndex = result.stdout.indexOf("##vso[task.logissue type=error;");
  const failureIndex = result.stdout.indexOf("##vso[task.complete result=Failed;");
  assert.ok(publishIndex >= 0, "Expected the real report artifact command");
  assert.ok(issueIndex > publishIndex, "Expected report publication before the visible failure");
  assert.ok(failureIndex > issueIndex, "Expected the visible failure before task completion");
  assert.equal((await fs.stat(path.join(directory, "dotsider-size-check.json"))).isFile(), true);
  assert.equal((await fs.stat(path.join(directory, "dotsider-size-check.md"))).isFile(), true);
});

async function runAzure(reportDirectory: string, budgets: string): Promise<ChildResult> {
  return await new Promise<ChildResult>((resolve, reject) => {
    const child = spawn(process.execPath, [runtime], {
      shell: false,
      windowsHide: true,
      env: {
        ...process.env,
        INPUT_TARGET: target,
        INPUT_BASELINE: baseline,
        INPUT_BUDGETS: budgets,
        INPUT_TOP: "10",
        INPUT_WHY: "false",
        INPUT_DOTSIDER_VERSION: "not-a-release",
        INPUT_DOTSIDER_PATH: executable,
        INPUT_REPORT_DIRECTORY: reportDirectory,
        INPUT_PUBLISH_SUMMARY: "true",
        INPUT_PUBLISH_REPORTS: "true",
        INPUT_ARTIFACT_NAME: "dotsider-azure-test",
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
    child.on("close", exitCode => resolve({ exitCode: exitCode ?? 1, stdout, stderr }));
  });
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
