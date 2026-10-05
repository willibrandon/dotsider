import { spawnSync } from "node:child_process";
import path from "node:path";

const repositoryRoot = path.resolve(import.meta.dirname, "../../..");
const bracesAdvisory = "GHSA-vfj7-8cjw-p6xm";

export function evaluateAudit(result, directory) {
  if (result.error || result.signal || ![0, 1].includes(result.status)) {
    throw new Error(`pnpm audit failed: ${result.error?.message ?? result.signal ?? result.status}`);
  }

  const report = JSON.parse(result.stdout);
  if (!report || report.error || !report.advisories
    || typeof report.advisories !== "object" || Array.isArray(report.advisories)
    || !report.metadata?.vulnerabilities) {
    throw new Error("pnpm audit did not return a valid advisory report.");
  }

  const advisories = Object.values(report.advisories);
  if (advisories.length === 0 && (result.status !== 0
    || Object.values(report.metadata.vulnerabilities).some(count => count !== 0))) {
    throw new Error("pnpm audit reported vulnerabilities without advisory details.");
  }

  const deferred = [];
  const blocking = [];
  for (const advisory of advisories) {
    // Azure's packaging tool has no patched braces release for this advisory.
    // Recheck the registry's patched range on every run so a published fix blocks CI.
    // https://github.com/advisories/GHSA-vfj7-8cjw-p6xm
    const unpatchedPackagingDependency = directory === "azure-devops"
      && advisory?.github_advisory_id === bracesAdvisory
      && advisory.module_name === "braces"
      && advisory.patched_versions === "<0.0.0"
      && Array.isArray(advisory.findings) && advisory.findings.length > 0
      && advisory.findings.every(finding => Array.isArray(finding.paths)
        && finding.paths.length > 0
        && finding.paths.every(dependencyPath => typeof dependencyPath === "string"
          && dependencyPath.startsWith(".>tfx-cli>")));
    (unpatchedPackagingDependency ? deferred : blocking).push(advisory);
  }
  return { deferred, blocking };
}

if (import.meta.main) {
  for (const directory of ["integrations/size-check", "azure-devops"]) {
    console.log(`Auditing ${directory}`);
    const result = spawnSync("pnpm", ["audit", "--audit-level", "low", "--json"], {
      cwd: path.join(repositoryRoot, directory),
      encoding: "utf8",
      shell: process.platform === "win32",
      timeout: 120_000,
      maxBuffer: 10 * 1024 * 1024,
    });
    // Keep the complete findings visible, including the deferred advisory.
    process.stdout.write(result.stdout ?? "");
    process.stderr.write(result.stderr ?? "");
    try {
      const { deferred, blocking } = evaluateAudit(result, directory);
      if (deferred.length > 0) {
        console.warn(`::warning::${directory}: ${bracesAdvisory} in tfx-cli's braces dependency has no patched version. Deferring this finding until a fix is published. https://github.com/advisories/${bracesAdvisory}`);
      }
      if (blocking.length > 0) {
        console.error(`${directory}: ${blocking.length} blocking audit finding(s).`);
        process.exitCode = 1;
      }
    } catch (error) {
      console.error(`${directory}: ${error.message}`);
      process.exitCode = 1;
    }
  }
}
