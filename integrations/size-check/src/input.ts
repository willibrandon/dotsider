import * as path from "node:path";
import { SizeCheckInputs } from "./types";

export function parseBoolean(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined || value.trim() === "") {
    return fallback;
  }

  switch (value.trim().toLowerCase()) {
    case "true":
    case "1":
    case "yes":
      return true;
    case "false":
    case "0":
    case "no":
      return false;
    default:
      throw new Error(`Expected a boolean value but received '${value}'.`);
  }
}

export function parseTop(value: string | undefined): number {
  const candidate = (value ?? "10").trim();
  if (!/^\d+$/u.test(candidate)) {
    throw new Error(`top must be a non-negative integer; received '${value ?? ""}'.`);
  }

  const parsed = Number(candidate);
  if (!Number.isSafeInteger(parsed)) {
    throw new Error(`top must be a non-negative integer; received '${value ?? ""}'.`);
  }

  return parsed;
}

export function parseBudgets(value: string | undefined): string[] {
  return (value ?? "")
    .split(/\r?\n/u)
    .map(line => line.trim())
    .filter(line => line.length > 0);
}

export function createInputs(
  values: Readonly<Record<string, string | undefined>>,
  defaultReportRoot: string,
): SizeCheckInputs {
  const target = required(values.target, "target");
  const baseline = optionalPath(values.baseline);
  const requestedDirectory = values.reportDirectory?.trim();
  const reportDirectory = path.resolve(
    requestedDirectory && requestedDirectory.length > 0
      ? requestedDirectory
      : path.join(defaultReportRoot, "dotsider-size-check"),
  );

  return {
    target: path.resolve(target),
    baseline,
    baselineKey: optional(values.baselineKey),
    budgets: parseBudgets(values.budgets),
    budgetFile: optionalPath(values.budgetFile),
    top: parseTop(values.top),
    why: parseBoolean(values.why, false),
    dotsiderVersion: values.dotsiderVersion?.trim() || "latest",
    dotsiderPath: optionalPath(values.dotsiderPath),
    reportDirectory,
    publishSummary: parseBoolean(values.publishSummary, true),
    publishReports: parseBoolean(values.publishReports, true),
    artifactName: values.artifactName?.trim() || "dotsider-size-check",
  };
}

function required(value: string | undefined, name: string): string {
  if (!value || value.trim().length === 0) {
    throw new Error(`${name} is required.`);
  }

  return value.trim();
}

function optionalPath(value: string | undefined): string | undefined {
  const candidate = value?.trim();
  return candidate ? path.resolve(candidate) : undefined;
}

function optional(value: string | undefined): string | undefined {
  const candidate = value?.trim();
  return candidate || undefined;
}
