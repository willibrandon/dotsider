import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import { createServer, IncomingMessage, ServerResponse } from "node:http";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import { prepareTool } from "../src/acquisition";
import { baselineArtifactName, createBaselineIdentity, stageBaseline } from "../src/baseline";
import { executeSizeCheck } from "../src/process";
import { SizeCheckInputs } from "../src/types";

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
  assert.match(result.stdout, /task\.complete result=Succeeded;/u);
  assert.doesNotMatch(result.stdout, /SucceededWithIssues/u);
  assert.match(result.stdout, /artifact\.upload artifactname=dotsider-baseline-/u);
  const json = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
    baselineSource?: { status?: string };
    budgets?: { hasDeferred?: boolean };
  };
  assert.equal(json.baselineSource?.status, "not-found");
  assert.equal(json.budgets?.hasDeferred, true);
  assert.match(await fs.readFile(path.join(directory, "dotsider-size-check.md"), "utf8"), /DEFERRED/u);
});

test("Azure handler keeps an existing real warning budget Succeeded", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-warning-"));
  const result = await runAzure(directory, {
    baseline,
    budgetFile: path.resolve(__dirname, "../../../integrations/size-check/test/fixtures/warning-budgets.json"),
  });

  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(result.stdout, /variable=result;isOutput=true;\]passed-with-warnings/u);
  assert.match(result.stdout, /task\.complete result=Succeeded;/u);
  assert.doesNotMatch(result.stdout, /SucceededWithIssues/u);
});

test("Azure succeeded deferred baseline is eligible for the next managed run", async () => {
  const firstDirectory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-lifecycle-first-"));
  const secondDirectory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-lifecycle-second-"));
  try {
    const commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const first = await runAzure(firstDirectory, { budgets: "growth=1%" }, undefined, {
      BUILD_SOURCEVERSION: commit,
      BUILD_BUILDID: "100",
      BUILD_BUILDNUMBER: "100",
    });
    assert.equal(first.exitCode, 0, first.stderr);
    assert.match(first.stdout, /variable=result;isOutput=true;\]passed-with-warnings/u);
    assert.match(first.stdout, /task\.complete result=Succeeded;/u);
    assert.doesNotMatch(first.stdout, /SucceededWithIssues/u);

    const artifactName = outputValue(first.stdout, "baselineArtifactName");
    const uploadPath = artifactUploadPath(first.stdout, artifactName);
    const archive = await zipDirectory(uploadPath, artifactName);
    let artifactReads = 0;
    const second = await runAzure(secondDirectory, { budgets: "max=1gb" }, (request, response) => {
      response.setHeader("content-type", "application/json");
      if (request.url?.includes("/_apis/build/builds?")) {
        response.end(JSON.stringify({ value: [{
          id: 100,
          buildNumber: "100",
          sourceBranch: "refs/heads/main",
          sourceVersion: commit,
          result: "succeeded",
        }] }));
      } else if (request.url?.includes("/artifacts?") && request.headers.accept === "application/zip") {
        artifactReads++;
        response.setHeader("content-type", "application/zip");
        response.end(archive);
      } else if (request.url?.includes("/artifacts?")) {
        response.end(JSON.stringify({ name: artifactName }));
      } else {
        response.statusCode = 404;
        response.end(JSON.stringify({ message: "unexpected request" }));
      }
    }, {
      BUILD_SOURCEVERSION: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
      BUILD_BUILDID: "101",
      BUILD_BUILDNUMBER: "101",
    });

    assert.equal(second.exitCode, 0, second.stderr);
    assert.equal(artifactReads, 1);
    assert.equal(outputValue(second.stdout, "baselineStatus"), "restored");
    assert.equal(outputValue(second.stdout, "baselineSourceId"), "100");
    assert.equal(outputValue(second.stdout, "baselineSourceCommit"), commit);
    assert.match(second.stdout, /task\.complete result=Succeeded;/u);
  } finally {
    await fs.rm(firstDirectory, { recursive: true, force: true });
    await fs.rm(secondDirectory, { recursive: true, force: true });
  }
});

