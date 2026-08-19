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
exports.discoverAzureBaseline = discoverAzureBaseline;
exports.takeAccessToken = takeAccessToken;
exports.resolveAzureExpectedTarget = resolveAzureExpectedTarget;
exports.resolveLocalMergeTargetCommit = resolveLocalMergeTargetCommit;
exports.parseGitCommitParents = parseGitCommitParents;
exports.extractZipArchive = extractZipArchive;
const node_child_process_1 = require("node:child_process");
const node_zlib_1 = require("node:zlib");
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
const baseline_1 = require("./baseline");
const maximumArchiveBytes = 1024 * 1024 * 1024;
class AzureHttpError extends Error {
    status;
    constructor(status, message) {
        super(message);
        this.status = status;
    }
}
async function discoverAzureBaseline(inputs, preparedRid, environment = process.env) {
    const targetRid = await (0, baseline_1.detectTargetRid)(inputs.target, preparedRid);
    if (inputs.baseline) {
        const identity = (0, baseline_1.createBaselineIdentity)("azure-pipelines", "explicit", "explicit", inputs.target, targetRid, inputs.baselineKey, environment.PIPELINE_WORKSPACE || environment.BUILD_SOURCESDIRECTORY, environment.AGENT_TEMPDIRECTORY);
        const artifactName = (0, baseline_1.baselineArtifactName)(identity);
        return {
            identity,
            artifactName,
            source: { status: "explicit", path: inputs.baseline, artifactName },
            publish: false,
        };
    }
    const project = required(environment.SYSTEM_TEAMPROJECTID || environment.SYSTEM_TEAMPROJECT, "project");
    const definition = required(environment.SYSTEM_DEFINITIONID, "pipeline definition");
    const job = required(environment.SYSTEM_JOBNAME, "job");
    const collection = required(environment.SYSTEM_TEAMFOUNDATIONCOLLECTIONURI, "collection URI").replace(/\/$/u, "");
    const identity = (0, baseline_1.createBaselineIdentity)("azure-pipelines", `${project}/${definition}`, job, inputs.target, targetRid, inputs.baselineKey, environment.PIPELINE_WORKSPACE || environment.BUILD_SOURCESDIRECTORY, environment.AGENT_TEMPDIRECTORY);
    const artifactName = (0, baseline_1.baselineArtifactName)(identity);
    const branch = targetBranch(environment);
    const pullRequest = environment.BUILD_REASON === "PullRequest";
    const publish = !pullRequest && branch !== undefined;
    if (!branch) {
        return {
            identity,
            artifactName,
            source: { status: "not-found", provider: "azure-pipelines", artifactName },
            publish: false,
        };
    }
    const token = takeAccessToken(environment);
    const apiRoot = `${collection}/${encodeURIComponent(project)}/_apis/build`;
    const buildsUrl = `${apiRoot}/builds?definitions=${encodeURIComponent(definition)}`
        + `&branchName=${encodeURIComponent(branch)}&statusFilter=completed&resultFilter=succeeded`
        + "&queryOrder=queueTimeDescending&$top=100&api-version=7.1";
    const firstPage = await azureBuildPage(buildsUrl, token);
    const currentBuild = Number(environment.BUILD_BUILDID || "0");
    const fallback = await findNewestAzureCandidate(firstPage.value, apiRoot, token, artifactName, branch, currentBuild);
    if (!fallback) {
        return {
            identity,
            artifactName,
            source: { status: "not-found", provider: "azure-pipelines", branch, artifactName },
            publish,
        };
    }
    let selected = fallback;
    let comparison;
    if (pullRequest) {
        const target = await resolveAzureExpectedTarget(collection, project, token, environment);
        if (target.status === "resolved") {
            try {
                const exact = await findExactAzureCandidate(firstPage, buildsUrl, apiRoot, token, artifactName, branch, currentBuild, target.targetCommit);
                if (exact.candidate)
                    selected = exact.candidate;
                comparison = exact.complete
                    ? (0, baseline_1.compareBaseline)(requireAzureBuildCommit(selected.build), target.targetCommit)
                    : (0, baseline_1.unknownBaselineComparison)("candidate-search-incomplete", target.targetCommit);
            }
            catch (error) {
                if (error instanceof AzureHttpError && (error.status === 401 || error.status === 403)) {
                    throw error;
                }
                comparison = (0, baseline_1.unknownBaselineComparison)("candidate-search-incomplete", target.targetCommit);
            }
        }
        else {
            comparison = (0, baseline_1.unknownBaselineComparison)(target.reason);
        }
    }
    const downloadDirectory = path.join(environment.AGENT_TEMPDIRECTORY || process.cwd(), "dotsider-baseline", artifactName);
    const baselineRoot = await downloadAndExtractZip(selected.artifactUrl, token, downloadDirectory, collection);
    const source = {
        status: "restored",
        provider: "azure-pipelines",
        branch,
        commit: requireAzureBuildCommit(selected.build),
        id: String(selected.build.id),
        number: selected.build.buildNumber,
        url: selected.build._links?.web?.href
            || `${collection}/${encodeURIComponent(project)}/_build/results?buildId=${selected.build.id}`,
        artifactName,
    };
    return { identity, artifactName, source, comparison, downloadDirectory: baselineRoot, publish };
}
function takeAccessToken(environment = process.env) {
    const name = "ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN";
    const token = environment[name]?.trim();
    delete environment[name];
    if (!token) {
        throw new Error("Azure baseline discovery could not access the job token supplied to agent tasks. "
            + "Run DotsiderSizeCheck in an agent job with access to the current project.");
    }
    return token;
}
async function findNewestAzureCandidate(builds, apiRoot, token, artifactName, branch, currentBuild) {
    for (const build of builds) {
        if (!eligibleAzureBuild(build, branch, currentBuild))
            continue;
        const artifactUrl = azureArtifactUrl(apiRoot, build.id, artifactName);
        const artifact = await tryAzureArtifact(artifactUrl, token);
        if (!artifact || artifact.name !== artifactName)
            continue;
        requireAzureBuildCommit(build);
        return { build, artifactUrl };
    }
    return undefined;
}
async function findExactAzureCandidate(firstPage, buildsUrl, apiRoot, token, artifactName, branch, currentBuild, targetCommit) {
    let page = firstPage;
    const seenTokens = new Set();
    while (true) {
        for (const build of page.value) {
            if (!eligibleAzureBuild(build, branch, currentBuild))
                continue;
            const commit = (0, baseline_1.normalizeCommit)(build.sourceVersion);
            if (commit !== targetCommit)
                continue;
            const artifactUrl = azureArtifactUrl(apiRoot, build.id, artifactName);
            const artifact = await tryAzureArtifact(artifactUrl, token);
            if (artifact?.name === artifactName) {
                requireAzureBuildCommit(build);
                return { candidate: { build, artifactUrl }, complete: true };
            }
        }
        const continuation = page.continuationToken?.trim();
        if (!continuation)
            return { complete: true };
        if (seenTokens.has(continuation))
            return { complete: false };
        seenTokens.add(continuation);
        page = await azureBuildPage(`${buildsUrl}&continuationToken=${encodeURIComponent(continuation)}`, token);
    }
}
function eligibleAzureBuild(build, branch, currentBuild) {
    return build.id !== currentBuild
        && build.result.toLowerCase() === "succeeded"
        && build.sourceBranch === branch;
}
function azureArtifactUrl(apiRoot, buildId, artifactName) {
    return `${apiRoot}/builds/${buildId}/artifacts?artifactName=${encodeURIComponent(artifactName)}`
        + "&api-version=7.1";
}
function requireAzureBuildCommit(build) {
    const commit = (0, baseline_1.normalizeCommit)(build.sourceVersion);
    if (!commit)
        throw new Error(`Azure Pipelines build ${build.id} returned an invalid source commit ID.`);
    return commit;
}
async function resolveAzureExpectedTarget(collection, project, token, environment) {
    const provider = environment.BUILD_REPOSITORY_PROVIDER?.trim().toLowerCase();
    if (!provider || !["tfsgit", "git", "github"].includes(provider)) {
        return { status: "unknown", reason: "unsupported-repository-provider" };
    }
    const mergeCommit = (0, baseline_1.normalizeCommit)(environment.BUILD_SOURCEVERSION);
    const sourceCommit = environment.SYSTEM_PULLREQUEST_SOURCECOMMITID?.trim()
        ? (0, baseline_1.normalizeCommit)(environment.SYSTEM_PULLREQUEST_SOURCECOMMITID)
        : undefined;
    if (!mergeCommit || (environment.SYSTEM_PULLREQUEST_SOURCECOMMITID?.trim() && !sourceCommit)) {
        return { status: "unknown", reason: "not-a-test-merge" };
    }
    const local = await resolveLocalMergeTargetCommit(environment.BUILD_REPOSITORY_LOCALPATH, mergeCommit, sourceCommit);
    if (local.status === "resolved")
        return { status: "resolved", targetCommit: local.targetCommit };
    const localReason = local.reason;
    if (provider !== "tfsgit"
        || !["repository-not-checked-out", "git-unavailable", "commit-not-found"].includes(localReason)) {
        return { status: "unknown", reason: localReason };
    }
    return await resolveAzureReposCommit(collection, project, token, environment, mergeCommit, sourceCommit);
}
async function resolveLocalMergeTargetCommit(repository, mergeCommit, sourceCommit, gitExecutable = "git") {
    const root = repository?.trim();
    if (!root || !await isDirectory(root)) {
        return { status: "unknown", reason: "repository-not-checked-out" };
    }
    const object = await readGitCommitObject(root, mergeCommit, gitExecutable);
    if ("reason" in object)
        return { status: "unknown", reason: object.reason };
    if (object.objectId !== mergeCommit)
        return { status: "unknown", reason: "response-mismatch" };
    if (object.objectType !== "commit")
        return { status: "unknown", reason: "not-a-test-merge" };
    return validateMergeParents(parseGitCommitParents(object.contents), sourceCommit);
}
function parseGitCommitParents(commit) {
    const parents = [];
    for (const line of commit.split(/\r?\n/u)) {
        if (line === "")
            break;
        if (!line.startsWith("parent "))
            continue;
        parents.push(line.slice("parent ".length).trim());
    }
    return parents;
}
async function readGitCommitObject(repository, commit, gitExecutable) {
    return await new Promise(resolve => {
        const child = (0, node_child_process_1.spawn)(gitExecutable, ["-C", repository, "cat-file", "--batch"], {
            env: { ...process.env, GIT_NO_REPLACE_OBJECTS: "1" },
            shell: false,
            windowsHide: true,
            stdio: ["pipe", "pipe", "pipe"],
        });
        const stdout = [];
        const stderr = [];
        let bytes = 0;
        let settled = false;
        const finish = (value) => {
            if (settled)
                return;
            settled = true;
            resolve(value);
        };
        child.on("error", error => {
            finish({ reason: error.code === "ENOENT" ? "git-unavailable" : "provider-unavailable" });
        });
        child.stdout.on("data", (chunk) => {
            bytes += chunk.length;
            if (bytes <= 1024 * 1024)
                stdout.push(chunk);
            else
                child.kill();
        });
        child.stderr.on("data", (chunk) => stderr.push(chunk));
        child.on("close", code => {
            if (settled)
                return;
            const error = Buffer.concat(stderr).toString("utf8");
            if (code !== 0 || bytes > 1024 * 1024) {
                finish({ reason: /not a git repository/iu.test(error)
                        ? "repository-not-checked-out"
                        : "commit-not-found" });
                return;
            }
            const buffer = Buffer.concat(stdout);
            const newline = buffer.indexOf(0x0a);
            if (newline < 0) {
                finish({ reason: "commit-not-found" });
                return;
            }
            const header = buffer.subarray(0, newline).toString("ascii");
            if (header.endsWith(" missing")) {
                finish({ reason: "commit-not-found" });
                return;
            }
            const match = /^([0-9a-f]+) ([a-z]+) ([0-9]+)$/iu.exec(header);
            const size = match ? Number(match[3]) : NaN;
            if (!match?.[1] || !match[2] || !Number.isSafeInteger(size) || size < 0
                || newline + 1 + size > buffer.length) {
                finish({ reason: "response-mismatch" });
                return;
            }
            finish({
                objectId: match[1].toLowerCase(),
                objectType: match[2],
                contents: buffer.subarray(newline + 1, newline + 1 + size).toString("utf8"),
            });
        });
        child.stdin.end(`${commit}\n`, "ascii");
    });
}
async function resolveAzureReposCommit(collection, project, token, environment, mergeCommit, sourceCommit) {
    const repository = environment.BUILD_REPOSITORY_ID?.trim();
    if (!repository)
        return { status: "unknown", reason: "provider-unavailable" };
    const url = `${collection}/${encodeURIComponent(project)}/_apis/git/repositories/`
        + `${encodeURIComponent(repository)}/commits/${encodeURIComponent(mergeCommit)}?api-version=7.1`;
    for (let attempt = 0; attempt < 3; attempt++) {
        try {
            const commit = await azureJson(url, token, "read source commit metadata");
            const returned = (0, baseline_1.normalizeCommit)(commit.commitId);
            if (returned !== mergeCommit)
                return { status: "unknown", reason: "response-mismatch" };
            return validateMergeParents(commit.parents ?? [], sourceCommit);
        }
        catch (error) {
            const status = error instanceof AzureHttpError ? error.status : 0;
            if (status === 401 || status === 403)
                return { status: "unknown", reason: "permission-denied" };
            if (status === 404)
                return { status: "unknown", reason: "commit-not-found" };
            if (attempt === 2 || (status !== 0 && status !== 429 && status < 500)) {
                return { status: "unknown", reason: "provider-unavailable" };
            }
            await delay(attempt === 0 ? 1000 : 2000);
        }
    }
    return { status: "unknown", reason: "provider-unavailable" };
}
function validateMergeParents(parents, sourceCommit) {
    const normalized = parents.map(parent => (0, baseline_1.normalizeCommit)(parent));
    if (normalized.length !== 2 || !normalized[0] || !normalized[1]
        || (sourceCommit && normalized[1] !== sourceCommit)) {
        return { status: "unknown", reason: "not-a-test-merge" };
    }
    return { status: "resolved", targetCommit: normalized[0] };
}
async function extractZipArchive(buffer, destination) {
    if (buffer.length > maximumArchiveBytes)
        throw new Error("The Dotsider baseline archive exceeds 1 GiB.");
    const eocd = findEndOfCentralDirectory(buffer);
    const entryCount = buffer.readUInt16LE(eocd + 10);
    const centralOffset = buffer.readUInt32LE(eocd + 16);
    if (entryCount === 0xffff || centralOffset === 0xffffffff || entryCount > 20) {
        throw new Error("The Dotsider baseline archive uses an unsupported ZIP layout.");
    }
    await fs.rm(destination, { recursive: true, force: true });
    await fs.mkdir(destination, { recursive: true });
    let offset = centralOffset;
    let extractedBytes = 0;
    for (let index = 0; index < entryCount; index++) {
        requireSignature(buffer, offset, 0x02014b50, "central directory");
        const flags = buffer.readUInt16LE(offset + 8);
        const compression = buffer.readUInt16LE(offset + 10);
        const crc = buffer.readUInt32LE(offset + 16);
        const compressedBytes = buffer.readUInt32LE(offset + 20);
        const uncompressedBytes = buffer.readUInt32LE(offset + 24);
        const nameLength = buffer.readUInt16LE(offset + 28);
        const extraLength = buffer.readUInt16LE(offset + 30);
        const commentLength = buffer.readUInt16LE(offset + 32);
        const externalAttributes = buffer.readUInt32LE(offset + 38);
        const localOffset = buffer.readUInt32LE(offset + 42);
        const name = buffer.toString("utf8", offset + 46, offset + 46 + nameLength);
        offset += 46 + nameLength + extraLength + commentLength;
        if ((flags & 1) !== 0 || ![0, 8].includes(compression)) {
            throw new Error(`The Dotsider baseline archive entry '${name}' is encrypted or uses unsupported compression.`);
        }
        const unixMode = externalAttributes >>> 16;
        if ((unixMode & 0xf000) === 0xa000)
            throw new Error(`The Dotsider baseline archive contains symlink '${name}'.`);
        if (name.endsWith("/"))
            continue;
        const destinationPath = safeZipPath(destination, name);
        requireSignature(buffer, localOffset, 0x04034b50, "local entry");
        const localNameLength = buffer.readUInt16LE(localOffset + 26);
        const localExtraLength = buffer.readUInt16LE(localOffset + 28);
        const dataOffset = localOffset + 30 + localNameLength + localExtraLength;
        if (dataOffset + compressedBytes > buffer.length)
            throw new Error(`ZIP entry '${name}' is truncated.`);
        const compressed = buffer.subarray(dataOffset, dataOffset + compressedBytes);
        if (uncompressedBytes > maximumArchiveBytes - extractedBytes) {
            throw new Error("The Dotsider baseline archive expands beyond 1 GiB.");
        }
        const contents = compression === 0
            ? Buffer.from(compressed)
            : (0, node_zlib_1.inflateRawSync)(compressed, { maxOutputLength: Math.max(1, uncompressedBytes) });
        if (contents.length !== uncompressedBytes || crc32(contents) !== crc) {
            throw new Error(`ZIP entry '${name}' failed size or CRC validation.`);
        }
        extractedBytes += contents.length;
        if (extractedBytes > maximumArchiveBytes)
            throw new Error("The Dotsider baseline archive expands beyond 1 GiB.");
        await fs.mkdir(path.dirname(destinationPath), { recursive: true });
        await fs.writeFile(destinationPath, contents, { flag: "wx" });
    }
}
async function downloadAndExtractZip(url, token, destination, trustedCollection) {
    const trustedOrigin = new URL(trustedCollection).origin;
    let current = new URL(url);
    let response;
    for (let redirect = 0; redirect <= 5; redirect++) {
        response = await fetch(current, {
            redirect: "manual",
            headers: {
                Accept: "application/zip",
                ...(current.origin === trustedOrigin ? { Authorization: `Bearer ${token}` } : {}),
            },
        });
        if (![301, 302, 303, 307, 308].includes(response.status))
            break;
        const location = response.headers.get("location");
        if (!location)
            throw new Error("Azure baseline artifact download returned an invalid redirect.");
        current = new URL(location, current);
    }
    if (!response)
        throw new Error("Azure baseline artifact download did not return a response.");
    if (!response.ok) {
        const permission = response.status === 401 || response.status === 403
            ? " Grant the project Build Service permission to read builds and artifacts."
            : "";
        throw new Error(`Azure baseline artifact download failed with HTTP ${response.status}.${permission}`);
    }
    await extractZipArchive(await readBoundedBody(response), destination);
    return await locateBaselineRoot(destination);
}
async function readBoundedBody(response) {
    const declaredLength = Number(response.headers.get("content-length"));
    if (Number.isFinite(declaredLength) && declaredLength > maximumArchiveBytes) {
        throw new Error("The Dotsider baseline archive exceeds 1 GiB.");
    }
    if (!response.body)
        throw new Error("Azure baseline artifact download returned no content.");
    const chunks = [];
    let bytes = 0;
    const reader = response.body.getReader();
    while (true) {
        const next = await reader.read();
        if (next.done)
            break;
        bytes += next.value.byteLength;
        if (bytes > maximumArchiveBytes) {
            await reader.cancel();
            throw new Error("The Dotsider baseline archive exceeds 1 GiB.");
        }
        chunks.push(Buffer.from(next.value));
    }
    return Buffer.concat(chunks, bytes);
}
async function locateBaselineRoot(directory) {
    if (await isFile(path.join(directory, "dotsider-baseline.json")))
        return directory;
    const candidates = [];
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            const candidate = path.join(directory, entry.name);
            if (await isFile(path.join(candidate, "dotsider-baseline.json")))
                candidates.push(candidate);
        }
    }
    if (candidates.length !== 1) {
        throw new Error("The Azure baseline artifact does not contain one baseline manifest at its root.");
    }
    return candidates[0];
}
async function isFile(filePath) {
    try {
        return (await fs.stat(filePath)).isFile();
    }
    catch {
        return false;
    }
}
async function isDirectory(directoryPath) {
    try {
        return (await fs.stat(directoryPath)).isDirectory();
    }
    catch {
        return false;
    }
}
async function azureJson(url, token, requiredPermission = "read builds and artifacts") {
    let response;
    try {
        response = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    }
    catch (error) {
        throw new AzureHttpError(0, error instanceof Error ? error.message : String(error));
    }
    if (!response.ok) {
        const permission = response.status === 401 || response.status === 403
            ? ` Grant the project Build Service permission to ${requiredPermission}.`
            : "";
        throw new AzureHttpError(response.status, `Azure baseline discovery failed with HTTP ${response.status}.${permission}`);
    }
    return await response.json();
}
async function azureBuildPage(url, token) {
    let response;
    try {
        response = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    }
    catch (error) {
        throw new AzureHttpError(0, error instanceof Error ? error.message : String(error));
    }
    if (!response.ok) {
        const permission = response.status === 401 || response.status === 403
            ? " Grant the project Build Service permission to read builds and artifacts."
            : "";
        throw new AzureHttpError(response.status, `Azure baseline discovery failed with HTTP ${response.status}.${permission}`);
    }
    const value = await response.json();
    return {
        value: value.value ?? [],
        continuationToken: response.headers.get("x-ms-continuationtoken") ?? undefined,
    };
}
async function tryAzureArtifact(url, token) {
    const response = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    if (response.status === 404)
        return undefined;
    if (!response.ok) {
        const permission = response.status === 401 || response.status === 403
            ? " Grant the project Build Service permission to read builds and artifacts."
            : "";
        throw new AzureHttpError(response.status, `Azure baseline discovery failed with HTTP ${response.status}.${permission}`);
    }
    return await response.json();
}
function targetBranch(environment) {
    const pullRequest = environment.BUILD_REASON === "PullRequest";
    const value = (pullRequest
        ? environment.SYSTEM_PULLREQUEST_TARGETBRANCH
        : environment.BUILD_SOURCEBRANCH)?.trim();
    if (!value)
        return undefined;
    if (value.startsWith("refs/heads/"))
        return value;
    return pullRequest && !value.startsWith("refs/") ? `refs/heads/${value}` : undefined;
}
function findEndOfCentralDirectory(buffer) {
    const lowerBound = Math.max(0, buffer.length - 65_557);
    for (let offset = buffer.length - 22; offset >= lowerBound; offset--) {
        if (buffer.readUInt32LE(offset) === 0x06054b50)
            return offset;
    }
    throw new Error("The Dotsider baseline artifact is not a supported ZIP archive.");
}
function requireSignature(buffer, offset, signature, name) {
    if (offset < 0 || offset + 4 > buffer.length || buffer.readUInt32LE(offset) !== signature) {
        throw new Error(`The Dotsider baseline ZIP ${name} is invalid.`);
    }
}
function safeZipPath(root, entry) {
    if (!entry || path.isAbsolute(entry) || entry.includes("\\"))
        throw new Error(`Unsafe ZIP path '${entry}'.`);
    const normalized = path.posix.normalize(entry);
    if (normalized === ".." || normalized.startsWith("../") || normalized.includes("/../")) {
        throw new Error(`Unsafe ZIP path '${entry}'.`);
    }
    const resolvedRoot = path.resolve(root);
    const resolved = path.resolve(root, ...normalized.split("/"));
    if (!resolved.startsWith(`${resolvedRoot}${path.sep}`))
        throw new Error(`Unsafe ZIP path '${entry}'.`);
    return resolved;
}
function crc32(buffer) {
    let crc = 0xffffffff;
    for (const byte of buffer) {
        crc ^= byte;
        for (let bit = 0; bit < 8; bit++)
            crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
    return (crc ^ 0xffffffff) >>> 0;
}
function required(value, name) {
    const candidate = value?.trim();
    if (!candidate)
        throw new Error(`Unable to determine the Azure Pipelines ${name}.`);
    return candidate;
}
async function delay(milliseconds) {
    await new Promise(resolve => setTimeout(resolve, milliseconds));
}
