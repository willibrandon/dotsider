import * as fs from "node:fs/promises";
import * as path from "node:path";
import { baselineArtifactName, createBaselineIdentity, detectTargetRid } from "./baseline";
import { BaselineDiscovery, BaselineSource, SizeCheckInputs } from "./types";

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

interface PullRequestData {
  number: number;
  base: { ref: string };
  head: { sha: string };
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
    return {
      identity,
      artifactName,
      source: { status: "not-found", provider: "github-actions", artifactName },
      publish: false,
    };
  }

  const artifacts = await githubJson<{ artifacts?: GitHubArtifact[] }>(
    `${apiUrl}/repos/${repository}/actions/artifacts?name=${encodeURIComponent(artifactName)}&per_page=100`,
    token,
  );
  const artifactRunIds = new Set(
    (artifacts.artifacts ?? [])
      .filter(artifact => artifact.name === artifactName && !artifact.expired && artifact.workflow_run)
      .map(artifact => artifact.workflow_run!.id),
  );
  if (artifactRunIds.size === 0) {
    return {
      identity,
      artifactName,
      source: {
        status: "not-found",
        provider: "github-actions",
        branch: context.branch,
        artifactName,
      },
      publish: context.publish,
    };
  }

  const runsUrl = `${apiUrl}/repos/${repository}/actions/workflows/${encodeURIComponent(workflow)}/runs`
    + `?branch=${encodeURIComponent(context.branch)}&status=success&exclude_pull_requests=true&per_page=100`;
  const runs = await githubJson<{ workflow_runs?: GitHubRun[] }>(runsUrl, token);
  const currentRunId = Number(environment.GITHUB_RUN_ID || "0");
  for (const run of runs.workflow_runs ?? []) {
    if (run.id === currentRunId || run.conclusion !== "success" || run.head_branch !== context.branch
        || run.event === "pull_request" || run.event === "pull_request_target"
        || !artifactRunIds.has(run.id)) {
      continue;
    }

    const source: BaselineSource = {
      status: "restored",
      provider: "github-actions",
      branch: context.branch,
      commit: run.head_sha,
      id: String(run.id),
      number: String(run.run_number),
      url: run.html_url,
      artifactName,
    };
    return {
      identity,
      artifactName,
      source,
      runId: String(run.id),
      downloadDirectory: path.join(environment.RUNNER_TEMP || process.cwd(), "dotsider-baseline", artifactName),
      publish: context.publish,
    };
  }

  return {
    identity,
    artifactName,
    source: {
      status: "not-found",
      provider: "github-actions",
      branch: context.branch,
      artifactName,
    },
    publish: context.publish,
  };
}

async function resolveContext(
  apiUrl: string,
  repository: string,
  token: string,
  environment: NodeJS.ProcessEnv,
): Promise<{ branch?: string; publish: boolean }> {
  const eventName = environment.GITHUB_EVENT_NAME || "";
  const event = await readEvent(environment.GITHUB_EVENT_PATH);
  if (eventName === "pull_request" || eventName === "pull_request_target") {
    return { branch: objectString(event, "pull_request", "base", "ref"), publish: false };
  }
  if (eventName === "issue_comment" || eventName === "pull_request_review_comment") {
    const number = eventName === "issue_comment"
      ? objectNumber(event, "issue", "number")
      : objectNumber(event, "pull_request", "number");
    const pull = await githubJson<PullRequestData>(`${apiUrl}/repos/${repository}/pulls/${number}`, token);
    return { branch: pull.base.ref, publish: false };
  }
  if (eventName === "workflow_dispatch") {
    const dispatchNumber = objectOptionalNumber(event, "inputs", "pr_number");
    if (dispatchNumber !== undefined) {
      const pull = await githubJson<PullRequestData>(`${apiUrl}/repos/${repository}/pulls/${dispatchNumber}`, token);
      return { branch: pull.base.ref, publish: false };
    }
  }

  const ref = environment.GITHUB_REF || "";
  const branch = ref.startsWith("refs/heads/")
    ? ref.slice("refs/heads/".length)
    : environment.GITHUB_REF_TYPE === "branch" ? environment.GITHUB_REF_NAME : undefined;
  return { branch, publish: branch !== undefined };
}

async function githubJson<T>(url: string, token: string): Promise<T> {
  const response = await fetch(url, {
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${token}`,
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "dotsider-size-check",
    },
  });
  if (!response.ok) {
    const permission = response.status === 401 || response.status === 403
      ? " Grant the job 'actions: read' permission."
      : "";
    throw new Error(`GitHub baseline discovery failed with HTTP ${response.status}.${permission}`);
  }
  return await response.json() as T;
}

async function readEvent(eventPath: string | undefined): Promise<Record<string, unknown>> {
  if (!eventPath) return {};
  return JSON.parse(await fs.readFile(eventPath, "utf8")) as Record<string, unknown>;
}

function objectString(value: unknown, ...keys: string[]): string {
  let current = value;
  for (const key of keys) {
    if (!current || typeof current !== "object") throw new Error("The GitHub event payload is incomplete.");
    current = (current as Record<string, unknown>)[key];
  }
  if (typeof current !== "string" || !current) throw new Error("The GitHub event payload is incomplete.");
  return current;
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

function required(value: string | undefined, name: string): string {
  const candidate = value?.trim();
  if (!candidate) throw new Error(`Required environment variable ${name} was not provided.`);
  return candidate;
}
