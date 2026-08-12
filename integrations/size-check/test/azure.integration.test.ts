import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import { createServer } from "node:http";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";

const executable = required("DOTSIDER_INTEGRATION_EXE");
const baseline = required("DOTSIDER_INTEGRATION_BASELINE");
const target = required("DOTSIDER_INTEGRATION_TARGET");
const runtime = path.resolve(__dirname, "../../../azure-devops/tasks/DotsiderSizeCheckV1/runtime/azure.js");

test("Azure handler emits outputs from a real comparison", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-pass-"));
  const result = await runAzure(directory, {
    baseline,
    budgets: "total:max=1gb,growth=1gb",
  });
  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(result.stdout, /variable=result;isOutput=true;\]passed/u);
  assert.match(result.stdout, /variable=exitCode;isOutput=true;\]0/u);
  assert.doesNotMatch(result.stdout, /variable=mode;isOutput=true;/u);
  assert.match(result.stdout, /##vso\[task.uploadsummary /u);
  assert.match(result.stdout, /##vso\[artifact.upload artifactname=dotsider-azure-test;/u);

  const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
    schemaVersion: number;
    baseline?: string;
    leftTotal?: number;
    summary: { delta: number };
  };
  assert.equal(report.schemaVersion, 2);
  assert.equal(report.baseline, path.resolve(baseline));
  assert.ok((report.leftTotal ?? 0) > 0);
  assert.notEqual(report.summary.delta, 0);
});

test("Azure handler publishes real reports before a budget failure", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-fail-"));
  const result = await runAzure(directory, {
    baseline,
    budgets: "ns=NativeAotConsole.Telemetry:growth=0",
  });
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

test("Azure handler accepts an absolute budget without a baseline", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-current-"));
  const result = await runAzure(directory, { budgets: "max=1gb" });

  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(result.stdout, /variable=result;isOutput=true;\]passed/u);
  assert.match(result.stdout, /variable=baselineTotal;isOutput=true;\]\r?$/mu);
  assert.match(result.stdout, /Dotsider size check: current build \(no baseline comparison\);/u);
  const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
    schemaVersion?: number;
    baseline?: string | null;
    rightTotal?: number;
  };
  assert.equal(report.schemaVersion, 2);
  assert.equal(report.baseline ?? null, null);
  assert.ok((report.rightTotal ?? 0) > 0);
  assert.equal((await fs.stat(path.join(directory, "dotsider-size-check.md"))).isFile(), true);
});

test("Azure handler defers growth on a confirmed first run", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-growth-first-run-"));
  const result = await runAzure(directory, { budgets: "growth=1%" });

  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(result.stdout, /variable=result;isOutput=true;\]passed-with-warnings/u);
  assert.match(result.stdout, /variable=baselineStatus;isOutput=true;\]not-found/u);
  const json = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
    baselineSource?: { status?: string };
    budgets?: { hasDeferred?: boolean };
  };
  assert.equal(json.baselineSource?.status, "not-found");
  assert.equal(json.budgets?.hasDeferred, true);
  assert.match(await fs.readFile(path.join(directory, "dotsider-size-check.md"), "utf8"), /DEFERRED/u);
});

async function runAzure(
  reportDirectory: string,
  options: { baseline?: string; budgets?: string },
): Promise<ChildResult> {
  const server = createServer((_request, response) => {
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify({ value: [] }));
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  assert.ok(address && typeof address !== "string");
  return await new Promise<ChildResult>((resolve, reject) => {
    const child = spawn(process.execPath, [runtime], {
      shell: false,
      windowsHide: true,
      env: {
        ...process.env,
        INPUT_TARGET: target,
        INPUT_BASELINE: options.baseline,
        INPUT_BUDGETS: options.budgets,
        INPUT_TOP: "10",
        INPUT_WHY: "false",
        INPUT_DOTSIDER_VERSION: "not-a-release",
        INPUT_DOTSIDER_PATH: executable,
        INPUT_REPORT_DIRECTORY: reportDirectory,
        INPUT_PUBLISH_SUMMARY: "true",
        INPUT_PUBLISH_REPORTS: "true",
        INPUT_ARTIFACT_NAME: "dotsider-azure-test",
        SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: `http://127.0.0.1:${address.port}`,
        SYSTEM_TEAMPROJECTID: "project-id",
        SYSTEM_TEAMPROJECT: "project",
        SYSTEM_DEFINITIONID: "77",
        SYSTEM_JOBNAME: "size-check",
        BUILD_SOURCEBRANCH: "refs/heads/main",
        BUILD_BUILDID: "100",
        BUILD_BUILDNUMBER: "100",
        BUILD_SOURCEVERSION: "current-sha",
        BUILD_SOURCESDIRECTORY: path.dirname(target),
        AGENT_TEMPDIRECTORY: reportDirectory,
        ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "test-token",
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (value: string) => stdout += value);
    child.stderr.on("data", (value: string) => stderr += value);
    child.on("error", error => {
      server.close();
      reject(error);
    });
    child.on("close", exitCode => {
      server.close();
      resolve({ exitCode: exitCode ?? 1, stdout, stderr });
    });
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

async function fileExists(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(filePath)).isFile();
  } catch {
    return false;
  }
}
