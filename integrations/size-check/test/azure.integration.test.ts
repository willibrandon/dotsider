import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import { createServer } from "node:http";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import { baselineArtifactName, createBaselineIdentity, detectTargetRid, stageBaseline } from "../src/baseline";
import { SizeReport } from "../src/types";

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

test("Azure handler warns for a stale managed baseline while completing the real comparison", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-stale-baseline-"));
  const baselineMstat = path.join(path.dirname(baseline), "NativeAotConsole.mstat");
  const rid = await detectTargetRid(target, "unknown");
  const identity = createBaselineIdentity(
    "azure-pipelines", "project-id/77", "size-check", target, rid, undefined,
    path.dirname(target), directory,
  );
  const artifactName = baselineArtifactName(identity);
  const baselineCommit = "1111111111111111111111111111111111111111";
  const targetCommit = "2222222222222222222222222222222222222222";
  const mergeCommit = "3333333333333333333333333333333333333333";
  const source = {
    status: "restored" as const,
    provider: "azure-pipelines" as const,
    branch: "refs/heads/main",
    commit: baselineCommit,
    id: "304",
    number: "20260818.1",
    url: "https://example.test/build/304",
    artifactName,
  };
  const baselineReport: SizeReport = {
    schemaVersion: 2,
    target: baseline,
    targetArtifacts: {
      inputPath: baseline,
      binaryPath: baseline,
      mstatPath: baselineMstat,
    },
    totalBasis: "fileSize",
    leftTotal: null,
    rightTotal: 1,
    summary: { delta: 1 },
  };
  const staged = path.join(directory, "staged");
  await stageBaseline(baselineReport, identity, source, staged);
  const archive = storedZip([
    [`${artifactName}/dotsider-baseline.json`, await fs.readFile(path.join(staged, "dotsider-baseline.json"))],
    [`${artifactName}/files/target${path.extname(baseline)}`, await fs.readFile(path.join(staged, "files", `target${path.extname(baseline)}`))],
    [`${artifactName}/files/target.mstat`, await fs.readFile(path.join(staged, "files", "target.mstat"))],
  ]);

  const server = createServer((request, response) => {
    if (request.headers.authorization !== "Bearer test-token") {
      response.statusCode = 401;
      response.end("missing token");
      return;
    }
    if (request.url?.includes("/_apis/git/repositories/repository/commits/")) {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({
        commitId: mergeCommit,
        parents: [targetCommit, "4444444444444444444444444444444444444444"],
      }));
    } else if (request.url?.includes("/_apis/build/builds?")) {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ value: [{
        id: 304,
        buildNumber: "20260818.1",
        sourceBranch: "refs/heads/main",
        sourceVersion: baselineCommit,
        result: "succeeded",
        _links: { web: { href: "https://example.test/build/304" } },
      }] }));
    } else if (request.url?.includes("/artifacts?") && request.headers.accept === "application/zip") {
      response.setHeader("content-type", "application/zip");
      response.end(archive);
    } else if (request.url?.includes("/artifacts?")) {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ name: artifactName }));
    } else {
      response.statusCode = 404;
      response.end("unexpected request");
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const result = await runAzureProcess(directory, {
      INPUT_BUDGETS: "total:max=1gb,growth=1gb",
      SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: `http://127.0.0.1:${address.port}`,
      BUILD_REASON: "PullRequest",
      SYSTEM_PULLREQUEST_TARGETBRANCH: "main",
      BUILD_SOURCEBRANCH: "refs/pull/62/merge",
      BUILD_REPOSITORY_PROVIDER: "TfsGit",
      BUILD_REPOSITORY_ID: "repository",
      BUILD_SOURCEVERSION: mergeCommit,
    });

    assert.equal(result.exitCode, 0, result.stderr);
    assert.match(result.stdout, /##vso\[task\.logissue type=warning;\]The managed baseline does not match this pull request target/u);
    assert.match(result.stdout, /222222222222.*111111111111.*Azure Pipelines build 20260818\.1/u);
    assert.match(result.stdout, /variable=result;isOutput=true;\]passed-with-warnings/u);
    assert.match(result.stdout, new RegExp(`variable=baselineTargetCommit;isOutput=true;\\]${targetCommit}`, "u"));
    assert.match(result.stdout, /variable=baselineFreshness;isOutput=true;\]stale/u);
    const report = JSON.parse(await fs.readFile(path.join(directory, "dotsider-size-check.json"), "utf8")) as {
      baselineSource?: { targetCommit?: string; freshness?: string };
    };
    assert.equal(report.baselineSource?.targetCommit, targetCommit);
    assert.equal(report.baselineSource?.freshness, "stale");
    assert.match(await fs.readFile(path.join(directory, "dotsider-size-check.md"), "utf8"), /> \*\*Warning:\*\*/u);
  } finally {
    server.close();
  }
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
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    return await runAzureProcess(reportDirectory, {
      INPUT_BASELINE: options.baseline,
      INPUT_BUDGETS: options.budgets,
      SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: `http://127.0.0.1:${address.port}`,
    });
  } finally {
    server.close();
  }
}

async function runAzureProcess(
  reportDirectory: string,
  environment: NodeJS.ProcessEnv,
): Promise<ChildResult> {
  return await new Promise<ChildResult>((resolve, reject) => {
    const child = spawn(process.execPath, [runtime], {
      shell: false,
      windowsHide: true,
      env: {
        ...process.env,
        INPUT_TARGET: target,
        INPUT_BASELINE: undefined,
        INPUT_BUDGETS: undefined,
        INPUT_TOP: "10",
        INPUT_WHY: "false",
        INPUT_DOTSIDER_VERSION: "not-a-release",
        INPUT_DOTSIDER_PATH: executable,
        INPUT_REPORT_DIRECTORY: reportDirectory,
        INPUT_PUBLISH_SUMMARY: "true",
        INPUT_PUBLISH_REPORTS: "true",
        INPUT_ARTIFACT_NAME: "dotsider-azure-test",
        SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: undefined,
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
    child.on("close", exitCode => {
      resolve({ exitCode: exitCode ?? 1, stdout, stderr });
    });
  });
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
  const central = Buffer.concat(centralEntries);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(entries.length, 8);
  eocd.writeUInt16LE(entries.length, 10);
  eocd.writeUInt32LE(central.length, 12);
  eocd.writeUInt32LE(localOffset, 16);
  return Buffer.concat([...localEntries, central, eocd]);
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
