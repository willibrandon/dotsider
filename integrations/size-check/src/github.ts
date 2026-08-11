import * as fs from "node:fs/promises";
import * as os from "node:os";
import { acquireTool, prepareTool } from "./acquisition";
import { createInputs } from "./input";
import { executeSizeCheck } from "./process";
import { createErrorOutputs, createStableOutputs } from "./report";
import { PreparedTool, StableOutputs } from "./types";

void main();

async function main(): Promise<void> {
  const mode = process.argv[2];
  let errorOutputs = createErrorOutputs(
    optional(process.env.DOTSIDER_INPUT_ARTIFACT_NAME) || "dotsider-size-check",
    optional(process.env.DOTSIDER_PREPARED_VERSION) || "",
  );
  try {
    switch (mode) {
      case "prepare":
        await prepare();
        break;
      case "run":
        await run(outputs => {
          errorOutputs = { ...outputs, result: "error", exitCode: "1" };
        });
        break;
      case "enforce":
        enforce();
        break;
      default:
        throw new Error("Expected the GitHub adapter mode: prepare, run, or enforce.");
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (mode === "run") {
      writeStableOutputs(errorOutputs);
    }
    command("error", {}, message);
    process.exitCode = 1;
  }
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
  const inputs = createInputs({
    target: process.env.DOTSIDER_INPUT_TARGET,
    baseline: process.env.DOTSIDER_INPUT_BASELINE,
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

  const tool = preparedTool();
  const executable = await acquireTool(tool);
  const execution = await executeSizeCheck(executable, inputs);
  const outputs = createStableOutputs(execution, inputs.artifactName, tool.version);
  onOutputs(outputs);
  writeStableOutputs(outputs);

  if (inputs.publishSummary && await fileExists(execution.markdownReportPath)) {
    const summaryPath = process.env.GITHUB_STEP_SUMMARY;
    if (summaryPath) {
      const markdown = await fs.readFile(execution.markdownReportPath, "utf8");
      await fs.appendFile(summaryPath, `${markdown.trimEnd()}\n`);
    }
  }
}

function enforce(): void {
  const exitCode = Number.parseInt(process.env.DOTSIDER_EXIT_CODE || "1", 10);
  const result = process.env.DOTSIDER_RESULT || "error";
  if (exitCode === 0) {
    return;
  }
  if (exitCode === 2) {
    command("error", {}, "Dotsider size budgets were exceeded.");
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

async function fileExists(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(filePath)).isFile();
  } catch {
    return false;
  }
}
