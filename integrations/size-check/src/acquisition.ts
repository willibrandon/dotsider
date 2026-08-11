import { spawn } from "node:child_process";
import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { PreparedTool, ProcessResult } from "./types";

const Repository = "willibrandon/dotsider";
const MaximumArchiveBytes = 512 * 1024 * 1024;

export function resolveRid(
  platform: NodeJS.Platform = process.platform,
  architecture: string = process.arch,
  isMusl: boolean = platform === "linux" && process.env.DOTSIDER_MUSL === "1",
): string {
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

export function detectMuslRuntime(
  platform: NodeJS.Platform,
  override: string | undefined,
  report: unknown,
): boolean | undefined {
  if (platform !== "linux") {
    return false;
  }
  if (override !== undefined) {
    return override === "1";
  }
  if (!report || typeof report !== "object") {
    return undefined;
  }

  const header = (report as { header?: unknown }).header;
  if (!header || typeof header !== "object") {
    return undefined;
  }
  const glibcVersion = (header as { glibcVersionRuntime?: unknown }).glibcVersionRuntime;
  return typeof glibcVersion !== "string" || glibcVersion.length === 0;
}

export function archiveName(rid: string): string {
  return `dotsider-${rid}.${rid.startsWith("win-") ? "zip" : "tar.gz"}`;
}

export function parseChecksum(value: string): string {
  const match = /^\s*([a-fA-F0-9]{64})(?:\s+[*]?\S+)?\s*$/u.exec(value);
  if (!match?.[1]) {
    throw new Error("The release checksum sidecar is not a valid SHA-256 record.");
  }
  return match[1].toLowerCase();
}

export function validateArchiveEntries(entries: readonly string[]): void {
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

export async function prepareTool(
  requestedVersion: string,
  explicitPath: string | undefined,
  token: string | undefined = process.env.GITHUB_TOKEN,
): Promise<PreparedTool> {
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

export async function acquireTool(tool: PreparedTool, token?: string): Promise<string> {
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

    const entries = await listArchiveEntries(archivePath);
    validateArchiveEntries(entries);
    const extracted = path.join(staging, "content");
    await fs.mkdir(extracted);
    await extractArchive(archivePath, extracted);

    const executable = path.join(extracted, process.platform === "win32" ? "dotsider.exe" : "dotsider");
    await assertFileExists(executable, "The verified Dotsider archive");
    if (process.platform !== "win32") {
      await fs.chmod(executable, 0o755);
    }

    await fs.rm(tool.cacheDirectory, { recursive: true, force: true });
    await fs.rename(extracted, tool.cacheDirectory);
    return tool.executablePath;
  } finally {
    await fs.rm(staging, { recursive: true, force: true });
  }
}

export async function verifyChecksum(filePath: string, expected: string): Promise<void> {
  const actual = await hashFile(filePath);
  if (actual !== expected.toLowerCase()) {
    throw new Error(`Checksum verification failed: expected ${expected.toLowerCase()}, received ${actual}.`);
  }
}

export async function listArchiveEntries(archivePath: string): Promise<string[]> {
  const workingDirectory = path.dirname(archivePath);
  return (await run(archiveTool(), ["-tf", path.basename(archivePath)], workingDirectory)).stdout
    .split(/\r?\n/u)
    .filter(entry => entry.length > 0);
}

export async function extractArchive(archivePath: string, destinationPath: string): Promise<void> {
  const workingDirectory = path.dirname(archivePath);
  await run(
    archiveTool(),
    ["-xf", path.basename(archivePath), "-C", path.relative(workingDirectory, destinationPath)],
    workingDirectory,
  );
}

function archiveTool(): string {
  if (process.platform !== "win32") {
    return "tar";
  }

  const windowsDirectory = process.env.SystemRoot ?? process.env.WINDIR ?? "C:\\Windows";
  return path.join(windowsDirectory, "System32", "tar.exe");
}

async function resolveVersion(requested: string, token: string | undefined): Promise<string> {
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
  const payload = await response.json() as { tag_name?: unknown };
  if (typeof payload.tag_name !== "string" || !/^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/u.test(payload.tag_name)) {
    throw new Error("The latest Dotsider release did not provide an exact version tag.");
  }
  return payload.tag_name.slice(1);
}

async function download(url: string, destination: string, maximumBytes: number, token?: string): Promise<void> {
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
  } finally {
    await output.close();
  }
}

function requestHeaders(token: string | undefined): Record<string, string> {
  const headers: Record<string, string> = {
    Accept: "application/vnd.github+json",
    "User-Agent": "dotsider-size-check",
    "X-GitHub-Api-Version": "2022-11-28",
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

async function hashFile(filePath: string): Promise<string> {
  const hash = createHash("sha256");
  await new Promise<void>((resolve, reject) => {
    const input = createReadStream(filePath);
    input.on("data", chunk => hash.update(chunk));
    input.on("end", resolve);
    input.on("error", reject);
  });
  return hash.digest("hex");
}

async function run(
  fileName: string,
  args: readonly string[],
  workingDirectory?: string,
): Promise<ProcessResult> {
  return await new Promise<ProcessResult>((resolve, reject) => {
    const child = spawn(fileName, [...args], {
      cwd: workingDirectory,
      shell: false,
      windowsHide: true,
    });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (data: string) => stdout += data);
    child.stderr.on("data", (data: string) => stderr += data);
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

function toolCacheRoot(): string {
  return process.env.RUNNER_TOOL_CACHE
    || process.env.AGENT_TOOLSDIRECTORY
    || path.join(os.tmpdir(), "dotsider-tool-cache");
}

async function isMuslHost(): Promise<boolean> {
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

async function isFile(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(filePath)).isFile();
  } catch {
    return false;
  }
}

async function assertFileExists(filePath: string, subject: string): Promise<void> {
  if (!await isFile(filePath)) {
    throw new Error(`${subject} was not found at '${filePath}'.`);
  }
}
