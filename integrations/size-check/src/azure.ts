import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { acquireTool, prepareTool } from "./acquisition";
import { createInputs } from "./input";
import { executeSizeCheck } from "./process";
import { createErrorOutputs, createStableOutputs, formatSizeCheckSummary } from "./report";
import { StableOutputs } from "./types";

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
    const inputs = createInputs({
      target: getInput("target"),
      baseline: getInput("baseline"),
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
    const execution = await executeSizeCheck(executable, inputs);
    const outputs = createStableOutputs(execution, inputs.artifactName, tool.version);
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

    if (execution.exitCode === 0) {
      complete("Succeeded", execution.result === "passed-with-warnings"
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
