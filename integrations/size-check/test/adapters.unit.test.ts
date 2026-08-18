import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";

const repositoryRoot = path.resolve(__dirname, "../../..");
const githubRuntime = path.resolve(__dirname, "../src/github.js");
const azureRuntime = path.resolve(__dirname, "../src/azure.js");
const stableGithubOutputs = [
  "result",
  "exit-code",
  "json-report-path",
  "markdown-report-path",
  "artifact-name",
  "dotsider-version",
  "total-basis",
  "baseline-total",
  "current-total",
  "delta",
  "violation-count",
  "baseline-status",
  "baseline-source-id",
  "baseline-source-commit",
  "baseline-source-url",
  "baseline-artifact-name",
  "baseline-target-commit",
  "baseline-freshness",
];
const stableAzureOutputs = [
  "result",
  "exitCode",
  "jsonReportPath",
  "markdownReportPath",
  "artifactName",
  "dotsiderVersion",
  "totalBasis",
  "baselineTotal",
  "currentTotal",
  "delta",
  "violationCount",
  "baselineStatus",
  "baselineSourceId",
  "baselineSourceCommit",
  "baselineSourceUrl",
  "baselineArtifactName",
  "baselineTargetCommit",
  "baselineFreshness",
];

test("GitHub adapter writes stable error outputs before returning an input error", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-error-"));
  const outputPath = path.join(directory, "github-output.txt");
  const child = await runNode([githubRuntime, "run"], {
    GITHUB_OUTPUT: outputPath,
    DOTSIDER_INPUT_TARGET: "unused",
    DOTSIDER_INPUT_TOP: "10oops",
    DOTSIDER_INPUT_ARTIFACT_NAME: "review-error",
  });

  assert.equal(child.exitCode, 1);
  assert.match(child.stdout, /::error::top must be a non-negative integer/u);
  const outputs = parseGitHubOutputs(await fs.readFile(outputPath, "utf8"));
  assert.deepEqual([...outputs.keys()], stableGithubOutputs);
  assert.equal(outputs.get("result"), "error");
  assert.equal(outputs.get("exit-code"), "1");
  assert.equal(outputs.get("artifact-name"), "review-error");
  assert.equal(outputs.get("json-report-path"), "");
  assert.equal(outputs.get("violation-count"), "0");
});

test("GitHub Action exposes automatic baseline discovery and retention", async () => {
  const action = await fs.readFile(path.join(repositoryRoot, "action.yml"), "utf8");
  assert.match(action, /inputs:\r?\n  target:\r?\n(?:.*\r?\n)*?    required: true/u);
  assert.match(action, /  baseline:\r?\n(?:.*\r?\n)*?    required: false/u);
  assert.doesNotMatch(action, /^  mode:/mu);
  assert.doesNotMatch(action, /DOTSIDER_(?:INPUT_)?MODE|steps\.run\.outputs\.mode/u);
  assert.match(action, /actions\/download-artifact@v8/u);
  assert.match(action, /Find Dotsider baseline/u);
  assert.match(action, /Publish managed Dotsider baseline/u);
  assert.match(action, /  baseline-target-commit:\r?\n(?:.*\r?\n)*?    value: \$\{\{ steps\.run\.outputs\.baseline-target-commit \}\}/u);
  assert.match(action, /  baseline-freshness:\r?\n(?:.*\r?\n)*?    value: \$\{\{ steps\.run\.outputs\.baseline-freshness \}\}/u);

  const uploadStart = action.indexOf("- name: Upload Dotsider reports");
  const enforceStart = action.indexOf("- name: Enforce Dotsider result");
  assert.ok(uploadStart >= 0 && enforceStart > uploadStart);
  const uploadStep = action.slice(uploadStart, enforceStart);
  assert.match(uploadStep, /steps\.run\.outputs\.json-report-path/u);
  assert.match(uploadStep, /steps\.run\.outputs\.markdown-report-path/u);
  assert.match(uploadStep, /steps\.run\.outputs\.baseline-artifact-name/u);
});

test("GitHub enforcement identifies the measured size and retained report", async () => {
  const child = await runNode([githubRuntime, "enforce"], {
    DOTSIDER_EXIT_CODE: "2",
    DOTSIDER_RESULT: "budget-failed",
    DOTSIDER_TOTAL_BASIS: "fileSize",
    DOTSIDER_BASELINE_TOTAL: "",
    DOTSIDER_CURRENT_TOTAL: "36029560",
    DOTSIDER_DELTA: "36029560",
    DOTSIDER_VIOLATION_COUNT: "1",
    DOTSIDER_ARTIFACT_NAME: "dotsider-size-check-osx-arm64",
  });

  assert.equal(child.exitCode, 1);
  assert.match(
    child.stdout,
    /::error::Dotsider size budgets were exceeded: current build \(no baseline comparison\); 34\.4 MB total \(fileSize\); 1 budget violation\. Full report: job summary and 'dotsider-size-check-osx-arm64' artifact\./u,
  );
});

