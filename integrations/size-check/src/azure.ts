import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { acquireTool, prepareTool } from "./acquisition";
import { discoverAzureBaseline } from "./azure-baseline";
import { enrichReports, formatBaselineWarning, restoreBaseline, stageBaseline } from "./baseline";
import { createInputs } from "./input";
import { executeSizeCheck } from "./process";
import { createErrorOutputs, createStableOutputs, formatSizeCheckSummary } from "./report";
import { BaselineSource, StableOutputs } from "./types";

if (require.main === module) {
  void main();
}

async function main(): Promise<void> {
  let artifactName = getInput("artifactName") || "dotsider-size-check";
  let dotsiderVersion = "";
  let errorOutputs = createErrorOutputs(artifactName, dotsiderVersion);
  try {
    const defaultRoot = process.env.BUILD_ARTIFACTSTAGINGDIRECTORY
      || process.env.AGENT_TEMPDIRECTORY
      || os.tmpdir();
    let inputs = createInputs({
      target: getInput("target"),
      baseline: getInput("baseline"),
      baselineKey: getInput("baselineKey"),
      budgets: getInput("budgets"),
      budgetFile: getInput("budgetFile"),
      top: getInput("top"),
      why: getInput("why"),
      dotsiderVersion: getInput("dotsiderVersion"),
      dotsiderPath: getInput("dotsiderPath"),
      reportDirectory: getInput("reportDirectory"),
      publishSummary: getInput("publishSummary"),
      publishReports: getInput("publishReports"),
      artifactName: getInput("artifactName"),
    }, defaultRoot);
    artifactName = inputs.artifactName;

    const tool = await prepareTool(inputs.dotsiderVersion, inputs.dotsiderPath);
    dotsiderVersion = tool.version;
    const executable = await acquireTool(tool);
    const discovery = await discoverAzureBaseline(inputs, tool.rid);
    if (discovery.source.status === "restored") {
      const restored = await restoreBaseline(
        discovery.downloadDirectory || "",
        discovery.identity,
        discovery.source,
      );
      inputs = { ...inputs, baseline: restored.targetPath };
    }
    const baselineWarning = formatBaselineWarning(discovery.source);
    if (baselineWarning) vso("task.logissue", { type: "warning" }, baselineWarning);
    const execution = await executeSizeCheck(
      executable,
      inputs,
      discovery.source.status === "not-found",
    );
    if (execution.report && await fileExists(execution.markdownReportPath)) {
      execution.report = await enrichReports(
        execution.jsonReportPath,
        execution.markdownReportPath,
        discovery.source,
      );
    }
    const outputs = createStableOutputs(
      execution,
      inputs.artifactName,
      tool.version,
      discovery.source,
    );
    errorOutputs = { ...outputs, result: "error", exitCode: "1" };
    writeStableOutputs(outputs);
    const summary = formatSizeCheckSummary(outputs);
    if (summary) {
      process.stdout.write(`Dotsider size check: ${summary}.${os.EOL}`);
    }

    if (inputs.publishSummary && await fileExists(execution.markdownReportPath)) {
      vso("task.uploadsummary", {}, execution.markdownReportPath);
    }
    if (inputs.publishReports
      && await fileExists(execution.jsonReportPath)
      && await fileExists(execution.markdownReportPath)) {
      vso("artifact.upload", { artifactname: inputs.artifactName }, inputs.reportDirectory);
    }
    if (discovery.publish && execution.report
      && (outputs.result === "passed" || outputs.result === "passed-with-warnings")) {
      const baselineDirectory = path.join(
        process.env.AGENT_TEMPDIRECTORY || os.tmpdir(),
        "dotsider-baseline-upload",
        discovery.artifactName,
      );
      await stageBaseline(
        execution.report,
        discovery.identity,
        currentAzureSource(discovery.artifactName),
        baselineDirectory,
      );
      vso("artifact.upload", { artifactname: discovery.artifactName }, baselineDirectory);
    }

    if (execution.exitCode === 0) {
      complete("Succeeded", outputs.result === "passed-with-warnings"
        ? "Dotsider size check passed with warnings."
        : "Dotsider size check passed.");
      return;
    }
    if (execution.exitCode === 2) {
      complete("Failed", `Dotsider size budgets were exceeded${summary ? `: ${summary}` : ""}.`);
    } else {
      complete("Failed", `Dotsider size check failed with exit code ${execution.exitCode}.`);
    }
    process.exitCode = 1;
  } catch (error) {
    if (errorOutputs.artifactName !== artifactName || errorOutputs.dotsiderVersion !== dotsiderVersion) {
      errorOutputs = createErrorOutputs(artifactName, dotsiderVersion);
    }
    writeStableOutputs(errorOutputs);
    complete("Failed", error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}

function currentAzureSource(artifactName: string): BaselineSource {
  const collection = (process.env.SYSTEM_TEAMFOUNDATIONCOLLECTIONURI || "").replace(/\/$/u, "");
  const project = process.env.SYSTEM_TEAMPROJECT || process.env.SYSTEM_TEAMPROJECTID || "";
  const buildId = process.env.BUILD_BUILDID || "";
  return {
    status: "restored",
    provider: "azure-pipelines",
    branch: process.env.BUILD_SOURCEBRANCH,
    commit: process.env.BUILD_SOURCEVERSION,
    id: buildId,
    number: process.env.BUILD_BUILDNUMBER,
    url: collection && project && buildId
      ? `${collection}/${encodeURIComponent(project)}/_build/results?buildId=${buildId}`
      : undefined,
    artifactName,
  };
}

export function getInput(name: string): string | undefined {
  const variants = [
    `INPUT_${name}`,
    `INPUT_${name.toUpperCase()}`,
    `INPUT_${name.replace(/([a-z])([A-Z])/gu, "$1_$2").toUpperCase()}`,
  ];
  for (const variant of variants) {
    const value = process.env[variant];
    if (value !== undefined && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

export function escapeVsoProperty(value: string): string {
  return escapeVsoMessage(value).replaceAll(";", "%3B").replaceAll("]", "%5D");
}

export function escapeVsoMessage(value: string): string {
  return value
    .replaceAll("%", "%AZP25")
    .replaceAll("\r", "%0D")
    .replaceAll("\n", "%0A");
}

function setOutput(name: string, value: string): void {
  vso("task.setvariable", { variable: name, isOutput: "true" }, value);
}

function writeStableOutputs(outputs: StableOutputs): void {
  for (const [name, value] of Object.entries(outputs)) {
    setOutput(name, value);
  }
}

function complete(result: "Succeeded" | "Failed", message: string): void {
  if (result === "Failed") {
    vso("task.logissue", { type: "error" }, message);
  }
  vso("task.complete", { result }, message);
}

function vso(command: string, properties: Readonly<Record<string, string>>, message: string): void {
  const serialized = Object.entries(properties)
    .map(([key, value]) => `${key}=${escapeVsoProperty(value)};`)
    .join("");
  process.stdout.write(`##vso[${command} ${serialized}]${escapeVsoMessage(message)}${os.EOL}`);
}

async function fileExists(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(path.resolve(filePath))).isFile();
  } catch {
    return false;
  }
}
