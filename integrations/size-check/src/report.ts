import * as fs from "node:fs/promises";
import * as path from "node:path";
import { BaselineSource, SizeCheckExecution, SizeReport, StableOutputs } from "./types";

type SizeCheckSummary = Pick<
  StableOutputs,
  "totalBasis" | "baselineTotal" | "currentTotal" | "delta" | "violationCount"
>;

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
  return report.schemaVersion === 2
    && typeof report.target === "string"
    && !!report.targetArtifacts
    && typeof report.targetArtifacts.mstatPath === "string"
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
  if (report.budgets?.hasWarnings === true || report.budgets?.hasDeferred === true) {
    return "passed-with-warnings";
  }
  return "passed";
}

export function createStableOutputs(
  execution: SizeCheckExecution,
  artifactName: string,
  dotsiderVersion: string,
  baselineSource?: BaselineSource,
): StableOutputs {
  const report = execution.report;
  const evaluations = report?.budgets?.evaluations ?? [];
  const violationCount = evaluations.reduce(
    (count, evaluation) => count + (evaluation.violations?.length ?? 0),
    0,
  );

  return {
    result: resultWithBaselineWarning(execution.result, baselineSource),
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
    baselineStatus: baselineSource?.status ?? "",
    baselineSourceId: baselineSource?.id ?? "",
    baselineSourceCommit: baselineSource?.commit ?? "",
    baselineSourceUrl: baselineSource?.url ?? "",
    baselineArtifactName: baselineSource?.artifactName ?? "",
    baselineTargetCommit: baselineSource?.targetCommit ?? "",
    baselineFreshness: baselineSource?.freshness ?? "",
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
    baselineStatus: "",
    baselineSourceId: "",
    baselineSourceCommit: "",
    baselineSourceUrl: "",
    baselineArtifactName: "",
    baselineTargetCommit: "",
    baselineFreshness: "",
  };
}

function resultWithBaselineWarning(
  result: SizeCheckExecution["result"],
  source: BaselineSource | undefined,
): SizeCheckExecution["result"] {
  return result === "passed" && (source?.freshness === "stale" || source?.freshness === "unknown")
    ? "passed-with-warnings"
    : result;
}

export function formatSizeCheckSummary(outputs: SizeCheckSummary): string {
  const parts: string[] = [];
  if (outputs.currentTotal !== "" && outputs.baselineTotal === "") {
    parts.push("current build (no baseline comparison)");
  } else if (outputs.baselineTotal !== "") {
    parts.push("compared with baseline");
  }
  const currentTotal = parseOutputNumber(outputs.currentTotal);
  if (currentTotal !== undefined) {
    parts.push(`${formatBytes(currentTotal)} total${outputs.totalBasis ? ` (${outputs.totalBasis})` : ""}`);
  }

  const delta = parseOutputNumber(outputs.delta);
  if (outputs.baselineTotal !== "" && delta !== undefined) {
    parts.push(`${delta >= 0 ? "+" : "-"}${formatBytes(Math.abs(delta))} from baseline`);
  }

  const violationCount = parseOutputNumber(outputs.violationCount);
  if (violationCount !== undefined && violationCount > 0) {
    parts.push(`${violationCount} budget violation${violationCount === 1 ? "" : "s"}`);
  }

  return parts.join("; ");
}

function numberOutput(value: number | null | undefined): string {
  return value === null || value === undefined ? "" : String(value);
}

function parseOutputNumber(value: string): number | undefined {
  if (value.trim() === "") {
    return undefined;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function formatBytes(value: number): string {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let amount = value;
  let unit = 0;
  while (amount >= 1024 && unit < units.length - 1) {
    amount /= 1024;
    unit++;
  }
  return unit === 0 ? `${Math.round(amount)} B` : `${amount.toFixed(1)} ${units[unit]}`;
}
