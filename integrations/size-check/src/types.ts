export interface SizeCheckInputs {
  target: string;
  baseline?: string;
  baselineKey?: string;
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
  deferredMetrics?: readonly string[];
}

export interface SizeBudgetReport {
  passed?: boolean;
  hasWarnings?: boolean;
  hasDeferred?: boolean;
  evaluations?: readonly SizeBudgetEvaluation[];
}

export interface SizeArtifactPaths {
  inputPath: string;
  mstatPath: string;
  binaryPath?: string | null;
  dgmlPath?: string | null;
}

export type BaselineStatus = "explicit" | "restored" | "not-found";
export type BaselineProvider = "github-actions" | "azure-pipelines";

export type BaselineComparisonReason =
  | "permission-denied"
  | "merge-not-ready"
  | "merge-conflict"
  | "merge-commit-unavailable"
  | "provider-unavailable"
  | "repository-not-checked-out"
  | "git-unavailable"
  | "commit-not-found"
  | "unsupported-repository-provider"
  | "not-a-test-merge"
  | "response-mismatch"
  | "candidate-search-incomplete";

export type BaselineComparison =
  | { status: "current" | "mismatched"; targetCommit: string }
  | { status: "unknown"; targetCommit?: string; reason: BaselineComparisonReason };

export interface BaselineSource {
  status: BaselineStatus;
  provider?: BaselineProvider;
  branch?: string;
  commit?: string;
  id?: string;
  number?: string;
  url?: string;
  artifactName?: string;
  path?: string;
}

export interface SizeReport {
  schemaVersion: number;
  target: string;
  baseline?: string | null;
  targetArtifacts: SizeArtifactPaths;
  baselineArtifacts?: SizeArtifactPaths | null;
  baselineSource?: BaselineSource;
  baselineComparison?: BaselineComparison;
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
  baselineStatus: BaselineStatus | "";
  baselineSourceId: string;
  baselineSourceCommit: string;
  baselineSourceUrl: string;
  baselineArtifactName: string;
  baselineTargetCommit: string;
  baselineComparisonStatus: BaselineComparison["status"] | "";
  baselineComparisonReason: BaselineComparisonReason | "";
}

export interface BaselineIdentity {
  provider: BaselineProvider;
  scope: string;
  job: string;
  target: string;
  rid: string;
}

export interface BaselineDiscovery {
  source: BaselineSource;
  comparison?: BaselineComparison;
  identity: BaselineIdentity;
  artifactName: string;
  runId?: string;
  downloadDirectory?: string;
  publish: boolean;
}