test("Azure managed alignment warnings keep real checks and failures correctly classified", async t => {
  const rootDirectory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-alignment-"));
  try {
    const managed = await prepareRealAzureBaseline(rootDirectory);
    let metadataMode: "resolved" | "permission" = "resolved";
    const provider = (request: IncomingMessage, response: ServerResponse) => {
      response.setHeader("content-type", "application/json");
      if (request.url?.includes("/_apis/git/repositories/repository/commits/")) {
        if (metadataMode === "permission") {
          response.statusCode = 403;
          response.end(JSON.stringify({ message: "forbidden" }));
        } else {
          response.end(JSON.stringify({
            commitId: managed.mergeCommit,
            parents: [managed.targetCommit, managed.headCommit],
          }));
        }
      } else if (request.url?.includes("/_apis/build/builds?")) {
        response.end(JSON.stringify({ value: [{
          id: 41,
          buildNumber: "9",
          sourceBranch: "refs/heads/main",
          sourceVersion: managed.baselineCommit,
          result: "succeeded",
        }] }));
      } else if (request.url?.includes("/artifacts?") && request.headers.accept === "application/zip") {
        response.setHeader("content-type", "application/zip");
        response.end(managed.archive);
      } else if (request.url?.includes("/artifacts?")) {
        response.end(JSON.stringify({ name: managed.artifactName }));
      } else {
        response.statusCode = 404;
        response.end(JSON.stringify({ message: "unexpected request" }));
      }
    };
    const prEnvironment: NodeJS.ProcessEnv = {
      BUILD_REASON: "PullRequest",
      BUILD_REPOSITORY_PROVIDER: "TfsGit",
      BUILD_REPOSITORY_ID: "repository",
      BUILD_REPOSITORY_LOCALPATH: path.join(rootDirectory, "not-checked-out"),
      BUILD_SOURCEVERSION: managed.mergeCommit,
      SYSTEM_PULLREQUEST_SOURCECOMMITID: managed.headCommit,
      SYSTEM_PULLREQUEST_TARGETBRANCH: "main",
      BUILD_SOURCEBRANCH: "refs/pull/62/merge",
      BUILD_BUILDID: "99",
      BUILD_BUILDNUMBER: "99",
    };

    for (const scenario of [
      { name: "mismatched", mode: "resolved" as const, expectedReason: "" },
      { name: "unknown", mode: "permission" as const, expectedReason: "permission-denied" },
    ]) {
      await t.test(scenario.name, async () => {
        metadataMode = scenario.mode;
        const directory = path.join(rootDirectory, scenario.name);
        const result = await runAzure(directory, { budgets: "max=1gb" }, provider, prEnvironment);
        assert.equal(result.exitCode, 0, result.stderr);
        assert.equal((result.stdout.match(/task\.logissue type=warning;/gu) ?? []).length, 1);
        assert.equal(outputValue(result.stdout, "result"), "passed-with-warnings");
        assert.equal(outputValue(result.stdout, "baselineComparisonStatus"), scenario.name);
        assert.equal(outputValue(result.stdout, "baselineComparisonReason"), scenario.expectedReason);
        assert.match(result.stdout, /task\.complete result=Succeeded;/u);
        assert.doesNotMatch(result.stdout, /SucceededWithIssues/u);
        const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
          schemaVersion?: number;
          baselineComparison?: { status?: string; reason?: string };
        };
        assert.equal(report.schemaVersion, 2);
        assert.equal(report.baselineComparison?.status, scenario.name);
        assert.match(await fs.readFile(path.join(directory, "dotsider-size-check.md"), "utf8"),
          /> \*\*Warning:\*\*/u);
      });
    }

    await t.test("budget failure", async () => {
      metadataMode = "resolved";
      const directory = path.join(rootDirectory, "budget-failure");
      const result = await runAzure(directory, {
        budgets: ["ns=NativeAotConsole.Telemetry:growth=0", "max=1gb"].join("\n"),
      }, provider, prEnvironment);
      assert.equal(result.exitCode, 1);
      assert.equal((result.stdout.match(/task\.logissue type=warning;/gu) ?? []).length, 1);
      assert.equal(outputValue(result.stdout, "result"), "budget-failed");
      assert.equal(outputValue(result.stdout, "exitCode"), "2");
      const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
        budgets?: { evaluations?: Array<{ violations?: unknown[] }> };
      };
      assert.equal(report.budgets?.evaluations?.length, 2);
      assert.ok((report.budgets?.evaluations?.[0]?.violations?.length ?? 0) > 0);
      assert.equal(report.budgets?.evaluations?.[1]?.violations?.length ?? 0, 0);
      const publish = result.stdout.indexOf("##vso[artifact.upload");
      const error = result.stdout.indexOf("##vso[task.logissue type=error;");
      const failure = result.stdout.indexOf("##vso[task.complete result=Failed;");
      assert.ok(publish >= 0 && error > publish && failure > error);
    });

    await t.test("unknown plus CLI error", async () => {
      metadataMode = "permission";
      const directory = path.join(rootDirectory, "execution-error");
      const result = await runAzure(directory, { budgets: "invalid" }, provider, prEnvironment);
      assert.equal(result.exitCode, 1);
      assert.equal((result.stdout.match(/task\.logissue type=warning;/gu) ?? []).length, 1);
      assert.equal(outputValue(result.stdout, "result"), "error");
      assert.equal(outputValue(result.stdout, "baselineComparisonStatus"), "unknown");
      assert.equal(outputValue(result.stdout, "baselineComparisonReason"), "permission-denied");
      assert.match(result.stdout, /task\.complete result=Failed;/u);
      assert.equal(await fileExists(path.join(directory, "dotsider-size-check.md")), false);
    });
  } finally {
    await fs.rm(rootDirectory, { recursive: true, force: true });
  }
});

