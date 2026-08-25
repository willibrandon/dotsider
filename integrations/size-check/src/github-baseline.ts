import * as fs from "node:fs/promises";
import * as path from "node:path";
import {
  baselineArtifactName,
  compareBaseline,
  createBaselineIdentity,
  detectTargetRid,
  normalizeCommit,
  unknownBaselineComparison,
} from "./baseline";
import {
  BaselineComparison,
  BaselineComparisonReason,
  BaselineDiscovery,
  BaselineSource,
  SizeCheckInputs,
} from "./types";

interface GitHubRun {
  id: number;
  run_number: number;
  head_branch: string | null;
  head_sha: string;
  html_url: string;
  event: string;
  conclusion: string | null;
}

interface GitHubArtifact {
  id: number;
  name: string;
  expired: boolean;
  workflow_run?: { id: number } | null;
}

interface GitHubCommit {
  sha?: string;
  parents?: Array<{ sha?: string }>;
}

interface PullRequestData {
  number: number;
  state: string;
  merged?: boolean;
  mergeable?: boolean | null;
  merge_commit_sha?: string | null;
  base: { ref: string };
  head: { sha: string };
}

interface PullRequestContext {
  number?: number;
  headCommit: string;
  mergeCommit?: string;
  mergeable?: boolean | null;
  apiBacked: boolean;
}

interface GitHubBaselineContext {
  branch?: string;
  publish: boolean;
  pullRequest?: PullRequestContext;
}

interface GitHubCandidate {
  run: GitHubRun;
  source: BaselineSource;
}

export type TargetResolution =
  | { status: "resolved"; targetCommit: string }
  | { status: "unknown"; reason: BaselineComparisonReason }
  | { status: "not-applicable" };

class GitHubHttpError extends Error {
  public constructor(
    public readonly status: number,
    message: string,
    public readonly rateLimited = false,
  ) {
    super(message);
  }
}

export async function discoverGithubBaseline(
  inputs: SizeCheckInputs,
  preparedRid: string,
  environment: NodeJS.ProcessEnv = process.env,
): Promise<BaselineDiscovery> {
  const targetRid = await detectTargetRid(inputs.target, preparedRid);
  if (inputs.baseline) {
    const identity = createBaselineIdentity(
      "github-actions", "explicit", "explicit", inputs.target, targetRid, inputs.baselineKey,
      environment.GITHUB_WORKSPACE, environment.RUNNER_TEMP,
    );
    const artifactName = baselineArtifactName(identity);
    return {
      identity,
      artifactName,
      source: { status: "explicit", path: inputs.baseline, artifactName },
      publish: false,
    };
  }

  const repository = required(environment.GITHUB_REPOSITORY, "GITHUB_REPOSITORY");
  const workflow = workflowFile(required(environment.GITHUB_WORKFLOW_REF, "GITHUB_WORKFLOW_REF"));
  const job = required(environment.GITHUB_JOB, "GITHUB_JOB");
  const identity = createBaselineIdentity(
    "github-actions",
    `${repository}/${workflow}`,
    job,
    inputs.target,
    targetRid,
    inputs.baselineKey,
    environment.GITHUB_WORKSPACE,
    environment.RUNNER_TEMP,
  );
  const artifactName = baselineArtifactName(identity);
  const apiUrl = (environment.GITHUB_API_URL || "https://api.github.com").replace(/\/$/u, "");
  const token = required(environment.GITHUB_TOKEN, "GITHUB_TOKEN");
  const context = await resolveContext(apiUrl, repository, token, environment);
  if (!context.branch) {
    return notFound(identity, artifactName, false);
  }

  const currentRunId = Number(environment.GITHUB_RUN_ID || "0");
  const fallback = await findNewestCandidate(
    apiUrl, repository, workflow, token, artifactName, context.branch, currentRunId,
  );
  if (!fallback) {
    return notFound(identity, artifactName, context.publish, context.branch);
  }

  let selected = fallback;
  let comparison: BaselineComparison | undefined;
  if (context.pullRequest) {
    const target = await resolveGithubTargetCommit(environment, context);
    if (target.status === "resolved") {
      try {
        const exact = await findExactCandidate(
          apiUrl,
          repository,
          workflow,
          token,
          artifactName,
          context.branch,
          target.targetCommit,
          currentRunId,
        );
        if (exact.candidate) selected = exact.candidate;
        comparison = exact.complete
          ? compareBaseline(selected.source.commit!, target.targetCommit)
          : unknownBaselineComparison("candidate-search-incomplete", target.targetCommit);
      } catch (error) {
        if (error instanceof GitHubHttpError
            && (error.status === 401 || (error.status === 403 && !error.rateLimited))) {
          throw error;
        }
        comparison = unknownBaselineComparison("candidate-search-incomplete", target.targetCommit);
      }
    } else if (target.status === "unknown") {
      comparison = unknownBaselineComparison(target.reason);
    }
  }

  return {
    identity,
    artifactName,
    source: selected.source,
    comparison,
    runId: String(selected.run.id),
    downloadDirectory: path.join(environment.RUNNER_TEMP || process.cwd(), "dotsider-baseline", artifactName),
    publish: context.publish,
  };
}

