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
exports.resolveGithubMergeTargetCommit = resolveGithubMergeTargetCommit;
const node_child_process_1 = require("node:child_process");
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
const baseline_1 = require("./baseline");
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
        return {
            identity,
            artifactName,
            source: { status: "not-found", provider: "github-actions", artifactName },
            publish: false,
        };
    }
    const artifacts = await githubJson(`${apiUrl}/repos/${repository}/actions/artifacts?name=${encodeURIComponent(artifactName)}&per_page=100`, token);
    const artifactRunIds = new Set((artifacts.artifacts ?? [])
        .filter(artifact => artifact.name === artifactName && !artifact.expired && artifact.workflow_run)
        .map(artifact => artifact.workflow_run.id));
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
    const runs = await githubJson(runsUrl, token);
    const currentRunId = Number(environment.GITHUB_RUN_ID || "0");
    for (const run of runs.workflow_runs ?? []) {
        if (run.id === currentRunId || run.conclusion !== "success" || run.head_branch !== context.branch
            || run.event === "pull_request" || run.event === "pull_request_target"
            || !artifactRunIds.has(run.id)) {
            continue;
        }
        const targetCommit = context.pullRequest
            ? resolveGithubMergeTargetCommit(environment, context.mergeCommit, context.headCommit)
            : undefined;
        const source = (0, baseline_1.withManagedBaselineFreshness)({
            status: "restored",
            provider: "github-actions",
            branch: context.branch,
            commit: run.head_sha,
            id: String(run.id),
            number: String(run.run_number),
            url: run.html_url,
            artifactName,
        }, context.pullRequest, targetCommit);
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
async function resolveContext(apiUrl, repository, token, environment) {
    const eventName = environment.GITHUB_EVENT_NAME || "";
    const event = await readEvent(environment.GITHUB_EVENT_PATH);
    if (eventName === "pull_request" || eventName === "pull_request_target") {
        return { branch: objectString(event, "pull_request", "base", "ref"), publish: false, pullRequest: true,
            headCommit: objectString(event, "pull_request", "head", "sha"),
            mergeCommit: eventName === "pull_request" ? environment.GITHUB_SHA : undefined };
    }
    if (eventName === "issue_comment" || eventName === "pull_request_review_comment") {
        const number = eventName === "issue_comment"
            ? objectNumber(event, "issue", "number")
            : objectNumber(event, "pull_request", "number");
        const pull = await githubJson(`${apiUrl}/repos/${repository}/pulls/${number}`, token);
        return pullRequestContext(pull);
    }
    if (eventName === "workflow_dispatch") {
        const dispatchNumber = objectOptionalNumber(event, "inputs", "pr_number");
        if (dispatchNumber !== undefined) {
            const pull = await githubJson(`${apiUrl}/repos/${repository}/pulls/${dispatchNumber}`, token);
            return pullRequestContext(pull);
        }
    }
    const ref = environment.GITHUB_REF || "";
    const branch = ref.startsWith("refs/heads/")
        ? ref.slice("refs/heads/".length)
        : environment.GITHUB_REF_TYPE === "branch" ? environment.GITHUB_REF_NAME : undefined;
    return { branch, publish: branch !== undefined, pullRequest: false };
}
function pullRequestContext(pull) {
    if (!pull.base?.ref || !pull.head?.sha)
        throw new Error("The GitHub pull request response is incomplete.");
    return { branch: pull.base.ref, publish: false, pullRequest: true, headCommit: pull.head.sha,
        mergeCommit: pull.merge_commit_sha || undefined };
}
function resolveGithubMergeTargetCommit(environment, expectedMergeCommit, expectedHeadCommit) {
    const roots = [environment.GITHUB_WORKSPACE, process.cwd()].map(value => value?.trim())
        .filter((value) => !!value);
    const repositoryName = environment.GITHUB_REPOSITORY?.split("/").pop();
    const repositories = [...new Set([...roots, ...(repositoryName ? roots.map(root => path.join(root, repositoryName)) : [])])];
    for (const repository of repositories) {
        try {
            const testedCommit = gitOutput(repository, "rev-parse", "HEAD").trim();
            if (!/^[0-9a-f]{40,64}$/iu.test(testedCommit))
                continue;
            const parents = [...gitOutput(repository, "cat-file", "-p", testedCommit)
                    .matchAll(/^parent ([0-9a-f]{40,64})$/gimu)].map(match => match[1]);
            if (parents.length !== 2)
                continue;
            const matchesMerge = isSameCommit(testedCommit, expectedMergeCommit);
            const matchesHead = isSameCommit(parents[1], expectedHeadCommit);
            if (matchesMerge || matchesHead)
                return parents[0];
        }
        catch {
            // A merge checkout is not guaranteed for every GitHub trigger. Try the next likely repository path.
        }
    }
    return undefined;
}
async function githubJson(url, token) {
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
    return await response.json();
}
async function readEvent(eventPath) {
    if (!eventPath)
        return {};
    return JSON.parse(await fs.readFile(eventPath, "utf8"));
}
function objectString(value, ...keys) {
    let current = value;
    for (const key of keys) {
        if (!current || typeof current !== "object")
            throw new Error("The GitHub event payload is incomplete.");
        current = current[key];
    }
    if (typeof current !== "string" || !current)
        throw new Error("The GitHub event payload is incomplete.");
    return current;
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
const isSameCommit = (actual, expected) => !!expected && actual.toLowerCase() === expected.trim().toLowerCase();
function gitOutput(repository, ...arguments_) {
    return (0, node_child_process_1.execFileSync)("git", ["-C", repository, ...arguments_], { encoding: "utf8", maxBuffer: 1024 * 1024,
        windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
}
function required(value, name) {
    const candidate = value?.trim();
    if (!candidate)
        throw new Error(`Required environment variable ${name} was not provided.`);
    return candidate;
}
