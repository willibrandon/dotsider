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
exports.resolveRid = resolveRid;
exports.detectMuslRuntime = detectMuslRuntime;
exports.archiveName = archiveName;
exports.parseChecksum = parseChecksum;
exports.validateArchiveEntries = validateArchiveEntries;
exports.prepareTool = prepareTool;
exports.acquireTool = acquireTool;
exports.verifyChecksum = verifyChecksum;
const node_child_process_1 = require("node:child_process");
const node_crypto_1 = require("node:crypto");
const node_fs_1 = require("node:fs");
const fs = __importStar(require("node:fs/promises"));
const os = __importStar(require("node:os"));
const path = __importStar(require("node:path"));
const Repository = "willibrandon/dotsider";
const MaximumArchiveBytes = 512 * 1024 * 1024;
function resolveRid(platform = process.platform, architecture = process.arch, isMusl = platform === "linux" && process.env.DOTSIDER_MUSL === "1") {
    const architectureName = architecture === "x64"
        ? "x64"
        : architecture === "arm64"
            ? "arm64"
            : undefined;
    if (!architectureName) {
        throw new Error(`Dotsider releases do not support architecture '${architecture}'.`);
    }
    switch (platform) {
        case "win32":
            return `win-${architectureName}`;
        case "darwin":
            return `osx-${architectureName}`;
        case "linux":
            return `${isMusl ? "linux-musl" : "linux"}-${architectureName}`;
        default:
            throw new Error(`Dotsider releases do not support platform '${platform}'.`);
    }
}
function detectMuslRuntime(platform, override, report) {
    if (platform !== "linux") {
        return false;
    }
    if (override !== undefined) {
        return override === "1";
    }
    if (!report || typeof report !== "object") {
        return undefined;
    }
    const header = report.header;
    if (!header || typeof header !== "object") {
        return undefined;
    }
    const glibcVersion = header.glibcVersionRuntime;
    return typeof glibcVersion !== "string" || glibcVersion.length === 0;
}
function archiveName(rid) {
    return `dotsider-${rid}.${rid.startsWith("win-") ? "zip" : "tar.gz"}`;
}
function parseChecksum(value) {
    const match = /^\s*([a-fA-F0-9]{64})(?:\s+[*]?\S+)?\s*$/u.exec(value);
    if (!match?.[1]) {
        throw new Error("The release checksum sidecar is not a valid SHA-256 record.");
    }
    return match[1].toLowerCase();
}
function validateArchiveEntries(entries) {
    for (const entry of entries) {
        const normalized = entry.replaceAll("\\", "/");
        if (normalized.length === 0 || normalized === "." || normalized === "./") {
            continue;
        }
        if (normalized.startsWith("/")
            || /^[a-zA-Z]:\//u.test(normalized)
            || normalized.split("/").some(segment => segment === "..")) {
            throw new Error(`The Dotsider archive contains an unsafe path: '${entry}'.`);
        }
    }
}
async function prepareTool(requestedVersion, explicitPath, token = process.env.GITHUB_TOKEN) {
    const rid = resolveRid(process.platform, process.arch, await isMuslHost());
    if (explicitPath) {
        const executablePath = path.resolve(explicitPath);
        await assertFileExists(executablePath, "The configured Dotsider executable");
        return {
            version: "custom",
            rid,
            cacheDirectory: path.dirname(executablePath),
            executablePath,
            cacheKey: "dotsider-custom",
            explicit: true,
        };
    }
    const version = await resolveVersion(requestedVersion, token);
    const root = toolCacheRoot();
    const cacheDirectory = path.join(root, "dotsider", version, rid);
    return {
        version,
        rid,
        cacheDirectory,
        executablePath: path.join(cacheDirectory, process.platform === "win32" ? "dotsider.exe" : "dotsider"),
        cacheKey: `dotsider-${version}-${rid}`,
        explicit: false,
    };
}
async function acquireTool(tool, token) {
    if (tool.explicit) {
        return tool.executablePath;
    }
    if (await isFile(tool.executablePath)) {
        return tool.executablePath;
    }
    const parent = path.dirname(tool.cacheDirectory);
    await fs.mkdir(parent, { recursive: true });
    const staging = await fs.mkdtemp(path.join(parent, ".download-"));
    try {
        const asset = archiveName(tool.rid);
        const archivePath = path.join(staging, asset);
        const checksumPath = `${archivePath}.sha256`;
        const baseUrl = `https://github.com/${Repository}/releases/download/v${tool.version}`;
        await download(`${baseUrl}/${asset}`, archivePath, MaximumArchiveBytes, token);
        await download(`${baseUrl}/${asset}.sha256`, checksumPath, 16 * 1024, token);
        const expected = parseChecksum(await fs.readFile(checksumPath, "utf8"));
        const actual = await hashFile(archivePath);
        if (actual !== expected) {
            throw new Error(`Checksum verification failed for ${asset}: expected ${expected}, received ${actual}.`);
        }
        const entries = (await run("tar", ["-tf", archivePath])).stdout
            .split(/\r?\n/u)
            .filter(entry => entry.length > 0);
        validateArchiveEntries(entries);
        const extracted = path.join(staging, "content");
        await fs.mkdir(extracted);
        await run("tar", ["-xf", archivePath, "-C", extracted]);
        const executable = path.join(extracted, process.platform === "win32" ? "dotsider.exe" : "dotsider");
        await assertFileExists(executable, "The verified Dotsider archive");
        if (process.platform !== "win32") {
            await fs.chmod(executable, 0o755);
        }
        await fs.rm(tool.cacheDirectory, { recursive: true, force: true });
        await fs.rename(extracted, tool.cacheDirectory);
        return tool.executablePath;
    }
    finally {
        await fs.rm(staging, { recursive: true, force: true });
    }
}
async function verifyChecksum(filePath, expected) {
    const actual = await hashFile(filePath);
    if (actual !== expected.toLowerCase()) {
        throw new Error(`Checksum verification failed: expected ${expected.toLowerCase()}, received ${actual}.`);
    }
}
async function resolveVersion(requested, token) {
    const normalized = requested.trim().replace(/^v/u, "");
    if (normalized !== "latest") {
        if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/u.test(normalized)) {
            throw new Error(`dotsider-version must be 'latest' or an exact version; received '${requested}'.`);
        }
        return normalized;
    }
    const response = await fetch(`https://api.github.com/repos/${Repository}/releases/latest`, {
        headers: requestHeaders(token),
        redirect: "follow",
    });
    if (!response.ok) {
        throw new Error(`Unable to resolve the latest Dotsider release: HTTP ${response.status}.`);
    }
    const payload = await response.json();
    if (typeof payload.tag_name !== "string" || !/^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/u.test(payload.tag_name)) {
        throw new Error("The latest Dotsider release did not provide an exact version tag.");
    }
    return payload.tag_name.slice(1);
}
async function download(url, destination, maximumBytes, token) {
    const response = await fetch(url, { headers: requestHeaders(token), redirect: "follow" });
    if (!response.ok || !response.body) {
        throw new Error(`Unable to download '${url}': HTTP ${response.status}.`);
    }
    const advertised = Number(response.headers.get("content-length") ?? "0");
    if (advertised > maximumBytes) {
        throw new Error(`Download '${url}' exceeds the ${maximumBytes}-byte limit.`);
    }
    const output = await fs.open(destination, "wx");
    const reader = response.body.getReader();
    let length = 0;
    try {
        while (true) {
            const { done, value } = await reader.read();
            if (done) {
                break;
            }
            length += value.byteLength;
            if (length > maximumBytes) {
                throw new Error(`Download '${url}' exceeds the ${maximumBytes}-byte limit.`);
            }
            await output.write(value);
        }
    }
    finally {
        await output.close();
    }
}
function requestHeaders(token) {
    const headers = {
        Accept: "application/vnd.github+json",
        "User-Agent": "dotsider-size-check",
        "X-GitHub-Api-Version": "2022-11-28",
    };
    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }
    return headers;
}
async function hashFile(filePath) {
    const hash = (0, node_crypto_1.createHash)("sha256");
    await new Promise((resolve, reject) => {
        const input = (0, node_fs_1.createReadStream)(filePath);
        input.on("data", chunk => hash.update(chunk));
        input.on("end", resolve);
        input.on("error", reject);
    });
    return hash.digest("hex");
}
async function run(fileName, args) {
    return await new Promise((resolve, reject) => {
        const child = (0, node_child_process_1.spawn)(fileName, [...args], { shell: false, windowsHide: true });
        let stdout = "";
        let stderr = "";
        child.stdout.setEncoding("utf8");
        child.stderr.setEncoding("utf8");
        child.stdout.on("data", (data) => stdout += data);
        child.stderr.on("data", (data) => stderr += data);
        child.on("error", reject);
        child.on("close", exitCode => {
            if (exitCode !== 0) {
                reject(new Error(`${fileName} ${args.join(" ")} failed with exit code ${exitCode}:\n${stderr}`));
                return;
            }
            resolve({ exitCode, stdout, stderr });
        });
    });
}
function toolCacheRoot() {
    return process.env.RUNNER_TOOL_CACHE
        || process.env.AGENT_TOOLSDIRECTORY
        || path.join(os.tmpdir(), "dotsider-tool-cache");
}
async function isMuslHost() {
    if (process.platform !== "linux") {
        return false;
    }
    if (process.env.DOTSIDER_MUSL !== undefined) {
        return process.env.DOTSIDER_MUSL === "1";
    }
    const detected = detectMuslRuntime(process.platform, undefined, process.report?.getReport());
    if (detected !== undefined) {
        return detected;
    }
    return await isFile("/etc/alpine-release");
}
async function isFile(filePath) {
    try {
        return (await fs.stat(filePath)).isFile();
    }
    catch {
        return false;
    }
}
async function assertFileExists(filePath, subject) {
    if (!await isFile(filePath)) {
        throw new Error(`${subject} was not found at '${filePath}'.`);
    }
}