export async function resolveGithubTargetCommit(
  environment: NodeJS.ProcessEnv = process.env,
  resolvedContext?: GitHubBaselineContext,
): Promise<TargetResolution> {
  const repository = required(environment.GITHUB_REPOSITORY, "GITHUB_REPOSITORY");
  const apiUrl = (environment.GITHUB_API_URL || "https://api.github.com").replace(/\/$/u, "");
  const token = required(environment.GITHUB_TOKEN, "GITHUB_TOKEN");
  const context = resolvedContext ?? await resolveContext(apiUrl, repository, token, environment);
  return context.pullRequest
    ? await resolveExpectedTarget(apiUrl, repository, token, context.pullRequest)
    : { status: "not-applicable" };
}

async function findNewestCandidate(
  apiUrl: string,
  repository: string,
  workflow: string,
  token: string,
  artifactName: string,
  branch: string,
  currentRunId: number,
): Promise<GitHubCandidate | undefined> {
  const artifacts = await githubJson<{ artifacts?: GitHubArtifact[] }>(
    `${apiUrl}/repos/${repository}/actions/artifacts?name=${encodeURIComponent(artifactName)}&per_page=100`,
    token,
  );
  const artifactRunIds = new Set(
    (artifacts.artifacts ?? [])
      .filter(artifact => artifact.name === artifactName && !artifact.expired && artifact.workflow_run)
      .map(artifact => artifact.workflow_run!.id),
  );
  if (artifactRunIds.size === 0) return undefined;

  const url = workflowRunsUrl(apiUrl, repository, workflow, branch)
    + "&status=success&exclude_pull_requests=true&per_page=100";
  const runs = await githubJson<{ workflow_runs?: GitHubRun[] }>(url, token);
  for (const run of runs.workflow_runs ?? []) {
    if (!eligibleRun(run, branch, currentRunId) || !artifactRunIds.has(run.id)) continue;
    return candidateFromRun(run, artifactName, branch);
  }
  return undefined;
}

async function findExactCandidate(
  apiUrl: string,
  repository: string,
  workflow: string,
  token: string,
  artifactName: string,
  branch: string,
  targetCommit: string,
  currentRunId: number,
): Promise<{ candidate?: GitHubCandidate; complete: boolean }> {
  for (let page = 1; page <= 10; page++) {
    const url = workflowRunsUrl(apiUrl, repository, workflow, branch)
      + `&head_sha=${encodeURIComponent(targetCommit)}`
      + `&status=success&exclude_pull_requests=true&per_page=100&page=${page}`;
    const response = await githubJson<{ workflow_runs?: GitHubRun[] }>(url, token);
    const runs = response.workflow_runs ?? [];
    for (const run of runs) {
      if (!eligibleRun(run, branch, currentRunId)) continue;
      const commit = requireProviderCommit(run.head_sha, `GitHub Actions run ${run.id}`);
      if (commit !== targetCommit) continue;
      const artifacts = await githubJson<{ artifacts?: GitHubArtifact[] }>(
        `${apiUrl}/repos/${repository}/actions/runs/${run.id}/artifacts`
          + `?name=${encodeURIComponent(artifactName)}&per_page=100`,
        token,
      );
      if ((artifacts.artifacts ?? []).some(artifact => artifact.name === artifactName && !artifact.expired)) {
        return { candidate: candidateFromRun(run, artifactName, branch), complete: true };
      }
    }
    if (runs.length < 100) return { complete: true };
  }
  return { complete: false };
}

