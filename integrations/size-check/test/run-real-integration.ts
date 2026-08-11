import { spawn } from "node:child_process";
import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import * as fs from "node:fs/promises";
import * as path from "node:path";

void main();

async function main(): Promise<void> {
  try {
    const root = await findRepositoryRoot();
    const rid = await hostRid();
    const outputRoot = path.join(root, "artifacts", "size-check-local", rid);
    const dotsiderDirectory = path.join(outputRoot, "dotsider");
    const baselineDirectory = path.join(outputRoot, "baseline");
    const targetDirectory = path.join(outputRoot, "target");
    await fs.rm(outputRoot, { recursive: true, force: true });
    await fs.mkdir(outputRoot, { recursive: true });

    await run("dotnet", [
      "publish",
      "src/Dotsider/Dotsider.csproj",
      "--configuration", "Release",
      "--runtime", rid,
      "-p:Version=0.0.0-local",
      "--output", dotsiderDirectory,
    ], root);
    await run("dotnet", [
      "publish",
      "samples/NativeAotConsole/NativeAotConsole.csproj",
      "--configuration", "Release",
      "--runtime", rid,
      "--output", baselineDirectory,
    ], root);
    await run("dotnet", [
      "publish",
      "samples/NativeAotConsoleV2/NativeAotConsoleV2.csproj",
      "--configuration", "Release",
      "--runtime", rid,
      "--output", targetDirectory,
    ], root);

    const suffix = process.platform === "win32" ? ".exe" : "";
    const executable = path.join(dotsiderDirectory, `dotsider${suffix}`);
    const baseline = path.join(baselineDirectory, `NativeAotConsole${suffix}`);
    const target = path.join(targetDirectory, `NativeAotConsole${suffix}`);
    const targetMstat = path.join(targetDirectory, "NativeAotConsole.mstat");
    const targetDgml = path.join(targetDirectory, "NativeAotConsole.codegen.dgml.xml");
    for (const required of [executable, baseline, target, targetMstat, targetDgml]) {
      if (!await isFile(required)) {
        throw new Error(`The real integration asset was not produced: ${required}`);
      }
    }

    const archive = path.join(
      outputRoot,
      process.platform === "win32" ? "dotsider.zip" : "dotsider.tar.gz",
    );
    await run("tar", process.platform === "win32"
      ? ["-a", "-cf", archive, "-C", dotsiderDirectory, "."]
      : ["-czf", archive, "-C", dotsiderDirectory, "."], root);
    const checksum = `${archive}.sha256`;
    await fs.writeFile(
      checksum,
      `${await hashFile(archive)}  ${path.basename(archive)}\n`,
      "ascii",
    );

    const compiledTests = path.join(root, "artifacts", "size-check-tests", "test");
    await run(process.execPath, [
      "--test",
      path.join(compiledTests, "real.integration.test.js"),
      path.join(compiledTests, "azure.integration.test.js"),
    ], root, {
      ...process.env,
      DOTSIDER_INTEGRATION_EXE: executable,
      DOTSIDER_INTEGRATION_BASELINE: baseline,
      DOTSIDER_INTEGRATION_TARGET: target,
      DOTSIDER_INTEGRATION_TARGET_MSTAT: targetMstat,
      DOTSIDER_INTEGRATION_TARGET_DGML: targetDgml,
      DOTSIDER_INTEGRATION_ARCHIVE: archive,
      DOTSIDER_INTEGRATION_CHECKSUM: checksum,
    });
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}

async function hostRid(): Promise<string> {
  const architecture = process.arch === "x64" || process.arch === "arm64"
    ? process.arch
    : undefined;
  if (!architecture) {
    throw new Error(`Local integration tests do not support architecture '${process.arch}'.`);
  }

  switch (process.platform) {
    case "win32":
      return `win-${architecture}`;
    case "darwin":
      return `osx-${architecture}`;
    case "linux": {
      const musl = process.env.DOTSIDER_MUSL === "1" || await isFile("/etc/alpine-release");
      return `${musl ? "linux-musl" : "linux"}-${architecture}`;
    }
    default:
      throw new Error(`Local integration tests do not support platform '${process.platform}'.`);
  }
}

async function findRepositoryRoot(): Promise<string> {
  let directory = path.resolve(process.cwd());
  while (true) {
    if (await isFile(path.join(directory, "Dotsider.slnx"))) {
      return directory;
    }
    const parent = path.dirname(directory);
    if (parent === directory) {
      throw new Error("Could not find the Dotsider repository root.");
    }
    directory = parent;
  }
}

async function run(
  fileName: string,
  args: readonly string[],
  workingDirectory: string,
  environment: NodeJS.ProcessEnv = process.env,
): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const child = spawn(fileName, [...args], {
      cwd: workingDirectory,
      env: environment,
      shell: false,
      stdio: "inherit",
      windowsHide: true,
    });
    child.on("error", reject);
    child.on("close", (exitCode, signal) => {
      if (exitCode !== 0) {
        reject(new Error(`${fileName} failed with exit code ${exitCode ?? `signal ${signal ?? "unknown"}`}.`));
        return;
      }
      resolve();
    });
  });
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

async function isFile(filePath: string): Promise<boolean> {
  try {
    return (await fs.stat(filePath)).isFile();
  } catch {
    return false;
  }
}