test("GitHub enforcement identifies a comparison with a supplied baseline", async () => {
  const child = await runNode([githubRuntime, "enforce"], {
    DOTSIDER_EXIT_CODE: "2",
    DOTSIDER_RESULT: "budget-failed",
    DOTSIDER_TOTAL_BASIS: "fileSize",
    DOTSIDER_BASELINE_TOTAL: "26214400",
    DOTSIDER_CURRENT_TOTAL: "36029560",
    DOTSIDER_DELTA: "9815160",
    DOTSIDER_VIOLATION_COUNT: "1",
    DOTSIDER_ARTIFACT_NAME: "dotsider-size-check-linux-x64",
  });

  assert.equal(child.exitCode, 1);
  assert.match(child.stdout, /compared with baseline; 34\.4 MB total \(fileSize\); \+9\.4 MB from baseline/u);
});

test("Azure adapter writes stable error outputs before completing an input error", async () => {
  const child = await runNode([azureRuntime], {
    INPUT_TARGET: "unused",
    INPUT_TOP: "10oops",
    INPUT_ARTIFACT_NAME: "review-error",
  });

  assert.equal(child.exitCode, 1);
  const names = [...child.stdout.matchAll(
    /##vso\[task\.setvariable variable=([^;]+);isOutput=true;\]/gu,
  )].map(match => match[1]);
  assert.deepEqual(names, stableAzureOutputs);
  assert.match(child.stdout, /variable=result;isOutput=true;\]error/u);
  assert.match(child.stdout, /variable=exitCode;isOutput=true;\]1/u);
  assert.match(child.stdout, /variable=artifactName;isOutput=true;\]review-error/u);
  const outputIndex = child.stdout.indexOf("variable=result;isOutput=true;]error");
  const issueIndex = child.stdout.indexOf("##vso[task.logissue type=error;");
  const completionIndex = child.stdout.indexOf("##vso[task.complete result=Failed;");
  assert.ok(outputIndex >= 0, "Expected stable error outputs");
  assert.ok(issueIndex > outputIndex, "Expected a visible error after stable outputs");
  assert.ok(completionIndex > issueIndex, "Expected the visible error before task completion");
  assert.match(child.stdout, /task\.logissue type=error;\]top must be a non-negative integer/u);
});

test("Azure task exposes the baseline-driven contract on its supported Node handlers", async () => {
  const taskPath = path.join(
    repositoryRoot,
    "azure-devops/tasks/DotsiderSizeCheckV1/task.json",
  );
  const task = JSON.parse(await fs.readFile(taskPath, "utf8")) as {
    minimumAgentVersion?: string;
    execution?: Record<string, { target?: string }>;
    inputs?: Array<{
      name?: string;
      type?: string;
      defaultValue?: string;
      required?: boolean;
    }>;
    outputVariables?: Array<{ name?: string }>;
    restrictions?: { settableVariables?: { allowed?: string[] } };
  };

  assert.equal(task.minimumAgentVersion, "3.230.2");
  assert.equal(task.execution?.Node24?.target, "runtime/azure.js");
  assert.equal(task.execution?.Node20_1?.target, "runtime/azure.js");

  assert.equal(task.inputs?.some(candidate => candidate.name === "mode"), false);
  assert.equal(task.outputVariables?.some(candidate => candidate.name === "mode"), false);
  assert.equal(task.restrictions?.settableVariables?.allowed?.includes("mode"), false);

  const target = task.inputs?.find(candidate => candidate.name === "target");
  assert.ok(target, "Expected the required target input");
  assert.equal(target.type, "filePath");
  assert.equal(target.required, true);

  for (const name of ["baseline", "baselineKey", "budgetFile", "dotsiderPath"]) {
    const input: {
      name?: string;
      type?: string;
      defaultValue?: string;
      required?: boolean;
    } | undefined = task.inputs?.find(candidate => candidate.name === name);
    assert.ok(input, `Expected the ${name} task input`);
    assert.equal(
      input.type,
      "string",
      `${name} must be a string so Azure does not root an empty filePath input`,
    );
    assert.equal(input.defaultValue, "");
    assert.equal(input.required, false);
  }

  assert.deepEqual(task.outputVariables?.map(output => output.name), stableAzureOutputs);
});

async function runNode(args: readonly string[], environment: NodeJS.ProcessEnv): Promise<ChildResult> {
  return await new Promise<ChildResult>((resolve, reject) => {
    const child = spawn(process.execPath, [...args], {
      shell: false,
      windowsHide: true,
      env: { ...process.env, ...environment },
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