function candidateFromRun(run: GitHubRun, artifactName: string, branch: string): GitHubCandidate {
  const commit = requireProviderCommit(run.head_sha, `GitHub Actions run ${run.id}`);
  return {
    run,
    source: {
      status: "restored",
      provider: "github-actions",
      branch,
      commit,
      id: String(run.id),
      number: String(run.run_number),
      url: run.html_url,
      artifactName,
    },
  };
}

function eligibleRun(run: GitHubRun, branch: string, currentRunId: number): boolean {
  return run.id !== currentRunId
    && run.conclusion === "success"
    && run.head_branch === branch
    && run.event !== "pull_request"
    && run.event !== "pull_request_target";
}

async function resolveExpectedTarget(
  apiUrl: string,
  repository: string,
  token: string,
  initial: PullRequestContext,
): Promise<TargetResolution> {
  let pull = initial;
  if (pull.apiBacked && pull.mergeable === null && pull.number) {
    for (const delayMs of [1000, 2000]) {
      await delay(delayMs);
      try {
        const refreshed = await githubJson<PullRequestData>(
          `${apiUrl}/repos/${repository}/pulls/${pull.number}`,
          token,
        );
        if (!isOpenPullRequest(refreshed)) return { status: "not-applicable" };
        pull = pullRequestDetails(refreshed, true);
        if (pull.mergeable !== null) break;
      } catch (error) {
        return { status: "unknown", reason: optionalGithubReason(error) };
      }
    }
  }

  if (pull.apiBacked && pull.mergeable === null) {
    return { status: "unknown", reason: "merge-not-ready" };
  }
  if (pull.apiBacked && pull.mergeable === false) {
    return { status: "unknown", reason: "merge-conflict" };
  }

  if (!pull.mergeCommit) {
    return { status: "unknown", reason: missingMergeReason(pull.mergeable) };
  }
  const mergeCommit = normalizeCommit(pull.mergeCommit);
  const headCommit = normalizeCommit(pull.headCommit);
  if (!mergeCommit || !headCommit) return { status: "unknown", reason: "not-a-test-merge" };

  const commit = await readOptionalCommit(apiUrl, repository, token, mergeCommit);
  if ("reason" in commit) return { status: "unknown", reason: commit.reason };
  const returned = normalizeCommit(commit.value.sha);
  if (returned !== mergeCommit) return { status: "unknown", reason: "response-mismatch" };
  const parents = commit.value.parents?.map(parent => normalizeCommit(parent.sha));
  if (!parents || parents.length !== 2 || !parents[0] || !parents[1] || parents[1] !== headCommit) {
    return { status: "unknown", reason: "not-a-test-merge" };
  }
  return { status: "resolved", targetCommit: parents[0] };
}

async function readOptionalCommit(
  apiUrl: string,
  repository: string,
  token: string,
  commit: string,
): Promise<{ value: GitHubCommit } | { reason: BaselineComparisonReason }> {
  const url = `${apiUrl}/repos/${repository}/git/commits/${encodeURIComponent(commit)}`;
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      return { value: await githubJson<GitHubCommit>(url, token) };
    } catch (error) {
      const status = error instanceof GitHubHttpError ? error.status : 0;
      if (status === 401 || status === 403) return { reason: "permission-denied" };
      if (status === 404) return { reason: "merge-commit-unavailable" };
      if (attempt === 2 || (status !== 0 && status !== 429 && status < 500)) {
        return { reason: "provider-unavailable" };
      }
      await delay(attempt === 0 ? 1000 : 2000);
    }
  }
  return { reason: "provider-unavailable" };
}

