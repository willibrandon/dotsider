export interface SizeCheckInputs {
  target: string;
  baseline?: string;
  budgets: readonly string[];
  budgetFile?: string;
  top: number;
  why: boolean;
  dotsiderVersion: string;
  dotsiderPath?: string;
  reportDirectory: string;
  publishSummary: boolean;
  publishReports: boolean;
  artifactName: string;
}

export interface PreparedTool {
  version: string;
  rid: string;
  cacheDirectory: string;
  executablePath: string;
  cacheKey: string;
  explicit: boolean;
}

export interface ProcessResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

export type SizeCheckResult = "passed" | "passed-with-warnings" | "budget-failed" | "error";

export interface SizeBudgetEvaluation {
  violations?: readonly unknown[];
}

export interface SizeBudgetReport {
  passed?: boolean;
  hasWarnings?: boolean;
  evaluations?: readonly SizeBudgetEvaluation[];
}

export interface SizeReport {
  schemaVersion: number;
  target: string;
  baseline?: string | null;
  totalBasis: string;
  leftTotal?: number | null;
  rightTotal: number;
  summary: {
    delta: number;
  };
  budgets?: SizeBudgetReport | null;
}

export interface SizeCheckExecution {
  result: SizeCheckResult;
  exitCode: number;
  jsonReportPath: string;
  markdownReportPath: string;
  report?: SizeReport;
  stderr: string;
}

export interface StableOutputs {
  result: SizeCheckResult;
  exitCode: string;
  jsonReportPath: string;
  markdownReportPath: string;
  artifactName: string;
  dotsiderVersion: string;
  totalBasis: string;
  baselineTotal: string;
  currentTotal: string;
  delta: string;
  violationCount: string;
}
