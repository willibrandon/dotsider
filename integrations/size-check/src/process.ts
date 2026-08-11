import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import * as path from "node:path";
import { buildSizeCheckArguments, classifyResult, readSizeReport } from "./report";
import { ProcessResult, SizeCheckExecution, SizeCheckInputs } from "./types";

export async function runProcess(fileName: string, args: readonly string[]): Promise<ProcessResult> {
  return await new Promise<ProcessResult>((resolve, reject) => {
    const child = spawn(fileName, [...args], {
      shell: false,
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";

    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (data: string) => {
      stdout += data;
      process.stdout.write(data);
    });
    child.stderr.on("data", (data: string) => {
      stderr += data;
      process.stderr.write(data);
    });
    child.on("error", reject);
    child.on("close", (exitCode, signal) => {
      if (exitCode === null) {
        reject(new Error(`${fileName} terminated by signal ${signal ?? "unknown"}.`));
        return;
      }
      resolve({ exitCode, stdout, stderr });
    });
  });
}

export async function executeSizeCheck(
  executablePath: string,
  inputs: SizeCheckInputs,
): Promise<SizeCheckExecution> {
  await fs.mkdir(inputs.reportDirectory, { recursive: true });
  const jsonReportPath = path.join(inputs.reportDirectory, "dotsider-size-check.json");
  const markdownReportPath = path.join(inputs.reportDirectory, "dotsider-size-check.md");
  await Promise.all([
    fs.rm(jsonReportPath, { force: true }),
    fs.rm(markdownReportPath, { force: true }),
  ]);

  const args = buildSizeCheckArguments(
    inputs.target,
    inputs.baseline,
    inputs.budgets,
    inputs.budgetFile,
    inputs.top,
    inputs.why,
    jsonReportPath,
    markdownReportPath,
  );
  const processResult = await runProcess(executablePath, args);

  let report;
  try {
    report = await readSizeReport(jsonReportPath);
  } catch (error) {
    if (processResult.exitCode === 0 || processResult.exitCode === 2) {
      const message = error instanceof Error ? error.message : String(error);
      return {
        result: "error",
        exitCode: 1,
        jsonReportPath,
        markdownReportPath,
        stderr: `${processResult.stderr}${message}`,
      };
    }
  }

  return {
    result: classifyResult(processResult.exitCode, report),
    exitCode: processResult.exitCode,
    jsonReportPath,
    markdownReportPath,
    report,
    stderr: processResult.stderr,
  };
}