async function resolveContext(
  apiUrl: string,
  repository: string,
  token: string,
  environment: NodeJS.ProcessEnv,
): Promise<GitHubBaselineContext> {
  const eventName = environment.GITHUB_EVENT_NAME || "";
  const event = await readEvent(environment.GITHUB_EVENT_PATH);
  if (eventName === "pull_request" || eventName === "pull_request_target") {
    const pull = eventPullRequest(event);
    const branch = pull.base.ref;
    if (!isOpenPullRequest(pull)) return { branch, publish: false };
    return {
      branch,
      publish: false,
      pullRequest: {
        headCommit: pull.head.sha,
        mergeCommit: eventName === "pull_request" ? environment.GITHUB_SHA : pull.merge_commit_sha ?? undefined,
        mergeable: pull.mergeable,
        apiBacked: false,
      },
    };
  }
  if (eventName === "issue_comment" || eventName === "pull_request_review_comment") {
    const number = eventName === "issue_comment"
      ? objectNumber(event, "issue", "number")
      : objectNumber(event, "pull_request", "number");
    const pull = await githubJson<PullRequestData>(`${apiUrl}/repos/${repository}/pulls/${number}`, token);
    return apiPullRequestContext(pull);
  }
  if (eventName === "workflow_dispatch") {
    const dispatchNumber = objectOptionalNumber(event, "inputs", "pr_number");
    if (dispatchNumber !== undefined) {
      const pull = await githubJson<PullRequestData>(
        `${apiUrl}/repos/${repository}/pulls/${dispatchNumber}`,
        token,
      );
      return apiPullRequestContext(pull);
    }
  }

  const ref = environment.GITHUB_REF || "";
  const branch = ref.startsWith("refs/heads/")
    ? ref.slice("refs/heads/".length)
    : environment.GITHUB_REF_TYPE === "branch" ? environment.GITHUB_REF_NAME : undefined;
  return { branch, publish: branch !== undefined };
}

function apiPullRequestContext(pull: PullRequestData): GitHubBaselineContext {
  requirePullRequest(pull);
  if (!isOpenPullRequest(pull)) return { branch: pull.base.ref, publish: false };
  return { branch: pull.base.ref, publish: false, pullRequest: pullRequestDetails(pull, true) };
}

function pullRequestDetails(pull: PullRequestData, apiBacked: boolean): PullRequestContext {
  requirePullRequest(pull);
  return {
    number: pull.number,
    headCommit: pull.head.sha,
    mergeCommit: pull.merge_commit_sha ?? undefined,
    mergeable: pull.mergeable,
    apiBacked,
  };
}

function eventPullRequest(event: Record<string, unknown>): PullRequestData {
  const value = asRecord(event.pull_request);
  if (!value) throw new Error("The GitHub event payload is incomplete.");
  const base = asRecord(value.base);
  const head = asRecord(value.head);
  const result = {
    number: objectOptionalNumber(value, "number") ?? 0,
    state: typeof value.state === "string" ? value.state : "",
    merged: value.merged === true,
    mergeable: typeof value.mergeable === "boolean" || value.mergeable === null ? value.mergeable : undefined,
    merge_commit_sha: typeof value.merge_commit_sha === "string" ? value.merge_commit_sha : null,
    base: { ref: typeof base?.ref === "string" ? base.ref : "" },
    head: { sha: typeof head?.sha === "string" ? head.sha : "" },
  };
  requirePullRequest(result);
  return result;
}

function requirePullRequest(pull: PullRequestData): void {
  if (!pull.base?.ref || !pull.head?.sha || !pull.state) {
    throw new Error("The GitHub pull request response is incomplete.");
  }
}

function isOpenPullRequest(pull: PullRequestData): boolean {
  return pull.state.toLowerCase() === "open" && pull.merged !== true;
}

function missingMergeReason(mergeable: boolean | null | undefined): BaselineComparisonReason {
  return mergeable === null ? "merge-not-ready"
    : mergeable === false ? "merge-conflict"
      : "merge-commit-unavailable";
}

function optionalGithubReason(error: unknown): BaselineComparisonReason {
  if (error instanceof GitHubHttpError) {
    if (error.status === 401 || (error.status === 403 && !error.rateLimited)) return "permission-denied";
    if (error.status === 404) return "merge-commit-unavailable";
  }
  return "provider-unavailable";
}

