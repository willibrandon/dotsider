"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.discoverGithubBaseline = discoverGithubBaseline;
exports.resolveGithubTargetCommit = resolveGithubTargetCommit;
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
const baseline_1 = require("./baseline");
class GitHubHttpError extends Error {
    status;
    rateLimited;
    constructor(status, message, rateLimited = false) {
        super(message);
        this.status = status;
        this.rateLimited = rateLimited;
    }
}
async function discoverGithubBaseline(inputs, preparedRid, environment = process.env) {
    const targetRid = await (0, baseline_1.detectTargetRid)(inputs.target, preparedRid);
    if (inputs.baseline) {
        const identity = (0, baseline_1.createBaselineIdentity)("github-actions", "explicit", "explicit", inputs.target, targetRid, inputs.baselineKey, environment.GITHUB_WORKSPACE, environment.RUNNER_TEMP);
        const artifactName = (0, baseline_1.baselineArtifactName)(identity);
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
    const identity = (0, baseline_1.createBaselineIdentity)("github-actions", `${repository}/${workflow}`, job, inputs.target, targetRid, inputs.baselineKey, environment.GITHUB_WORKSPACE, environment.RUNNER_TEMP);
    const artifactName = (0, baseline_1.baselineArtifactName)(identity);
    const apiUrl = (environment.GITHUB_API_URL || "https://api.github.com").replace(/\/$/u, "");
    const token = required(environment.GITHUB_TOKEN, "GITHUB_TOKEN");
    const context = await resolveContext(apiUrl, repository, token, environment);
    if (!context.branch) {
        return notFound(identity, artifactName, false);
    }
    const currentRunId = Number(environment.GITHUB_RUN_ID || "0");
    const fallback = await findNewestCandidate(apiUrl, repository, workflow, token, artifactName, context.branch, currentRunId);
    if (!fallback) {
        return notFound(identity, artifactName, context.publish, context.branch);
    }
    let selected = fallback;
    let comparison;
    if (context.pullRequest) {
        const target = await resolveGithubTargetCommit(environment, context);
        if (target.status === "resolved") {
            try {
                const exact = await findExactCandidate(apiUrl, repository, workflow, token, artifactName, context.branch, target.targetCommit, currentRunId);
                if (exact.candidate)
                    selected = exact.candidate;
                comparison = exact.complete
                    ? (0, baseline_1.compareBaseline)(selected.source.commit, target.targetCommit)
                    : (0, baseline_1.unknownBaselineComparison)("candidate-search-incomplete", target.targetCommit);
            }
            catch (error) {
                if (error instanceof GitHubHttpError
                    && (error.status === 401 || (error.status === 403 && !error.rateLimited))) {
                    throw error;
                }
                comparison = (0, baseline_1.unknownBaselineComparison)("candidate-search-incomplete", target.targetCommit);
            }
        }
        else if (target.status === "unknown") {
            comparison = (0, baseline_1.unknownBaselineComparison)(target.reason);
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
async function resolveGithubTargetCommit(environment = process.env, resolvedContext) {
    const repository = required(environment.GITHUB_REPOSITORY, "GITHUB_REPOSITORY");
    const apiUrl = (environment.GITHUB_API_URL || "https://api.github.com").replace(/\/$/u, "");
    const token = required(environment.GITHUB_TOKEN, "GITHUB_TOKEN");
    const context = resolvedContext ?? await resolveContext(apiUrl, repository, token, environment);
    return context.pullRequest
        ? await resolveExpectedTarget(apiUrl, repository, token, context.pullRequest)
        : { status: "not-applicable" };
}
async function findNewestCandidate(apiUrl, repository, workflow, token, artifactName, branch, currentRunId) {
    const artifacts = await githubJson(`${apiUrl}/repos/${repository}/actions/artifacts?name=${encodeURIComponent(artifactName)}&per_page=100`, token);
    const artifactRunIds = new Set((artifacts.artifacts ?? [])
        .filter(artifact => artifact.name === artifactName && !artifact.expired && artifact.workflow_run)
        .map(artifact => artifact.workflow_run.id));
    if (artifactRunIds.size === 0)
        return undefined;
    const url = workflowRunsUrl(apiUrl, repository, workflow, branch)
        + "&status=success&exclude_pull_requests=true&per_page=100";
    const runs = await githubJson(url, token);
    for (const run of runs.workflow_runs ?? []) {
        if (!eligibleRun(run, branch, currentRunId) || !artifactRunIds.has(run.id))
            continue;
        return candidateFromRun(run, artifactName, branch);
    }
    return undefined;
}
async function findExactCandidate(apiUrl, repository, workflow, token, artifactName, branch, targetCommit, currentRunId) {
    for (let page = 1; page <= 10; page++) {
        const url = workflowRunsUrl(apiUrl, repository, workflow, branch)
            + `&head_sha=${encodeURIComponent(targetCommit)}`
            + `&status=success&exclude_pull_requests=true&per_page=100&page=${page}`;
        const response = await githubJson(url, token);
        const runs = response.workflow_runs ?? [];
        for (const run of runs) {
            if (!eligibleRun(run, branch, currentRunId))
                continue;
            const commit = requireProviderCommit(run.head_sha, `GitHub Actions run ${run.id}`);
            if (commit !== targetCommit)
                continue;
            const artifacts = await githubJson(`${apiUrl}/repos/${repository}/actions/runs/${run.id}/artifacts`
                + `?name=${encodeURIComponent(artifactName)}&per_page=100`, token);
            if ((artifacts.artifacts ?? []).some(artifact => artifact.name === artifactName && !artifact.expired)) {
                return { candidate: candidateFromRun(run, artifactName, branch), complete: true };
            }
        }
        if (runs.length < 100)
            return { complete: true };
    }
    return { complete: false };
}
function candidateFromRun(run, artifactName, branch) {
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
function eligibleRun(run, branch, currentRunId) {
    return run.id !== currentRunId
        && run.conclusion === "success"
        && run.head_branch === branch
        && run.event !== "pull_request"
        && run.event !== "pull_request_target";
}
async function resolveExpectedTarget(apiUrl, repository, token, initial) {
    let pull = initial;
    if (pull.apiBacked && pull.mergeable === null && pull.number) {
        for (const delayMs of [1000, 2000]) {
            await delay(delayMs);
            try {
                const refreshed = await githubJson(`${apiUrl}/repos/${repository}/pulls/${pull.number}`, token);
                if (!isOpenPullRequest(refreshed))
                    return { status: "not-applicable" };
                pull = pullRequestDetails(refreshed, true);
                if (pull.mergeable !== null)
                    break;
            }
            catch (error) {
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
    const mergeCommit = (0, baseline_1.normalizeCommit)(pull.mergeCommit);
    const headCommit = (0, baseline_1.normalizeCommit)(pull.headCommit);
    if (!mergeCommit || !headCommit)
        return { status: "unknown", reason: "not-a-test-merge" };
    const commit = await readOptionalCommit(apiUrl, repository, token, mergeCommit);
    if ("reason" in commit)
        return { status: "unknown", reason: commit.reason };
    const returned = (0, baseline_1.normalizeCommit)(commit.value.sha);
    if (returned !== mergeCommit)
        return { status: "unknown", reason: "response-mismatch" };
    const parents = commit.value.parents?.map(parent => (0, baseline_1.normalizeCommit)(parent.sha));
    if (!parents || parents.length !== 2 || !parents[0] || !parents[1] || parents[1] !== headCommit) {
        return { status: "unknown", reason: "not-a-test-merge" };
    }
    return { status: "resolved", targetCommit: parents[0] };
}
async function readOptionalCommit(apiUrl, repository, token, commit) {
    const url = `${apiUrl}/repos/${repository}/git/commits/${encodeURIComponent(commit)}`;
    for (let attempt = 0; attempt < 3; attempt++) {
        try {
            return { value: await githubJson(url, token) };
        }
        catch (error) {
            const status = error instanceof GitHubHttpError ? error.status : 0;
            if (status === 401 || status === 403)
                return { reason: "permission-denied" };
            if (status === 404)
                return { reason: "merge-commit-unavailable" };
            if (attempt === 2 || (status !== 0 && status !== 429 && status < 500)) {
                return { reason: "provider-unavailable" };
            }
            await delay(attempt === 0 ? 1000 : 2000);
        }
    }
    return { reason: "provider-unavailable" };
}
async function resolveContext(apiUrl, repository, token, environment) {
    const eventName = environment.GITHUB_EVENT_NAME || "";
    const event = await readEvent(environment.GITHUB_EVENT_PATH);
    if (eventName === "pull_request" || eventName === "pull_request_target") {
        const pull = eventPullRequest(event);
        const branch = pull.base.ref;
        if (!isOpenPullRequest(pull))
            return { branch, publish: false };
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
        const pull = await githubJson(`${apiUrl}/repos/${repository}/pulls/${number}`, token);
        return apiPullRequestContext(pull);
    }
    if (eventName === "workflow_dispatch") {
        const dispatchNumber = objectOptionalNumber(event, "inputs", "pr_number");
        if (dispatchNumber !== undefined) {
            const pull = await githubJson(`${apiUrl}/repos/${repository}/pulls/${dispatchNumber}`, token);
            return apiPullRequestContext(pull);
        }
    }
    const ref = environment.GITHUB_REF || "";
    const branch = ref.startsWith("refs/heads/")
        ? ref.slice("refs/heads/".length)
        : environment.GITHUB_REF_TYPE === "branch" ? environment.GITHUB_REF_NAME : undefined;
    return { branch, publish: branch !== undefined };
}
function apiPullRequestContext(pull) {
    requirePullRequest(pull);
    if (!isOpenPullRequest(pull))
        return { branch: pull.base.ref, publish: false };
    return { branch: pull.base.ref, publish: false, pullRequest: pullRequestDetails(pull, true) };
}
function pullRequestDetails(pull, apiBacked) {
    requirePullRequest(pull);
    return {
        number: pull.number,
        headCommit: pull.head.sha,
        mergeCommit: pull.merge_commit_sha ?? undefined,
        mergeable: pull.mergeable,
        apiBacked,
    };
}
function eventPullRequest(event) {
    const value = asRecord(event.pull_request);
    if (!value)
        throw new Error("The GitHub event payload is incomplete.");
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
function requirePullRequest(pull) {
    if (!pull.base?.ref || !pull.head?.sha || !pull.state) {
        throw new Error("The GitHub pull request response is incomplete.");
    }
}
function isOpenPullRequest(pull) {
    return pull.state.toLowerCase() === "open" && pull.merged !== true;
}
function missingMergeReason(mergeable) {
    return mergeable === null ? "merge-not-ready"
        : mergeable === false ? "merge-conflict"
            : "merge-commit-unavailable";
}
function optionalGithubReason(error) {
    if (error instanceof GitHubHttpError) {
        if (error.status === 401 || (error.status === 403 && !error.rateLimited))
            return "permission-denied";
        if (error.status === 404)
            return "merge-commit-unavailable";
    }
    return "provider-unavailable";
}
function workflowRunsUrl(apiUrl, repository, workflow, branch) {
    return `${apiUrl}/repos/${repository}/actions/workflows/${encodeURIComponent(workflow)}/runs`
        + `?branch=${encodeURIComponent(branch)}`;
}
function notFound(identity, artifactName, publish, branch) {
    return {
        identity,
        artifactName,
        source: { status: "not-found", provider: "github-actions", branch, artifactName },
        publish,
    };
}
async function githubJson(url, token) {
    let response;
    try {
        response = await fetch(url, {
            headers: {
                Accept: "application/vnd.github+json",
                Authorization: `Bearer ${token}`,
                "X-GitHub-Api-Version": "2022-11-28",
                "User-Agent": "dotsider-size-check",
            },
        });
    }
    catch (error) {
        throw new GitHubHttpError(0, error instanceof Error ? error.message : String(error));
    }
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
        throw new GitHubHttpError(response.status, `GitHub baseline discovery failed with HTTP ${response.status}.${permission}${throttled}`, rateLimited);
    }
    return await response.json();
}
async function githubErrorMessage(response) {
    try {
        const value = JSON.parse(await response.text());
        if (value && typeof value === "object" && "message" in value
            && typeof value.message === "string") {
            return value.message;
        }
    }
    catch {
        // A non-JSON error body carries no structured GitHub rate-limit signal.
    }
    return "";
}
async function readEvent(eventPath) {
    if (!eventPath)
        return {};
    return JSON.parse(await fs.readFile(eventPath, "utf8"));
}
function asRecord(value) {
    return value && typeof value === "object" ? value : undefined;
}
function objectNumber(value, ...keys) {
    const result = objectOptionalNumber(value, ...keys);
    if (result === undefined)
        throw new Error("The GitHub event payload does not identify a pull request.");
    return result;
}
function objectOptionalNumber(value, ...keys) {
    let current = value;
    for (const key of keys) {
        if (!current || typeof current !== "object")
            return undefined;
        current = current[key];
    }
    const parsed = typeof current === "number" ? current : typeof current === "string" ? Number(current) : NaN;
    return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}
function workflowFile(reference) {
    const beforeRef = reference.split("@")[0] ?? reference;
    const marker = "/.github/workflows/";
    const index = beforeRef.indexOf(marker);
    if (index < 0)
        throw new Error(`Unable to determine the workflow file from GITHUB_WORKFLOW_REF '${reference}'.`);
    return beforeRef.slice(index + marker.length);
}
function requireProviderCommit(value, source) {
    const commit = (0, baseline_1.normalizeCommit)(value);
    if (!commit)
        throw new Error(`${source} returned an invalid source commit ID.`);
    return commit;
}
function required(value, name) {
    const candidate = value?.trim();
    if (!candidate)
        throw new Error(`Required environment variable ${name} was not provided.`);
    return candidate;
}
async function delay(milliseconds) {
    await new Promise(resolve => setTimeout(resolve, milliseconds));
}
