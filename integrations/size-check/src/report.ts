import * as fs from "node:fs/promises";
import * as path from "node:path";
import { SizeCheckExecution, SizeReport, StableOutputs } from "./types";

export function buildSizeCheckArguments(
  target: string,
  baseline: string | undefined,
  budgets: readonly string[],
  budgetFile: string | undefined,
  top: number,
  why: boolean,
  jsonReportPath: string,
  markdownReportPath: string,
): string[] {
  const args = [
    "size-check",
    target,
    "--format",
    "json",
    "--output",
    jsonReportPath,
    "--summary-file",
    markdownReportPath,
    "--top",
    String(top),
  ];

  if (baseline) {
    args.push("--baseline", baseline);
  }
  for (const budget of budgets) {
    args.push("--budget", budget);
  }
  if (budgetFile) {
    args.push("--budget-file", budgetFile);
  }
  if (why) {
    args.push("--why");
  }

  return args;
}

export async function readSizeReport(reportPath: string): Promise<SizeReport> {
  const parsed: unknown = JSON.parse(await fs.readFile(reportPath, "utf8"));
  if (!isSizeReport(parsed)) {
    throw new Error(`Dotsider wrote an unsupported JSON report to '${reportPath}'.`);
  }

  return parsed;
}

export function isSizeReport(value: unknown): value is SizeReport {
  if (!value || typeof value !== "object") {
    return false;
  }

  const report = value as Partial<SizeReport>;
  return report.schemaVersion === 1
    && typeof report.target === "string"
    && typeof report.totalBasis === "string"
    && typeof report.rightTotal === "number"
    && !!report.summary
    && typeof report.summary.delta === "number";
}

export function classifyResult(exitCode: number, report: SizeReport | undefined): SizeCheckExecution["result"] {
  if (exitCode === 2) {
    return "budget-failed";
  }
  if (exitCode !== 0 || report === undefined) {
    return "error";
  }
  if (report.budgets?.hasWarnings === true) {
    return "passed-with-warnings";
  }
  return "passed";
}

export function createStableOutputs(
  execution: SizeCheckExecution,
  artifactName: string,
  dotsiderVersion: string,
): StableOutputs {
  const report = execution.report;
  const evaluations = report?.budgets?.evaluations ?? [];
  const violationCount = evaluations.reduce(
    (count, evaluation) => count + (evaluation.violations?.length ?? 0),
    0,
  );

  return {
    result: execution.result,
    exitCode: String(execution.exitCode),
    jsonReportPath: path.resolve(execution.jsonReportPath),
    markdownReportPath: path.resolve(execution.markdownReportPath),
    artifactName,
    dotsiderVersion,
    totalBasis: report?.totalBasis ?? "",
    baselineTotal: numberOutput(report?.leftTotal),
    currentTotal: numberOutput(report?.rightTotal),
    delta: numberOutput(report?.summary.delta),
    violationCount: String(violationCount),
  };
}

export function createErrorOutputs(
  artifactName: string,
  dotsiderVersion: string,
): StableOutputs {
  return {
    result: "error",
    exitCode: "1",
    jsonReportPath: "",
    markdownReportPath: "",
    artifactName,
    dotsiderVersion,
    totalBasis: "",
    baselineTotal: "",
    currentTotal: "",
    delta: "",
    violationCount: "0",
  };
}

function numberOutput(value: number | null | undefined): string {
  return value === null || value === undefined ? "" : String(value);
}