function workflowRunsUrl(
  apiUrl: string,
  repository: string,
  workflow: string,
  branch: string,
): string {
  return `${apiUrl}/repos/${repository}/actions/workflows/${encodeURIComponent(workflow)}/runs`
    + `?branch=${encodeURIComponent(branch)}`;
}

function notFound(
  identity: BaselineDiscovery["identity"],
  artifactName: string,
  publish: boolean,
  branch?: string,
): BaselineDiscovery {
  return {
    identity,
    artifactName,
    source: { status: "not-found", provider: "github-actions", branch, artifactName },
    publish,
  };
}

async function githubJson<T>(url: string, token: string): Promise<T> {
  const response = await fetchGithub(url, token);
  if (!response.ok) {
    const responseMessage = await githubErrorMessage(response);
    const rateLimited = response.status === 429
      || response.headers.get("x-ratelimit-remaining") === "0"
      || response.headers.has("retry-after")
      || /\bsecondary rate limit\b/iu.test(responseMessage);
    const permission = !rateLimited && (response.status === 401 || response.status === 403)
      ? " Grant the job 'actions: read' permission."
      : "";
    const throttled = rateLimited ? " GitHub API rate limit reached; retry the workflow later." : "";
    throw new GitHubHttpError(
      response.status,
      `GitHub baseline discovery failed with HTTP ${response.status}.${permission}${throttled}`,
      rateLimited,
    );
  }
  return await response.json() as T;
}

async function fetchGithub(url: string, token: string): Promise<Response> {
  let lastError: unknown;
  for (const delayMs of [0, 1000, 2000]) {
    if (delayMs > 0) await delay(delayMs);
    try {
      return await fetch(url, {
        headers: {
          Accept: "application/vnd.github+json",
          Authorization: `Bearer ${token}`,
          "X-GitHub-Api-Version": "2022-11-28",
          "User-Agent": "dotsider-size-check",
        },
      });
    } catch (error) {
      lastError = error;
    }
  }
  throw new GitHubHttpError(0, lastError instanceof Error ? lastError.message : String(lastError));
}

async function githubErrorMessage(response: Response): Promise<string> {
  try {
    const value = JSON.parse(await response.text()) as unknown;
    if (value && typeof value === "object" && "message" in value
        && typeof value.message === "string") {
      return value.message;
    }
  } catch {
    // A non-JSON error body carries no structured GitHub rate-limit signal.
  }
  return "";
}

async function readEvent(eventPath: string | undefined): Promise<Record<string, unknown>> {
  if (!eventPath) return {};
  return JSON.parse(await fs.readFile(eventPath, "utf8")) as Record<string, unknown>;
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === "object" ? value as Record<string, unknown> : undefined;
}

function objectNumber(value: unknown, ...keys: string[]): number {
  const result = objectOptionalNumber(value, ...keys);
  if (result === undefined) throw new Error("The GitHub event payload does not identify a pull request.");
  return result;
}

function objectOptionalNumber(value: unknown, ...keys: string[]): number | undefined {
  let current = value;
  for (const key of keys) {
    if (!current || typeof current !== "object") return undefined;
    current = (current as Record<string, unknown>)[key];
  }
  const parsed = typeof current === "number" ? current : typeof current === "string" ? Number(current) : NaN;
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function workflowFile(reference: string): string {
  const beforeRef = reference.split("@")[0] ?? reference;
  const marker = "/.github/workflows/";
  const index = beforeRef.indexOf(marker);
  if (index < 0) throw new Error(`Unable to determine the workflow file from GITHUB_WORKFLOW_REF '${reference}'.`);
  return beforeRef.slice(index + marker.length);
}

function requireProviderCommit(value: string | undefined, source: string): string {
  const commit = normalizeCommit(value);
  if (!commit) throw new Error(`${source} returned an invalid source commit ID.`);
  return commit;
}

function required(value: string | undefined, name: string): string {
  const candidate = value?.trim();
  if (!candidate) throw new Error(`Required environment variable ${name} was not provided.`);
  return candidate;
}

async function delay(milliseconds: number): Promise<void> {
  await new Promise<void>(resolve => setTimeout(resolve, milliseconds));
}
