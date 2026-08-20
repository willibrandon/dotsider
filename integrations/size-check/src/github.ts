import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { acquireTool, prepareTool } from "./acquisition";
import { enrichReports, formatBaselineWarning, normalizeCommit, restoreBaseline, stageBaseline } from "./baseline";
import { discoverGithubBaseline } from "./github-baseline";
import { createInputs } from "./input";
import { executeSizeCheck } from "./process";
import { createErrorOutputs, createStableOutputs, formatSizeCheckSummary } from "./report";
import { BaselineComparison, BaselineDiscovery, BaselineSource, PreparedTool, StableOutputs } from "./types";

void main();

async function main(): Promise<void> {
  const commandName = process.argv[2];
  let errorOutputs = createErrorOutputs(
    optional(process.env.DOTSIDER_INPUT_ARTIFACT_NAME) || "dotsider-size-check",
    optional(process.env.DOTSIDER_PREPARED_VERSION) || "",
  );
  try {
    switch (commandName) {
      case "prepare":
        await prepare();
        break;
      case "run":
        await run(outputs => {
          errorOutputs = { ...outputs, result: "error", exitCode: "1" };
        });
        break;
      case "discover":
        await discover();
        break;
      case "enforce":
        enforce();
        break;
      default:
        throw new Error("Expected a GitHub adapter command: prepare, discover, run, or enforce.");
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (commandName === "run") {
      writeStableOutputs(errorOutputs);
    }
    command("error", {}, message);
    process.exitCode = 1;
  }
}

async function discover(): Promise<void> {
  const inputs = githubInputs();
  const discovery = await discoverGithubBaseline(inputs, requiredEnvironment("DOTSIDER_PREPARED_RID"));
  writeOutputs({
    status: discovery.source.status,
    "artifact-name": discovery.artifactName,
    "run-id": discovery.runId ?? "",
    "download-directory": discovery.downloadDirectory ?? "",
    publish: String(discovery.publish),
    identity: JSON.stringify(discovery.identity),
    source: JSON.stringify(discovery.source),
    comparison: discovery.comparison ? JSON.stringify(discovery.comparison) : "",
  });
}

async function prepare(): Promise<void> {
  const tool = await prepareTool(
    process.env.DOTSIDER_INPUT_VERSION || "latest",
    optional(process.env.DOTSIDER_INPUT_PATH),
  );
  writeOutputs({
    version: tool.version,
    rid: tool.rid,
    "cache-directory": tool.cacheDirectory,
    "executable-path": tool.executablePath,
    "cache-key": tool.cacheKey,
    explicit: String(tool.explicit),
  });
}

async function run(onOutputs: (outputs: StableOutputs) => void): Promise<void> {
  let inputs = githubInputs();

  const tool = preparedTool();
  const executable = await acquireTool(tool);
  const discovery = process.env.DOTSIDER_BASELINE_SOURCE
    ? preparedDiscovery()
    : await discoverGithubBaseline(inputs, tool.rid);
  let source = discovery.source;
  const comparison = discovery.comparison;
  onOutputs(createErrorOutputs(inputs.artifactName, tool.version, source, comparison));
  if (source.status === "restored") {
    const directory = requiredEnvironment("DOTSIDER_BASELINE_DOWNLOAD_DIRECTORY");
    const restored = await restoreBaseline(directory, discovery.identity, source);
    inputs = { ...inputs, baseline: restored.targetPath };
  }

  const baselineWarning = formatBaselineWarning(source, comparison);
  if (baselineWarning) command("warning", {}, baselineWarning);

  const execution = await executeSizeCheck(executable, inputs, source.status === "not-found");
  if (execution.report && await fileExists(execution.markdownReportPath)) {
    execution.report = await enrichReports(
      execution.jsonReportPath,
      execution.markdownReportPath,
      source,
      comparison,
    );
  }
  const outputs = createStableOutputs(execution, inputs.artifactName, tool.version, source, comparison);
  onOutputs(outputs);
  writeStableOutputs(outputs);

  let baselineUploadPath = "";
  let publishBaseline = false;
  if (discovery.publish && execution.report
      && (execution.result === "passed" || execution.result === "passed-with-warnings")) {
    source = currentGithubSource(discovery.artifactName);
    baselineUploadPath = await stageBaseline(
      execution.report,
      discovery.identity,
      source,
      pathForBaseline(inputs.reportDirectory),
    );
    publishBaseline = true;
  }
  writeOutputs({
    "baseline-upload-path": baselineUploadPath,
    "publish-baseline": String(publishBaseline),
  });
  const summary = formatSizeCheckSummary(outputs);
  if (summary) {
    process.stdout.write(`Dotsider size check: ${summary}.\n`);
  }

  if (inputs.publishSummary && await fileExists(execution.markdownReportPath)) {
    const summaryPath = process.env.GITHUB_STEP_SUMMARY;
    if (summaryPath) {
      const markdown = await fs.readFile(execution.markdownReportPath, "utf8");
      await fs.appendFile(summaryPath, `${markdown.trimEnd()}\n`);
    }
  }
}

function githubInputs() {
  return createInputs({
    target: process.env.DOTSIDER_INPUT_TARGET,
    baseline: process.env.DOTSIDER_INPUT_BASELINE,
    baselineKey: process.env.DOTSIDER_INPUT_BASELINE_KEY,
    budgets: process.env.DOTSIDER_INPUT_BUDGETS,
    budgetFile: process.env.DOTSIDER_INPUT_BUDGET_FILE,
    top: process.env.DOTSIDER_INPUT_TOP,
    why: process.env.DOTSIDER_INPUT_WHY,
    dotsiderVersion: process.env.DOTSIDER_INPUT_VERSION,
    dotsiderPath: process.env.DOTSIDER_INPUT_PATH,
    reportDirectory: process.env.DOTSIDER_INPUT_REPORT_DIRECTORY,
    publishSummary: process.env.DOTSIDER_INPUT_PUBLISH_SUMMARY,
    publishReports: process.env.DOTSIDER_INPUT_PUBLISH_REPORTS,
    artifactName: process.env.DOTSIDER_INPUT_ARTIFACT_NAME,
  }, process.env.RUNNER_TEMP || os.tmpdir());
}

function preparedDiscovery(): BaselineDiscovery {
  return {
    source: JSON.parse(requiredEnvironment("DOTSIDER_BASELINE_SOURCE")) as BaselineSource,
    comparison: optional(process.env.DOTSIDER_BASELINE_COMPARISON)
      ? JSON.parse(requiredEnvironment("DOTSIDER_BASELINE_COMPARISON")) as BaselineComparison
      : undefined,
    identity: JSON.parse(requiredEnvironment("DOTSIDER_BASELINE_IDENTITY")) as BaselineDiscovery["identity"],
    artifactName: requiredEnvironment("DOTSIDER_BASELINE_ARTIFACT_NAME"),
    downloadDirectory: optional(process.env.DOTSIDER_BASELINE_DOWNLOAD_DIRECTORY),
    publish: process.env.DOTSIDER_BASELINE_PUBLISH === "true",
  };
}

function currentGithubSource(artifactName: string): BaselineSource {
  const repository = requiredEnvironment("GITHUB_REPOSITORY");
  const runId = requiredEnvironment("GITHUB_RUN_ID");
  return {
    status: "restored",
    provider: "github-actions",
    branch: process.env.GITHUB_REF_NAME,
    commit: requiredCommit(process.env.GITHUB_SHA, "GITHUB_SHA"),
    id: runId,
    number: process.env.GITHUB_RUN_NUMBER,
    url: `${process.env.GITHUB_SERVER_URL || "https://github.com"}/${repository}/actions/runs/${runId}`,
    artifactName,
  };
}

function pathForBaseline(reportDirectory: string): string {
  return path.join(reportDirectory, ".baseline");
}

function enforce(): void {
  const exitCode = Number.parseInt(process.env.DOTSIDER_EXIT_CODE || "1", 10);
  const result = process.env.DOTSIDER_RESULT || "error";
  if (exitCode === 0) {
    return;
  }
  if (exitCode === 2) {
    const summary = formatSizeCheckSummary({
      totalBasis: process.env.DOTSIDER_TOTAL_BASIS || "",
      baselineTotal: process.env.DOTSIDER_BASELINE_TOTAL || "",
      currentTotal: process.env.DOTSIDER_CURRENT_TOTAL || "",
      delta: process.env.DOTSIDER_DELTA || "",
      violationCount: process.env.DOTSIDER_VIOLATION_COUNT || "",
    });
    const artifactName = process.env.DOTSIDER_ARTIFACT_NAME || "dotsider-size-check";
    command(
      "error",
      {},
      `Dotsider size budgets were exceeded${summary ? `: ${summary}` : ""}. Full report: job summary and '${artifactName}' artifact.`,
    );
  } else {
    command("error", {}, `Dotsider size check failed with exit code ${exitCode} (${result}).`);
  }
  process.exitCode = 1;
}

function preparedTool(): PreparedTool {
  const version = requiredEnvironment("DOTSIDER_PREPARED_VERSION");
  const rid = requiredEnvironment("DOTSIDER_PREPARED_RID");
  const cacheDirectory = requiredEnvironment("DOTSIDER_PREPARED_CACHE_DIRECTORY");
  const executablePath = requiredEnvironment("DOTSIDER_PREPARED_EXECUTABLE_PATH");
  return {
    version,
    rid,
    cacheDirectory,
    executablePath,
    cacheKey: requiredEnvironment("DOTSIDER_PREPARED_CACHE_KEY"),
    explicit: process.env.DOTSIDER_PREPARED_EXPLICIT === "true",
  };
}

function writeStableOutputs(outputs: StableOutputs): void {
  writeOutputs({
    result: outputs.result,
    "exit-code": outputs.exitCode,
    "json-report-path": outputs.jsonReportPath,
    "markdown-report-path": outputs.markdownReportPath,
    "artifact-name": outputs.artifactName,
    "dotsider-version": outputs.dotsiderVersion,
    "total-basis": outputs.totalBasis,
    "baseline-total": outputs.baselineTotal,
    "current-total": outputs.currentTotal,
    delta: outputs.delta,
    "violation-count": outputs.violationCount,
    "baseline-status": outputs.baselineStatus,
    "baseline-source-id": outputs.baselineSourceId,
    "baseline-source-commit": outputs.baselineSourceCommit,
    "baseline-source-url": outputs.baselineSourceUrl,
    "baseline-artifact-name": outputs.baselineArtifactName,
    "baseline-target-commit": outputs.baselineTargetCommit,
    "baseline-comparison-status": outputs.baselineComparisonStatus,
    "baseline-comparison-reason": outputs.baselineComparisonReason,
  });
}

function writeOutputs(outputs: Readonly<Record<string, string>>): void {
  const outputPath = requiredEnvironment("GITHUB_OUTPUT");
  const records = Object.entries(outputs).map(([name, value]) => {
    const delimiter = `dotsider_${crypto.randomUUID()}`;
    return `${name}<<${delimiter}\n${value}\n${delimiter}\n`;
  });
  require("node:fs").appendFileSync(outputPath, records.join(""), "utf8");
}

function command(name: string, properties: Readonly<Record<string, string>>, message: string): void {
  const serialized = Object.entries(properties)
    .map(([key, value]) => `${key}=${escapeProperty(value)}`)
    .join(",");
  process.stdout.write(`::${name}${serialized ? ` ${serialized}` : ""}::${escapeData(message)}\n`);
}

function escapeData(value: string): string {
  return value.replaceAll("%", "%25").replaceAll("\r", "%0D").replaceAll("\n", "%0A");
}

function escapeProperty(value: string): string {
  return escapeData(value).replaceAll(":", "%3A").replaceAll(",", "%2C");
}

function optional(value: string | undefined): string | undefined {
  return value?.trim() || undefined;
}

function requiredEnvironment(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Required environment variable ${name} was not provided.`);
  }
  return value;
}

function requiredCommit(value: string | undefined, name: string): string {
  const commit = normalizeCommit(value);
  if (!commit) throw new Error(`Required environment variable ${name} did not contain a full commit ID.`);
  return commit;
}

async function fileExists(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(filePath)).isFile();
  } catch {
    return false;
  }
}
