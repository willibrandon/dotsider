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
exports.createBaselineIdentity = createBaselineIdentity;
exports.baselineArtifactName = baselineArtifactName;
exports.detectTargetRid = detectTargetRid;
exports.detectRidFromHeader = detectRidFromHeader;
exports.stageBaseline = stageBaseline;
exports.restoreBaseline = restoreBaseline;
exports.enrichReports = enrichReports;
exports.withManagedBaselineFreshness = withManagedBaselineFreshness;
exports.formatBaselineWarning = formatBaselineWarning;
const node_crypto_1 = require("node:crypto");
const node_fs_1 = require("node:fs");
const fs = __importStar(require("node:fs/promises"));
const path = __importStar(require("node:path"));
const manifestFileName = "dotsider-baseline.json";
const manifestSchemaVersion = 1;
const maximumFileBytes = 1024 * 1024 * 1024;
function createBaselineIdentity(provider, scope, job, targetPath, rid, baselineKey, workspace, temporaryDirectory) {
    return {
        provider,
        scope: requiredIdentityPart(scope, "provider scope"),
        job: requiredIdentityPart(job, "job"),
        target: baselineKey?.trim() || logicalTarget(targetPath, workspace, temporaryDirectory),
        rid: requiredIdentityPart(rid, "target RID"),
    };
}
function baselineArtifactName(identity) {
    const digest = (0, node_crypto_1.createHash)("sha256").update(canonicalIdentity(identity)).digest("hex").slice(0, 20);
    const rid = sanitize(identity.rid).slice(0, 32) || "unknown";
    return `dotsider-baseline-${rid}-${digest}`;
}
async function detectTargetRid(targetPath, fallbackRid) {
    if (targetPath.toLowerCase().endsWith(".mstat")) {
        return fallbackRid;
    }
    const handle = await fs.open(targetPath, "r");
    try {
        // The ELF program interpreter is not guaranteed to appear in the first page.
        // Reading 64 KiB is still bounded while covering normal program-header layouts.
        const buffer = Buffer.alloc(64 * 1024);
        const { bytesRead } = await handle.read(buffer, 0, buffer.length, 0);
        return detectRidFromHeader(buffer.subarray(0, bytesRead), fallbackRid);
    }
    finally {
        await handle.close();
    }
}
function detectRidFromHeader(bytes, fallbackRid) {
    if (bytes.length >= 64 && bytes[0] === 0x7f && bytes.toString("ascii", 1, 4) === "ELF") {
        const littleEndian = bytes[5] === 1;
        const machine = littleEndian ? bytes.readUInt16LE(18) : bytes.readUInt16BE(18);
        const architecture = machine === 0x3e ? "x64"
            : machine === 0xb7 ? "arm64"
                : machine === 0x03 ? "x86"
                    : machine === 0x28 ? "arm"
                        : undefined;
        if (architecture) {
            const musl = bytes.includes(Buffer.from("ld-musl-"));
            return `linux-${musl ? "musl-" : ""}${architecture}`;
        }
    }
    if (bytes.length >= 64 && bytes[0] === 0x4d && bytes[1] === 0x5a) {
        const peOffset = bytes.readUInt32LE(0x3c);
        if (peOffset + 6 <= bytes.length && bytes.toString("ascii", peOffset, peOffset + 4) === "PE\0\0") {
            const machine = bytes.readUInt16LE(peOffset + 4);
            const architecture = machine === 0x8664 ? "x64"
                : machine === 0xaa64 ? "arm64"
                    : machine === 0x014c ? "x86"
                        : machine === 0x01c4 ? "arm"
                            : undefined;
            if (architecture)
                return `win-${architecture}`;
        }
    }
    if (bytes.length >= 8) {
        const magic = bytes.readUInt32BE(0);
        const littleEndian = magic === 0xcefaedfe || magic === 0xcffaedfe;
        if (magic === 0xfeedface || magic === 0xfeedfacf || littleEndian) {
            const cpu = littleEndian ? bytes.readUInt32LE(4) : bytes.readUInt32BE(4);
            if (cpu === 0x01000007)
                return "osx-x64";
            if (cpu === 0x0100000c)
                return "osx-arm64";
        }
    }
    return fallbackRid;
}
async function stageBaseline(report, identity, source, directory) {
    await fs.rm(directory, { recursive: true, force: true });
    const filesDirectory = path.join(directory, "files");
    await fs.mkdir(filesDirectory, { recursive: true });
    const artifacts = report.targetArtifacts;
    const candidates = [
        ["binary", artifacts.binaryPath],
        ["mstat", artifacts.mstatPath],
        ["dgml", artifacts.dgmlPath],
    ];
    const manifestFiles = [];
    let targetRelativePath;
    for (const [role, sourcePath] of candidates) {
        if (!sourcePath)
            continue;
        const extension = role === "binary" ? path.extname(sourcePath) : role === "mstat" ? ".mstat" : ".dgml.xml";
        const relativePath = path.posix.join("files", `target${extension}`);
        const destination = path.join(directory, ...relativePath.split("/"));
        const stat = await fs.lstat(sourcePath);
        if (!stat.isFile() || stat.isSymbolicLink()) {
            throw new Error(`Baseline source '${sourcePath}' is not a regular file.`);
        }
        if (stat.size > maximumFileBytes) {
            throw new Error(`Baseline source '${sourcePath}' exceeds the 1 GiB file limit.`);
        }
        await fs.copyFile(sourcePath, destination);
        manifestFiles.push({ role, path: relativePath, bytes: stat.size, sha256: await sha256File(destination) });
        if (role === (artifacts.binaryPath ? "binary" : "mstat"))
            targetRelativePath = relativePath;
    }
    if (!targetRelativePath || !manifestFiles.some(file => file.role === "mstat")) {
        throw new Error("Dotsider did not report the files required to create a baseline artifact.");
    }
    const manifest = {
        schemaVersion: manifestSchemaVersion,
        identity,
        source,
        targetPath: targetRelativePath,
        files: manifestFiles,
    };
    await fs.writeFile(path.join(directory, manifestFileName), `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
    return directory;
}
async function restoreBaseline(directory, expected, expectedSource) {
    const manifestPath = path.join(directory, manifestFileName);
    const manifest = parseManifest(JSON.parse(await fs.readFile(manifestPath, "utf8")));
    if (canonicalIdentity(manifest.identity) !== canonicalIdentity(expected)) {
        throw new Error("The stored Dotsider baseline does not match this workflow, job, target, and RID.");
    }
    if (expectedSource?.status === "restored"
        && (manifest.source.status !== "restored"
            || manifest.source.provider !== expectedSource.provider
            || manifest.source.branch !== expectedSource.branch
            || manifest.source.id !== expectedSource.id
            || manifest.source.commit !== expectedSource.commit
            || manifest.source.artifactName !== expectedSource.artifactName)) {
        throw new Error("The stored Dotsider baseline provenance does not match its provider build.");
    }
    if (manifest.files.length === 0 || manifest.files.length > 3) {
        throw new Error("The stored Dotsider baseline manifest has an invalid file count.");
    }
    const paths = new Set();
    const roles = new Set();
    for (const file of manifest.files) {
        if (paths.has(file.path) || roles.has(file.role)) {
            throw new Error("The stored Dotsider baseline manifest contains duplicate files or roles.");
        }
        paths.add(file.path);
        roles.add(file.role);
        const resolved = safeArtifactPath(directory, file.path);
        const stat = await fs.lstat(resolved);
        if (!stat.isFile() || stat.isSymbolicLink() || stat.size !== file.bytes || stat.size > maximumFileBytes) {
            throw new Error(`Stored Dotsider baseline file '${file.path}' failed validation.`);
        }
        if (await sha256File(resolved) !== file.sha256) {
            throw new Error(`Stored Dotsider baseline file '${file.path}' failed SHA-256 validation.`);
        }
    }
    if (!roles.has("mstat")
        || !manifest.files.some(file => file.path === manifest.targetPath
            && (file.role === "binary" || file.role === "mstat"))) {
        throw new Error("The stored Dotsider baseline manifest does not identify a verified target and mstat file.");
    }
    return {
        targetPath: safeArtifactPath(directory, manifest.targetPath),
        source: manifest.source,
    };
}
async function enrichReports(jsonPath, markdownPath, source) {
    const report = JSON.parse(await fs.readFile(jsonPath, "utf8"));
    report.baselineSource = source;
    await fs.writeFile(jsonPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
    const markdown = await fs.readFile(markdownPath, "utf8");
    const baselineLine = formatBaselineSource(source);
    const warning = formatBaselineWarningMarkdown(source);
    const firstSection = /\r?\n---\r?\n/u.exec(markdown);
    const newline = markdown.includes("\r\n") ? "\r\n" : "\n";
    const provenance = warning
        ? `${baselineLine}${newline}${newline}> **Warning:** ${warning}`
        : baselineLine;
    const enriched = firstSection?.index !== undefined
        ? `${markdown.slice(0, firstSection.index).trimEnd()}${newline}${newline}${provenance}${newline}`
            + markdown.slice(firstSection.index)
        : `${markdown.trimEnd()}${newline}${newline}${provenance}${newline}`;
    await fs.writeFile(markdownPath, enriched, "utf8");
    return report;
}
function withManagedBaselineFreshness(source, pullRequest, targetCommit) {
    if (!pullRequest || source.status !== "restored")
        return source;
    const target = targetCommit?.trim();
    if (!target)
        return { ...source, freshness: "unknown" };
    const baseline = source.commit?.trim();
    return {
        ...source,
        targetCommit: target,
        freshness: baseline && baseline.toLowerCase() === target.toLowerCase() ? "current" : "stale",
    };
}
function formatBaselineWarning(source) {
    if (source.freshness === "stale") {
        return "The managed baseline does not match this pull request target. "
            + `The pull request targets commit '${shortCommit(source.targetCommit)}', but Dotsider restored commit `
            + `'${shortCommit(source.commit)}' from ${plainSourceLabel(source)}. `
            + `${refreshGuidance(source)} The available baseline and all configured budgets are still evaluated.`;
    }
    if (source.freshness === "unknown") {
        return "Dotsider could not verify whether the managed baseline matches this pull request target. "
            + `It restored commit '${shortCommit(source.commit)}' from ${plainSourceLabel(source)}, but could not determine `
            + "the target commit. Ensure the triggering repository is checked out so Dotsider can inspect the pull request "
            + "merge commit. The available baseline and all configured budgets are still evaluated.";
    }
    return undefined;
}
function formatBaselineSource(source) {
    if (source.status === "explicit") {
        return `**Baseline:** Explicit file ${markdownCodeSpan(source.path ?? "unknown")}.`;
    }
    if (source.status === "not-found") {
        const branch = source.branch ? ` for ${markdownCodeSpan(source.branch)}` : "";
        return `**Baseline:** No stored baseline was found${branch}; absolute budgets were evaluated and growth budgets were deferred.`;
    }
    const label = source.number ? `run ${source.number}` : `run ${source.id ?? "unknown"}`;
    const linked = source.url ? `[${label}](${source.url})` : label;
    const commit = source.commit ? ` at ${markdownCodeSpan(source.commit.slice(0, 12))}` : "";
    const branch = source.branch ? ` on ${markdownCodeSpan(source.branch)}` : "";
    return `**Baseline:** Restored from ${linked}${commit}${branch}.`;
}
function formatBaselineWarningMarkdown(source) {
    if (source.freshness === "stale") {
        return "The managed baseline does not match this pull request target. "
            + `The pull request targets commit ${markdownCodeSpan(shortCommit(source.targetCommit))}, but Dotsider restored `
            + `commit ${markdownCodeSpan(shortCommit(source.commit))} from ${markdownSourceLabel(source)}. `
            + `${refreshGuidanceMarkdown(source)} The available baseline and all configured budgets are still evaluated.`;
    }
    if (source.freshness === "unknown") {
        return "Dotsider could not verify whether the managed baseline matches this pull request target. "
            + `It restored commit ${markdownCodeSpan(shortCommit(source.commit))} from ${markdownSourceLabel(source)}, but `
            + "could not determine the target commit. Ensure the triggering repository is checked out so Dotsider can "
            + "inspect the pull request merge commit. The available baseline and all configured budgets are still evaluated.";
    }
    return undefined;
}
function plainSourceLabel(source) {
    const label = sourceLabel(source);
    return source.url ? `${label} (${source.url})` : label;
}
function markdownSourceLabel(source) {
    const label = sourceLabel(source);
    return source.url ? `[${label}](${source.url})` : label;
}
function sourceLabel(source) {
    const provider = source.provider === "azure-pipelines" ? "Azure Pipelines build" : "GitHub Actions run";
    return `${provider} ${source.number ?? source.id ?? "unknown"}`;
}
function refreshGuidance(source) {
    const branch = source.branch ? ` '${source.branch}'` : "";
    return `The target branch${branch} needs a successful Dotsider size-check run to publish a current baseline.`;
}
function refreshGuidanceMarkdown(source) {
    const branch = source.branch ? ` ${markdownCodeSpan(source.branch)}` : "";
    return `The target branch${branch} needs a successful Dotsider size-check run to publish a current baseline.`;
}
function shortCommit(value) {
    return value?.slice(0, 12) || "unknown";
}
function markdownCodeSpan(value) {
    const longestRun = Math.max(0, ...[...value.matchAll(/`+/gu)].map(match => match[0].length));
    const delimiter = "`".repeat(longestRun + 1);
    const padding = value.startsWith("`") || value.startsWith(" ")
        || value.endsWith("`") || value.endsWith(" ")
        ? " "
        : "";
    return `${delimiter}${padding}${value}${padding}${delimiter}`;
}
function parseManifest(value) {
    if (!value || typeof value !== "object")
        throw new Error("The stored Dotsider baseline manifest is invalid.");
    const manifest = value;
    if (manifest.schemaVersion !== manifestSchemaVersion || !manifest.identity || !manifest.source
        || typeof manifest.targetPath !== "string" || !Array.isArray(manifest.files)) {
        throw new Error("The stored Dotsider baseline manifest is unsupported or incomplete.");
    }
    for (const file of manifest.files) {
        if (!file || typeof file.path !== "string" || typeof file.sha256 !== "string"
            || typeof file.bytes !== "number" || !["binary", "mstat", "dgml"].includes(file.role)) {
            throw new Error("The stored Dotsider baseline manifest contains an invalid file entry.");
        }
    }
    return manifest;
}
function safeArtifactPath(root, relativePath) {
    if (!relativePath || path.isAbsolute(relativePath) || relativePath.includes("\\")) {
        throw new Error(`Stored Dotsider baseline path '${relativePath}' is unsafe.`);
    }
    const normalized = path.posix.normalize(relativePath);
    if (normalized === ".." || normalized.startsWith("../") || normalized.includes("/../")) {
        throw new Error(`Stored Dotsider baseline path '${relativePath}' is unsafe.`);
    }
    const resolvedRoot = path.resolve(root);
    const resolved = path.resolve(root, ...normalized.split("/"));
    if (resolved !== resolvedRoot && !resolved.startsWith(`${resolvedRoot}${path.sep}`)) {
        throw new Error(`Stored Dotsider baseline path '${relativePath}' escapes its artifact.`);
    }
    return resolved;
}
async function sha256File(filePath) {
    const hash = (0, node_crypto_1.createHash)("sha256");
    await new Promise((resolve, reject) => {
        const stream = (0, node_fs_1.createReadStream)(filePath);
        stream.on("data", chunk => hash.update(chunk));
        stream.on("error", reject);
        stream.on("end", resolve);
    });
    return hash.digest("hex");
}
function canonicalIdentity(identity) {
    return JSON.stringify({
        provider: identity.provider,
        scope: identity.scope,
        job: identity.job,
        target: identity.target,
        rid: identity.rid,
    });
}
function logicalTarget(targetPath, workspace, temporaryDirectory) {
    const absolute = path.resolve(targetPath);
    for (const [label, root] of [["workspace", workspace], ["temp", temporaryDirectory]]) {
        if (!root)
            continue;
        const relative = path.relative(path.resolve(root), absolute);
        if (relative && !relative.startsWith("..") && !path.isAbsolute(relative)) {
            return `${label}/${relative.replaceAll(path.sep, "/")}`;
        }
    }
    return `file/${path.basename(absolute)}`;
}
function requiredIdentityPart(value, name) {
    const candidate = value.trim();
    if (!candidate)
        throw new Error(`Unable to determine the Dotsider baseline ${name}.`);
    return candidate;
}
function sanitize(value) {
    return value.toLowerCase().replace(/[^a-z0-9_.-]+/gu, "-").replace(/^-+|-+$/gu, "");
}