async function runAzure(
  reportDirectory: string,
  options: { baseline?: string; budgets?: string; budgetFile?: string },
  requestHandler?: (request: IncomingMessage, response: ServerResponse) => void,
  environment: NodeJS.ProcessEnv = {},
): Promise<ChildResult> {
  const server = createServer(requestHandler ?? ((_request, response) => {
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify({ value: [] }));
  }));
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
        INPUT_BUDGET_FILE: options.budgetFile,
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
        BUILD_SOURCEVERSION: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        BUILD_SOURCESDIRECTORY: path.dirname(target),
        AGENT_TEMPDIRECTORY: reportDirectory,
        ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "test-token",
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

async function prepareRealAzureBaseline(directory: string): Promise<{
  artifactName: string;
  archive: Buffer;
  baselineCommit: string;
  targetCommit: string;
  headCommit: string;
  mergeCommit: string;
}> {
  const tool = await prepareTool("not-a-release", executable);
  const baselineCommit = "1111111111111111111111111111111111111111";
  const targetCommit = "2222222222222222222222222222222222222222";
  const headCommit = "3333333333333333333333333333333333333333";
  const mergeCommit = "4444444444444444444444444444444444444444";
  const identity = createBaselineIdentity(
    "azure-pipelines",
    "project-id/77",
    "size-check",
    target,
    tool.rid,
    undefined,
    path.dirname(target),
    directory,
  );
  const artifactName = baselineArtifactName(identity);
  const execution = await executeSizeCheck(executable, await integrationInputs(directory, {
    target: baseline,
    budgets: ["max=1gb"],
  }));
  assert.equal(execution.result, "passed", execution.stderr);
  assert.ok(execution.report, "Expected a real Dotsider baseline report");
  const staged = path.join(directory, "staged-managed-baseline");
  await stageBaseline(execution.report, identity, {
    status: "restored",
    provider: "azure-pipelines",
    branch: "refs/heads/main",
    commit: baselineCommit,
    id: "41",
    number: "9",
    artifactName,
  }, staged);
  return {
    artifactName,
    archive: await zipDirectory(staged, artifactName),
    baselineCommit,
    targetCommit,
    headCommit,
    mergeCommit,
  };
}

async function integrationInputs(
  directory: string,
  overrides: Partial<SizeCheckInputs>,
): Promise<SizeCheckInputs> {
  return {
    target,
    budgets: [],
    top: 10,
    why: false,
    dotsiderVersion: "custom",
    dotsiderPath: executable,
    reportDirectory: await fs.mkdtemp(path.join(directory, "real-report-")),
    publishSummary: true,
    publishReports: true,
    artifactName: "dotsider-azure-test",
    ...overrides,
  };
}

function outputValue(stdout: string, name: string): string {
  const match = new RegExp(`##vso\\[task\\.setvariable variable=${name};isOutput=true;\\]([^\\r\\n]*)`, "u").exec(stdout);
  assert.ok(match?.[1] !== undefined, `Expected Azure output '${name}'`);
  return match[1];
}

function artifactUploadPath(stdout: string, artifactName: string): string {
  const escaped = artifactName.replace(/[.*+?^${}()|[\]\\]/gu, "\\$&");
  const match = new RegExp(`##vso\\[artifact\\.upload artifactname=${escaped};\\]([^\\r\\n]+)`, "u").exec(stdout);
  assert.ok(match?.[1], `Expected upload command for '${artifactName}'`);
  return match[1];
}

async function zipDirectory(directory: string, rootName: string): Promise<Buffer> {
  const entries: Array<readonly [string, Buffer]> = [];
  const visit = async (current: string): Promise<void> => {
    for (const entry of await fs.readdir(current, { withFileTypes: true })) {
      const fullPath = path.join(current, entry.name);
      if (entry.isDirectory()) await visit(fullPath);
      else if (entry.isFile()) {
        const relative = path.relative(directory, fullPath).split(path.sep).join("/");
        entries.push([`${rootName}/${relative}`, await fs.readFile(fullPath)]);
      }
    }
  };
  await visit(directory);
  return storedZip(entries);
}

function storedZip(entries: readonly (readonly [string, Buffer])[]): Buffer {
  const localEntries: Buffer[] = [];
  const centralEntries: Buffer[] = [];
  let localOffset = 0;
  for (const [name, contents] of entries) {
    const nameBytes = Buffer.from(name);
    const crc = crc32(contents);
    const local = Buffer.alloc(30 + nameBytes.length + contents.length);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(contents.length, 18);
    local.writeUInt32LE(contents.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);
    nameBytes.copy(local, 30);
    contents.copy(local, 30 + nameBytes.length);
    localEntries.push(local);

    const central = Buffer.alloc(46 + nameBytes.length);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(contents.length, 20);
    central.writeUInt32LE(contents.length, 24);
    central.writeUInt16LE(nameBytes.length, 28);
    central.writeUInt32LE(localOffset, 42);
    nameBytes.copy(central, 46);
    centralEntries.push(central);
    localOffset += local.length;
  }
  const centralOffset = localOffset;
  const centralSize = centralEntries.reduce((sum, entry) => sum + entry.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(centralSize, 12);
  end.writeUInt32LE(centralOffset, 16);
  return Buffer.concat([...localEntries, ...centralEntries, end]);
}

function crc32(buffer: Buffer): number {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
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
