import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createServer } from "node:http";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";
import { test } from "node:test";
import {
  discoverAzureBaseline,
  extractZipArchive,
  parseGitCommitParents,
  resolveAzureExpectedTarget,
  resolveLocalMergeTargetCommit,
} from "../src/azure-baseline";
import {
  baselineArtifactName,
  compareBaseline,
  createBaselineIdentity,
  detectRidFromHeader,
  detectTargetRid,
  enrichReports,
  formatBaselineWarning,
  normalizeCommit,
  restoreBaseline,
  stageBaseline,
} from "../src/baseline";
import { discoverGithubBaseline } from "../src/github-baseline";
import { SizeCheckInputs, SizeReport } from "../src/types";

test("baseline identity is stable and isolated by job, logical target, and RID", () => {
  const first = createBaselineIdentity(
    "github-actions", "owner/repo/ci.yml", "size-linux", "C:\\temp\\app.exe", "win-x64",
    "picket", "C:\\src", "C:\\temp",
  );
  const same = { ...first };
  const differentJob = { ...first, job: "size-windows" };

  assert.equal(baselineArtifactName(first), baselineArtifactName(same));
  assert.notEqual(baselineArtifactName(first), baselineArtifactName(differentJob));
  assert.match(baselineArtifactName(first), /^dotsider-baseline-win-x64-[a-f0-9]{20}$/u);
});

test("target RID detection reads PE, ELF, and Mach-O architecture headers", () => {
  const pe = Buffer.alloc(256);
  pe.write("MZ");
  pe.writeUInt32LE(128, 0x3c);
  pe.write("PE\0\0", 128);
  pe.writeUInt16LE(0xaa64, 132);
  assert.equal(detectRidFromHeader(pe, "linux-x64"), "win-arm64");

  const elf = Buffer.alloc(64);
  elf.set([0x7f, 0x45, 0x4c, 0x46, 2, 1]);
  elf.writeUInt16LE(0x3e, 18);
  assert.equal(detectRidFromHeader(elf, "win-x64"), "linux-x64");

  const macho = Buffer.alloc(16);
  macho.writeUInt32BE(0xfeedfacf, 0);
  macho.writeUInt32BE(0x0100000c, 4);
  assert.equal(detectRidFromHeader(macho, "linux-x64"), "osx-arm64");
});

test("target RID detection finds a musl interpreter beyond the first ELF page", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-musl-target-"));
  try {
    const target = path.join(directory, "app");
    const elf = Buffer.alloc(16 * 1024);
    elf.set([0x7f, 0x45, 0x4c, 0x46, 2, 1]);
    elf.writeUInt16LE(0x3e, 18);
    elf.write("/lib/ld-musl-x86_64.so.1", 8 * 1024, "ascii");
    await fs.writeFile(target, elf);

    assert.equal(await detectTargetRid(target, "linux-x64"), "linux-musl-x64");
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("staged baselines verify identity, lengths, and SHA-256 before restoration", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-baseline-manifest-"));
  try {
    const binary = path.join(directory, "app");
    const mstat = path.join(directory, "app.mstat");
    const dgml = path.join(directory, "app.dgml.xml");
    await Promise.all([
      fs.writeFile(binary, "native binary"),
      fs.writeFile(mstat, "real mstat bytes"),
      fs.writeFile(dgml, "<DirectedGraph />"),
    ]);
    const identity = createBaselineIdentity(
      "github-actions", "owner/repo/ci.yml", "size", binary, "linux-x64", "app", directory, directory,
    );
    const staged = path.join(directory, "artifact");
    await stageBaseline(report(binary, mstat, dgml), identity, {
      status: "restored", provider: "github-actions", id: "12", commit: "abc", artifactName: "baseline",
    }, staged);

    const restored = await restoreBaseline(staged, identity);
    assert.equal(await fs.readFile(restored.targetPath, "utf8"), "native binary");
    assert.equal(restored.source.id, "12");

    await assert.rejects(
      () => restoreBaseline(staged, { ...identity, job: "different-job" }),
      /does not match/u,
    );

    const manifestPath = path.join(staged, "dotsider-baseline.json");
    const manifestText = await fs.readFile(manifestPath, "utf8");
    const manifest = JSON.parse(manifestText) as { schemaVersion: number; targetPath: string; baselineComparison?: unknown };
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.baselineComparison, undefined);
    manifest.targetPath = "files/unverified";
    await fs.writeFile(path.join(staged, "files", "unverified"), "unverified bytes");
    await fs.writeFile(manifestPath, JSON.stringify(manifest));
    await assert.rejects(() => restoreBaseline(staged, identity), /verified target and mstat/u);
    await fs.writeFile(manifestPath, manifestText);

    await fs.writeFile(restored.targetPath, "forged binary");
    await assert.rejects(() => restoreBaseline(staged, identity), /SHA-256 validation/u);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("legacy v1 managed artifacts restore without invocation alignment fields", async () => {
  const fixture = path.resolve(__dirname, "../../../integrations/size-check/test/fixtures/legacy-v1");
  const restored = await restoreBaseline(fixture, {
    provider: "github-actions",
    scope: "owner/repo/ci.yml",
    job: "size",
    target: "app",
    rid: "linux-x64",
  });

  assert.equal(await fs.readFile(restored.targetPath, "utf8"), "legacy managed baseline\n");
  assert.equal(restored.source.id, "10");
  const manifest = JSON.parse(await fs.readFile(path.join(fixture, "dotsider-baseline.json"), "utf8")) as {
    schemaVersion?: number;
    baselineComparison?: unknown;
  };
  assert.equal(manifest.schemaVersion, 1);
  assert.equal(manifest.baselineComparison, undefined);
});

test("managed baseline commits require normalized full SHA-1 or SHA-256 IDs", () => {
  const sha1 = "a".repeat(40);
  const sha256 = "B".repeat(64);
  assert.equal(normalizeCommit(`  ${sha1.toUpperCase()}  `), sha1);
  assert.equal(normalizeCommit(sha256), sha256.toLowerCase());
  for (const invalid of [
    "a".repeat(39),
    "a".repeat(41),
    "b".repeat(63),
    "b".repeat(65),
    `${"a".repeat(39)}g`,
    "",
  ]) {
    assert.equal(normalizeCommit(invalid), undefined, `Expected '${invalid}' to be rejected`);
  }
  assert.throws(() => compareBaseline("invalid", sha1), /valid full commit IDs/u);
  assert.throws(() => compareBaseline(sha1, "invalid"), /valid full commit IDs/u);
});

test("report enrichment records provenance and explains a genuine first run", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-baseline-report-"));
  try {
    const json = path.join(directory, "report.json");
    const markdown = path.join(directory, "report.md");
    await fs.writeFile(json, JSON.stringify(report("app", "app.mstat")), "utf8");
    await fs.writeFile(markdown, "## Size check\n\n---\n\n### Overview\n", "utf8");

    const enriched = await enrichReports(json, markdown, {
      status: "not-found", provider: "github-actions", branch: "main", artifactName: "baseline",
    });

    assert.equal(enriched.baselineSource?.status, "not-found");
    assert.match(await fs.readFile(markdown, "utf8"), /No stored baseline was found for `main`.*growth budgets were deferred/u);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("report enrichment places provenance before the first CRLF report section", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-baseline-crlf-report-"));
  try {
    const json = path.join(directory, "report.json");
    const markdown = path.join(directory, "report.md");
    await fs.writeFile(json, JSON.stringify(report("app", "app.mstat")), "utf8");
    await fs.writeFile(markdown, "## Size check\r\n\r\n---\r\n\r\n### Overview\r\n", "utf8");

    await enrichReports(json, markdown, {
      status: "restored",
      provider: "github-actions",
      id: "15",
      number: "7",
      branch: "main",
      commit: "abcdef1234567890abcdef1234567890abcdef12",
      artifactName: "baseline",
    });

    const enriched = await fs.readFile(markdown, "utf8");
    assert.match(enriched, /## Size check\r\n\r\n\*\*Baseline:\*\* Restored from run 7 at `abcdef123456` on `main`\.\r\n\r\n---\r\n/u);
    assert.ok(enriched.indexOf("**Baseline:**") < enriched.indexOf("### Overview"));
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("report enrichment keeps alignment separate from durable baseline provenance", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-alignment-report-"));
  try {
    const json = path.join(directory, "report.json");
    const markdown = path.join(directory, "report.md");
    const source = {
      status: "restored" as const,
      provider: "github-actions" as const,
      branch: "main`branch",
      commit: "1111111111111111111111111111111111111111",
      id: "41",
      number: "9",
      url: "https://dev.azure.com/owner/My%20Project/run/(41)?path=%2fbuild&value=100%",
      artifactName: "baseline",
    };
    const comparison = {
      status: "mismatched" as const,
      targetCommit: "2222222222222222222222222222222222222222",
    };
    await fs.writeFile(json, JSON.stringify(report("app", "app.mstat")), "utf8");
    await fs.writeFile(markdown, "## Size check\n\n---\n\n### Overview\n", "utf8");

    const enriched = await enrichReports(json, markdown, source, comparison);
    const summary = await fs.readFile(markdown, "utf8");
    const warning = formatBaselineWarning(source, comparison);

    assert.deepEqual(enriched.baselineSource, source);
    assert.deepEqual(enriched.baselineComparison, comparison);
    assert.match(summary, /> \*\*Warning:\*\* The baseline was built from a different commit/u);
    assert.match(summary, /`222222222222`.*`111111111111`/u);
    assert.match(summary,
      /https:\/\/dev\.azure\.com\/owner\/My%20Project\/run\/%2841%29\?path=%2fbuild&value=100%25/u);
    assert.doesNotMatch(summary, /My%2520Project|%252fbuild/u);
    assert.match(summary, /``main`branch``/u);
    assert.match(warning ?? "", /The size comparison and budgets will still run/u);
    assert.doesNotMatch(`${summary}\n${warning}`, /\b(?:stale|older|predates)\b/iu);
    assert.equal(normalizeCommit("ABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD"),
      "abcdefabcdefabcdefabcdefabcdefabcdefabcd");
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("unknown alignment warnings give provider-specific guidance for every stable reason", () => {
  const source = {
    status: "restored" as const,
    provider: "azure-pipelines" as const,
    branch: "refs/heads/main",
    commit: "1111111111111111111111111111111111111111",
    id: "41",
    number: "9",
    url: "https://example.test/build/41",
    artifactName: "baseline",
  };
  const reasons = [
    "permission-denied",
    "merge-not-ready",
    "merge-conflict",
    "merge-commit-unavailable",
    "provider-unavailable",
    "repository-not-checked-out",
    "git-unavailable",
    "commit-not-found",
    "unsupported-repository-provider",
    "not-a-test-merge",
    "response-mismatch",
    "candidate-search-incomplete",
  ] as const;

  for (const reason of reasons) {
    const warning = formatBaselineWarning(source, { status: "unknown", reason });
    assert.match(warning ?? "", /could not tell whether the baseline matches/u);
    assert.match(warning ?? "", /Azure Pipelines build 9/u);
    assert.match(warning ?? "", /The size comparison and budgets will still run/u);
    assert.doesNotMatch(warning ?? "", /\b(?:stale|older|predates)\b/iu);
  }
  assert.match(formatBaselineWarning(source, { status: "unknown", reason: "permission-denied" }) ?? "",
    /Build Service repository Read permission/u);
});

test("GitHub discovery aligns open PR contexts to merge parent zero and prefers the exact baseline", async t => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-discovery-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  const staleBase = "1111111111111111111111111111111111111111";
  const expectedTarget = "2222222222222222222222222222222222222222";
  const head = "3333333333333333333333333333333333333333";
  const merge = "4444444444444444444444444444444444444444";
  const newer = "5555555555555555555555555555555555555555";
  const competingMerge = "6666666666666666666666666666666666666666";
  await fs.writeFile(target, "mstat");
  let includeExact = true;
  let boundedExactSearch = false;
  let exactSearchFailure: "none" | "permission" | "rate-limit-headers" | "rate-limit-message" = "none";
  let exactPages = 0;
  let commitRequests = 0;
  const server = createServer((request, response) => {
    response.setHeader("content-type", "application/json");
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    if (url.pathname.endsWith(`/git/commits/${merge}`)) {
      commitRequests++;
      response.end(JSON.stringify({ sha: merge, parents: [{ sha: expectedTarget }, { sha: head }] }));
    } else if (url.pathname.endsWith("/pulls/62")) {
      response.end(JSON.stringify({
        number: 62,
        state: "open",
        merged: false,
        mergeable: true,
        merge_commit_sha: merge,
        base: { ref: "main", sha: staleBase },
        head: { sha: head },
      }));
    } else if (/\/actions\/runs\/42\/artifacts$/u.test(url.pathname)) {
      response.end(JSON.stringify({ artifacts: includeExact
        ? [{ id: 42, name: expectedArtifact(target), expired: false }]
        : [] }));
    } else if (url.pathname.endsWith("/actions/artifacts")) {
      response.end(JSON.stringify({ artifacts: [
        { id: 60, name: expectedArtifact(target), expired: false, workflow_run: { id: 60 } },
        { id: 41, name: expectedArtifact(target), expired: false, workflow_run: { id: 41 } },
      ] }));
    } else if (url.pathname.includes("/actions/workflows/") && url.pathname.endsWith("/runs")) {
      if (url.searchParams.has("head_sha")) {
        if (exactSearchFailure !== "none") {
          response.statusCode = 403;
          if (exactSearchFailure === "rate-limit-headers") {
            response.setHeader("x-ratelimit-remaining", "0");
            response.setHeader("retry-after", "60");
          }
          response.end(JSON.stringify({ message: exactSearchFailure === "rate-limit-message"
            ? "You have exceeded a secondary rate limit. Please wait a few minutes before you try again."
            : exactSearchFailure }));
          return;
        }
        exactPages++;
        const page = url.searchParams.get("page");
        if (boundedExactSearch || (includeExact && page === "1")) {
          response.end(JSON.stringify({ workflow_runs: Array.from({ length: 100 }, (_, index) => ({
            id: 1000 + index,
            run_number: 1000 + index,
            head_branch: "different-branch",
            head_sha: expectedTarget,
            html_url: `https://example.test/run/${1000 + index}`,
            event: "push",
            conclusion: "success",
          })) }));
        } else if (includeExact && page === "2") {
          response.end(JSON.stringify({ workflow_runs: [{
            id: 42, run_number: 10, head_branch: "main", head_sha: expectedTarget,
            html_url: "https://example.test/run/42", event: "push", conclusion: "success",
          }] }));
        } else {
          response.end(JSON.stringify({ workflow_runs: [] }));
        }
      } else {
        response.end(JSON.stringify({ workflow_runs: [
          {
            id: 60, run_number: 12, head_branch: "main", head_sha: newer,
            html_url: "https://example.test/run/60", event: "push", conclusion: "success",
          },
          {
            id: 41, run_number: 9, head_branch: "main", head_sha: staleBase,
            html_url: "https://example.test/run/41", event: "push", conclusion: "success",
          },
        ] }));
      }
    } else {
      response.statusCode = 404;
      response.end(JSON.stringify({ message: "unexpected request" }));
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const scenarios = [
      {
        name: "reopened pull_request uses GITHUB_SHA",
        eventName: "pull_request",
        githubSha: merge,
        event: { action: "reopened", pull_request: {
          number: 62, state: "open", merged: false, mergeable: true,
          merge_commit_sha: competingMerge,
          base: { ref: "main", sha: staleBase }, head: { sha: head },
        } },
      },
      {
        name: "pull_request_target uses the event merge commit",
        eventName: "pull_request_target",
        githubSha: competingMerge,
        event: { pull_request: {
          number: 62, state: "open", merged: false, mergeable: true,
          merge_commit_sha: merge,
          base: { ref: "main", sha: staleBase }, head: { sha: head },
        } },
      },
      { name: "issue comment uses the PR API merge commit", eventName: "issue_comment",
        githubSha: competingMerge, event: { issue: { number: 62 } } },
      { name: "review comment uses the PR API merge commit", eventName: "pull_request_review_comment",
        githubSha: competingMerge, event: { pull_request: { number: 62 } } },
      { name: "manual dispatch uses the PR API merge commit", eventName: "workflow_dispatch",
        githubSha: competingMerge, event: { inputs: { pr_number: "62" } } },
    ];
    for (const scenario of scenarios) {
      await t.test(scenario.name, async () => {
        await fs.writeFile(eventPath, JSON.stringify(scenario.event));
        const discovery = await discoverGithubBaseline(inputs(target), "linux-x64", {
          GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
          GITHUB_REPOSITORY: "owner/repo",
          GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
          GITHUB_JOB: "size",
          GITHUB_EVENT_NAME: scenario.eventName,
          GITHUB_EVENT_PATH: eventPath,
          GITHUB_SHA: scenario.githubSha,
          GITHUB_TOKEN: "token",
          GITHUB_RUN_ID: "99",
          GITHUB_WORKSPACE: directory,
          RUNNER_TEMP: directory,
        });
        assert.equal(discovery.source.status, "restored");
        assert.equal(discovery.source.id, "42");
        assert.equal(discovery.source.commit, expectedTarget);
        assert.deepEqual(discovery.comparison, { status: "current", targetCommit: expectedTarget });
      });
    }

    includeExact = false;
    await fs.writeFile(eventPath, JSON.stringify(scenarios[0]!.event));
    const mismatched = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "pull_request",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: merge,
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(mismatched.source.id, "60");
    assert.deepEqual(mismatched.comparison, { status: "mismatched", targetCommit: expectedTarget });

    boundedExactSearch = true;
    exactPages = 0;
    const incomplete = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "pull_request",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: merge,
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(incomplete.source.id, "60");
    assert.deepEqual(incomplete.comparison, {
      status: "unknown",
      targetCommit: expectedTarget,
      reason: "candidate-search-incomplete",
    });
    assert.equal(exactPages, 10);
    boundedExactSearch = false;

    for (const scenario of [
      { name: "rate-limit headers retain the fallback baseline", mode: "rate-limit-headers" as const },
      { name: "headerless secondary-rate-limit 403 retains the fallback baseline",
        mode: "rate-limit-message" as const },
    ]) {
      await t.test(scenario.name, async () => {
        exactSearchFailure = scenario.mode;
        const throttled = await discoverGithubBaseline(inputs(target), "linux-x64", {
          GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
          GITHUB_REPOSITORY: "owner/repo",
          GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
          GITHUB_JOB: "size",
          GITHUB_EVENT_NAME: "pull_request",
          GITHUB_EVENT_PATH: eventPath,
          GITHUB_SHA: merge,
          GITHUB_TOKEN: "token",
          GITHUB_RUN_ID: "99",
          GITHUB_WORKSPACE: directory,
          RUNNER_TEMP: directory,
        });
        assert.equal(throttled.source.id, "60");
        assert.deepEqual(throttled.comparison, {
          status: "unknown",
          targetCommit: expectedTarget,
          reason: "candidate-search-incomplete",
        });
      });
    }

    exactSearchFailure = "permission";
    await assert.rejects(
      () => discoverGithubBaseline(inputs(target), "linux-x64", {
        GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
        GITHUB_REPOSITORY: "owner/repo",
        GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
        GITHUB_JOB: "size",
        GITHUB_EVENT_NAME: "pull_request",
        GITHUB_EVENT_PATH: eventPath,
        GITHUB_SHA: merge,
        GITHUB_TOKEN: "token",
        GITHUB_RUN_ID: "99",
        GITHUB_WORKSPACE: directory,
        RUNNER_TEMP: directory,
      }),
      /HTTP 403/u,
    );
    exactSearchFailure = "none";

    const beforeClosed = commitRequests;
    await fs.writeFile(eventPath, JSON.stringify({ pull_request: {
      number: 62, state: "closed", merged: true, mergeable: true,
      merge_commit_sha: merge, base: { ref: "main", sha: staleBase }, head: { sha: head },
    } }));
    const closed = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "pull_request",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: merge,
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(closed.comparison, undefined);
    assert.equal(commitRequests, beforeClosed);
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub event-bound contexts classify unavailable PR merge commits deterministically", async t => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-merge-unavailable-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  const head = "3333333333333333333333333333333333333333";
  const baselineCommit = "5555555555555555555555555555555555555555";
  await fs.writeFile(target, "mstat");
  let metadataRequests = 0;
  const server = createServer((request, response) => {
    response.setHeader("content-type", "application/json");
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    if (url.pathname.includes("/git/commits/")) {
      metadataRequests++;
      response.statusCode = 500;
      response.end(JSON.stringify({ message: "unexpected metadata request" }));
    } else if (url.pathname.endsWith("/actions/artifacts")) {
      response.end(JSON.stringify({ artifacts: [{
        id: 60, name: expectedArtifact(target), expired: false, workflow_run: { id: 60 },
      }] }));
    } else if (url.pathname.includes("/actions/workflows/") && url.pathname.endsWith("/runs")) {
      response.end(JSON.stringify({ workflow_runs: [{
        id: 60, run_number: 12, head_branch: "main", head_sha: baselineCommit,
        html_url: "https://example.test/run/60", event: "push", conclusion: "success",
      }] }));
    } else {
      response.statusCode = 404;
      response.end(JSON.stringify({ message: "unexpected request" }));
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const scenarios = [
      { mergeable: null, reason: "merge-not-ready" },
      { mergeable: false, reason: "merge-conflict" },
      { mergeable: true, reason: "merge-commit-unavailable" },
      { mergeable: undefined, reason: "merge-commit-unavailable" },
    ] as const;
    for (const scenario of scenarios) {
      await t.test(String(scenario.mergeable), async () => {
        await fs.writeFile(eventPath, JSON.stringify({ pull_request: {
          number: 62,
          state: "open",
          merged: false,
          ...(scenario.mergeable !== undefined ? { mergeable: scenario.mergeable } : {}),
          merge_commit_sha: null,
          base: { ref: "main", sha: "1111111111111111111111111111111111111111" },
          head: { sha: head },
        } }));
        const discovery = await discoverGithubBaseline(inputs(target), "linux-x64", {
          GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
          GITHUB_REPOSITORY: "owner/repo",
          GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
          GITHUB_JOB: "size",
          GITHUB_EVENT_NAME: "pull_request_target",
          GITHUB_EVENT_PATH: eventPath,
          GITHUB_SHA: "6666666666666666666666666666666666666666",
          GITHUB_TOKEN: "token",
          GITHUB_RUN_ID: "99",
          GITHUB_WORKSPACE: directory,
          RUNNER_TEMP: directory,
        });
        assert.deepEqual(discovery.comparison, { status: "unknown", reason: scenario.reason });
        assert.equal(discovery.source.commit, baselineCommit);
      });
    }
    assert.equal(metadataRequests, 0);
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub API-backed PR resolution retries mergeability and validates commit metadata", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-merge-retry-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  const expectedTarget = "2222222222222222222222222222222222222222";
  const head = "3333333333333333333333333333333333333333";
  const merge = "4444444444444444444444444444444444444444";
  const baselineCommit = "5555555555555555555555555555555555555555";
  await fs.writeFile(target, "mstat");
  await fs.writeFile(eventPath, JSON.stringify({ issue: { number: 62 } }));
  let pullRequests = 0;
  let keepMergeabilityPending = false;
  let commitRequests = 0;
  const server = createServer((request, response) => {
    response.setHeader("content-type", "application/json");
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    if (url.pathname.endsWith("/pulls/62")) {
      pullRequests++;
      response.end(JSON.stringify({
        number: 62,
        state: "open",
        merged: false,
        mergeable: keepMergeabilityPending || pullRequests === 1 ? null : true,
        merge_commit_sha: keepMergeabilityPending || pullRequests === 1 ? null : merge,
        base: { ref: "main" },
        head: { sha: head },
      }));
    } else if (url.pathname.endsWith(`/git/commits/${merge}`)) {
      commitRequests++;
      response.end(JSON.stringify({ sha: merge, parents: [{ sha: expectedTarget }, { sha: head }] }));
    } else if (url.pathname.endsWith("/actions/artifacts")) {
      response.end(JSON.stringify({ artifacts: [{
        id: 60, name: expectedArtifact(target), expired: false, workflow_run: { id: 60 },
      }] }));
    } else if (url.pathname.includes("/actions/workflows/") && url.pathname.endsWith("/runs")) {
      response.end(JSON.stringify({ workflow_runs: url.searchParams.has("head_sha") ? [] : [{
        id: 60, run_number: 12, head_branch: "main", head_sha: baselineCommit,
        html_url: "https://example.test/run/60", event: "push", conclusion: "success",
      }] }));
    } else {
      response.statusCode = 404;
      response.end(JSON.stringify({ message: "unexpected request" }));
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const discovery = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "issue_comment",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: "6666666666666666666666666666666666666666",
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(pullRequests, 2);
    assert.equal(commitRequests, 1);
    assert.deepEqual(discovery.comparison, { status: "mismatched", targetCommit: expectedTarget });

    keepMergeabilityPending = true;
    pullRequests = 0;
    const pending = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "issue_comment",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: "6666666666666666666666666666666666666666",
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(pullRequests, 3);
    assert.equal(commitRequests, 1, "Pending mergeability must not inspect a possibly stale merge SHA");
    assert.deepEqual(pending.comparison, { status: "unknown", reason: "merge-not-ready" });
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub optional commit-metadata failures become stable unknown reasons", async t => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-metadata-reasons-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  const expectedTarget = "2222222222222222222222222222222222222222";
  const head = "3333333333333333333333333333333333333333";
  const merge = "4444444444444444444444444444444444444444";
  const baselineCommit = "5555555555555555555555555555555555555555";
  await fs.writeFile(target, "mstat");
  await fs.writeFile(eventPath, JSON.stringify({ pull_request: {
    number: 62, state: "open", merged: false, mergeable: true, merge_commit_sha: merge,
    base: { ref: "main" }, head: { sha: head },
  } }));
  let responseMode = "permission";
  const server = createServer((request, response) => {
    response.setHeader("content-type", "application/json");
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    if (url.pathname.endsWith(`/git/commits/${merge}`)) {
      if (responseMode === "permission") {
        response.statusCode = 403;
        response.end(JSON.stringify({ message: "forbidden" }));
      } else if (responseMode === "mismatch") {
        response.end(JSON.stringify({ sha: baselineCommit, parents: [{ sha: expectedTarget }, { sha: head }] }));
      } else if (responseMode === "head-mismatch") {
        response.end(JSON.stringify({ sha: merge, parents: [{ sha: expectedTarget }, { sha: baselineCommit }] }));
      } else {
        response.end(JSON.stringify({
          sha: merge,
          parents: [{ sha: expectedTarget }, { sha: head }, { sha: baselineCommit }],
        }));
      }
    } else if (url.pathname.endsWith("/actions/artifacts")) {
      response.end(JSON.stringify({ artifacts: [{
        id: 60, name: expectedArtifact(target), expired: false, workflow_run: { id: 60 },
      }] }));
    } else if (url.pathname.includes("/actions/workflows/") && url.pathname.endsWith("/runs")) {
      response.end(JSON.stringify({ workflow_runs: [{
        id: 60, run_number: 12, head_branch: "main", head_sha: baselineCommit,
        html_url: "https://example.test/run/60", event: "push", conclusion: "success",
      }] }));
    } else {
      response.statusCode = 404;
      response.end(JSON.stringify({ message: "unexpected request" }));
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const scenarios = [
      { mode: "permission", reason: "permission-denied" },
      { mode: "mismatch", reason: "response-mismatch" },
      { mode: "head-mismatch", reason: "not-a-test-merge" },
      { mode: "octopus", reason: "not-a-test-merge" },
    ] as const;
    for (const scenario of scenarios) {
      await t.test(scenario.mode, async () => {
        responseMode = scenario.mode;
        const discovery = await discoverGithubBaseline(inputs(target), "linux-x64", {
          GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
          GITHUB_REPOSITORY: "owner/repo",
          GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
          GITHUB_JOB: "size",
          GITHUB_EVENT_NAME: "pull_request_target",
          GITHUB_EVENT_PATH: eventPath,
          GITHUB_SHA: baselineCommit,
          GITHUB_TOKEN: "token",
          GITHUB_RUN_ID: "99",
          GITHUB_WORKSPACE: directory,
          RUNNER_TEMP: directory,
        });
        assert.deepEqual(discovery.comparison, { status: "unknown", reason: scenario.reason });
        assert.equal(discovery.source.commit, baselineCommit);
      });
    }
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub discovery reports the permission required for an authorization failure", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-permission-"));
  const target = path.join(directory, "app.mstat");
  await fs.writeFile(target, "mstat");
  const server = createServer((_request, response) => {
    response.statusCode = 403;
    response.end("forbidden");
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    await assert.rejects(
      () => discoverGithubBaseline(inputs(target), "linux-x64", {
        GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
        GITHUB_REPOSITORY: "owner/repo",
        GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
        GITHUB_JOB: "size",
        GITHUB_EVENT_NAME: "push",
        GITHUB_REF: "refs/heads/main",
        GITHUB_TOKEN: "token",
        GITHUB_WORKSPACE: directory,
        RUNNER_TEMP: directory,
      }),
      /HTTP 403.*actions: read/u,
    );
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub discovery proves a first run with one matching-artifact request", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-first-run-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  await fs.writeFile(target, "mstat");
  let requests = 0;
  const server = createServer((_request, response) => {
    requests++;
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify({ artifacts: [] }));
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const discovery = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "push",
      GITHUB_REF: "refs/heads/main",
      GITHUB_REF_NAME: "main",
      GITHUB_RUN_ID: "51",
      GITHUB_TOKEN: "token",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });

    assert.equal(discovery.source.status, "not-found");
    assert.equal(discovery.publish, true);
    assert.equal(requests, 1);

    await fs.writeFile(eventPath, JSON.stringify({ pull_request: {
      number: 62,
      state: "open",
      merged: false,
      mergeable: true,
      merge_commit_sha: "5555555555555555555555555555555555555555",
      base: { ref: "main" },
      head: { sha: "3333333333333333333333333333333333333333" },
    } }));
    const pullRequestFirstRun = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/main",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "pull_request",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_SHA: "4444444444444444444444444444444444444444",
      GITHUB_RUN_ID: "52",
      GITHUB_TOKEN: "token",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(pullRequestFirstRun.source.status, "not-found");
    assert.equal(pullRequestFirstRun.comparison, undefined);
    assert.equal(requests, 2, "Open-PR first runs must not request merge metadata");
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("explicit baselines omit alignment without provider metadata", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-explicit-baseline-"));
  try {
    const target = path.join(directory, "target.mstat");
    const baseline = path.join(directory, "baseline.mstat");
    await fs.writeFile(target, "target");
    await fs.writeFile(baseline, "baseline");
    const explicitInputs = { ...inputs(target), baseline };

    const github = await discoverGithubBaseline(explicitInputs, "linux-x64", {});
    assert.equal(github.source.status, "explicit");
    assert.equal(github.comparison, undefined);

    const azure = await discoverAzureBaseline(explicitInputs, "linux-x64", {});
    assert.equal(azure.source.status, "explicit");
    assert.equal(azure.comparison, undefined);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure discovery treats no successful artifact as first run and clears its token", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-discovery-"));
  const target = path.join(directory, "app.mstat");
  await fs.writeFile(target, "mstat");
  const server = createServer((_request, response) => {
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify({ value: [] }));
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const environment: NodeJS.ProcessEnv = {
      SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: `http://127.0.0.1:${address.port}`,
      SYSTEM_TEAMPROJECTID: "project",
      SYSTEM_DEFINITIONID: "12",
      SYSTEM_JOBNAME: "size",
      BUILD_SOURCEBRANCH: "refs/heads/main",
      BUILD_BUILDID: "99",
      BUILD_SOURCESDIRECTORY: directory,
      AGENT_TEMPDIRECTORY: directory,
      ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
    };
    const discovery = await discoverAzureBaseline(inputs(target), "linux-x64", environment);
    assert.equal(discovery.source.status, "not-found");
    assert.equal(discovery.publish, true);
    assert.equal(environment.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN, undefined);
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure discovery selects the exact target baseline over a newer eligible build", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-restore-"));
  const target = path.join(directory, "app.mstat");
  const stale = "1111111111111111111111111111111111111111";
  const expectedTarget = "2222222222222222222222222222222222222222";
  const head = "3333333333333333333333333333333333333333";
  const merge = "4444444444444444444444444444444444444444";
  const newer = "5555555555555555555555555555555555555555";
  await fs.writeFile(target, "real baseline mstat bytes");
  const identity = createBaselineIdentity(
    "azure-pipelines", "project/12", "size", target, "linux-x64", "app", directory, directory,
  );
  const artifactName = baselineArtifactName(identity);
  const source = {
    status: "restored" as const,
    provider: "azure-pipelines" as const,
    branch: "refs/heads/main",
    commit: expectedTarget,
    id: "304",
    number: "304",
    artifactName,
  };
  const staged = path.join(directory, "staged");
  await stageBaseline(report(target, target), identity, source, staged);
  const archive = storedZip([
    [`${artifactName}/dotsider-baseline.json`, await fs.readFile(path.join(staged, "dotsider-baseline.json"))],
    [`${artifactName}/files/target.mstat`, await fs.readFile(path.join(staged, "files", "target.mstat"))],
  ]);

  const requestedUrls: string[] = [];
  let repeatContinuation = false;
  let exactArtifactPermissionDenied = false;
  let root = "";
  const server = createServer((request, response) => {
    requestedUrls.push(request.url ?? "");
    if (request.headers.authorization !== "Bearer secret-token") {
      response.statusCode = 401;
      response.end("missing token");
      return;
    }
    if (request.url?.includes("/_apis/git/repositories/repository/commits/")) {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ commitId: merge, parents: [expectedTarget, head] }));
    } else if (request.url?.includes("/_apis/build/builds?")
        && request.url.includes("continuationToken=next")) {
      response.setHeader("content-type", "application/json");
      if (repeatContinuation) response.setHeader("x-ms-continuationtoken", "next");
      response.end(JSON.stringify({ value: repeatContinuation ? [{
        id: 305,
        buildNumber: "305",
        sourceBranch: "refs/heads/main",
        sourceVersion: newer,
        result: "succeeded",
      }] : [{
        id: 304,
        buildNumber: "304",
        sourceBranch: "refs/heads/main",
        sourceVersion: expectedTarget,
        result: "succeeded",
      }] }));
    } else if (request.url?.includes("/_apis/build/builds?")) {
      response.setHeader("content-type", "application/json");
      response.setHeader("x-ms-continuationtoken", "next");
      response.end(JSON.stringify({ value: [{
        id: 305,
        buildNumber: "305",
        sourceBranch: "refs/heads/main",
        sourceVersion: newer,
        result: "succeeded",
      }, {
        id: 303,
        buildNumber: "303",
        sourceBranch: "refs/heads/main",
        sourceVersion: stale,
        result: "succeeded",
      }, {
        id: 307,
        buildNumber: "307",
        sourceBranch: "refs/heads/main",
        sourceVersion: expectedTarget,
        result: "partiallySucceeded",
      }] }));
    } else if (request.url?.includes("/artifacts?")
        && request.headers.accept === "application/zip") {
      response.setHeader("content-type", "application/zip");
      response.end(archive);
    } else if (request.url?.includes("/artifacts?")) {
      if (exactArtifactPermissionDenied && request.url.includes("/builds/304/")) {
        response.statusCode = 403;
        response.end(JSON.stringify({ message: "forbidden" }));
        return;
      }
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ name: artifactName }));
    } else {
      response.setHeader("content-type", "application/zip");
      response.end(archive);
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    root = `http://127.0.0.1:${address.port}`;
    const environment: NodeJS.ProcessEnv = {
      SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: root,
      SYSTEM_TEAMPROJECTID: "project",
      SYSTEM_DEFINITIONID: "12",
      SYSTEM_JOBNAME: "size",
      BUILD_REASON: "PullRequest",
      BUILD_REPOSITORY_PROVIDER: "TfsGit",
      BUILD_REPOSITORY_ID: "repository",
      BUILD_REPOSITORY_LOCALPATH: path.join(directory, "not-checked-out"),
      BUILD_SOURCEVERSION: merge,
      SYSTEM_PULLREQUEST_SOURCECOMMITID: head,
      SYSTEM_PULLREQUEST_TARGETBRANCH: "main",
      BUILD_SOURCEBRANCH: "refs/pull/62/merge",
      BUILD_BUILDID: "306",
      BUILD_SOURCESDIRECTORY: directory,
      AGENT_TEMPDIRECTORY: directory,
      ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
    };

    const discovery = await discoverAzureBaseline(inputs(target), "linux-x64", environment);
    assert.equal(discovery.source.status, "restored");
    assert.equal(discovery.source.id, "304");
    assert.equal(discovery.source.branch, "refs/heads/main");
    assert.equal(discovery.source.commit, expectedTarget);
    assert.equal(discovery.source.url, `${root}/project/_build/results?buildId=304`);
    assert.deepEqual(discovery.comparison, { status: "current", targetCommit: expectedTarget });
    assert.equal(discovery.publish, false);
    assert.ok(requestedUrls.some(url => url.includes("branchName=refs%2Fheads%2Fmain")));
    assert.ok(requestedUrls.some(url => url.includes("resultFilter=succeeded")));
    assert.equal(requestedUrls.some(url => url.includes("/builds/307/artifacts")), false);
    assert.ok(discovery.downloadDirectory);
    const restored = await restoreBaseline(discovery.downloadDirectory, identity, discovery.source);
    assert.equal(await fs.readFile(restored.targetPath, "utf8"), "real baseline mstat bytes");
    assert.equal(environment.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN, undefined);

    exactArtifactPermissionDenied = true;
    await assert.rejects(
      () => discoverAzureBaseline(inputs(target), "linux-x64", {
        ...environment,
        ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
      }),
      /HTTP 403/u,
    );
    exactArtifactPermissionDenied = false;

    repeatContinuation = true;
    const incompleteEnvironment = {
      ...environment,
      ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
    };
    const incomplete = await discoverAzureBaseline(inputs(target), "linux-x64", incompleteEnvironment);
    assert.equal(incomplete.source.id, "305");
    assert.deepEqual(incomplete.comparison, {
      status: "unknown",
      targetCommit: expectedTarget,
      reason: "candidate-search-incomplete",
    });
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure target resolution limits REST fallback and maps metadata failures", async t => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-target-reasons-"));
  const merge = "4444444444444444444444444444444444444444";
  const target = "2222222222222222222222222222222222222222";
  const head = "3333333333333333333333333333333333333333";
  const other = "5555555555555555555555555555555555555555";
  let mode = "permission";
  let requests = 0;
  const server = createServer((_request, response) => {
    requests++;
    response.setHeader("content-type", "application/json");
    if (mode === "permission") {
      response.statusCode = 403;
      response.end(JSON.stringify({ message: "forbidden" }));
    } else if (mode === "not-found") {
      response.statusCode = 404;
      response.end(JSON.stringify({ message: "missing" }));
    } else if (mode === "missing-id") {
      response.end(JSON.stringify({ parents: [target, head] }));
    } else if (mode === "response-mismatch") {
      response.end(JSON.stringify({ commitId: other, parents: [target, head] }));
    } else if (mode === "invalid-parents") {
      response.end(JSON.stringify({ commitId: merge, parents: [target] }));
    } else if (mode === "source-mismatch") {
      response.end(JSON.stringify({ commitId: merge, parents: [target, other] }));
    } else {
      response.statusCode = 400;
      response.end(JSON.stringify({ message: "bad request" }));
    }
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const collection = `http://127.0.0.1:${address.port}`;
    const environment: NodeJS.ProcessEnv = {
      BUILD_REPOSITORY_PROVIDER: "TfsGit",
      BUILD_REPOSITORY_ID: "repository",
      BUILD_REPOSITORY_LOCALPATH: path.join(directory, "not-checked-out"),
      BUILD_SOURCEVERSION: merge,
      SYSTEM_PULLREQUEST_SOURCECOMMITID: head,
    };
    const scenarios = [
      { mode: "permission", reason: "permission-denied" },
      { mode: "not-found", reason: "commit-not-found" },
      { mode: "missing-id", reason: "response-mismatch" },
      { mode: "response-mismatch", reason: "response-mismatch" },
      { mode: "invalid-parents", reason: "not-a-test-merge" },
      { mode: "source-mismatch", reason: "not-a-test-merge" },
      { mode: "bad-request", reason: "provider-unavailable" },
    ] as const;
    for (const scenario of scenarios) {
      await t.test(scenario.mode, async () => {
        mode = scenario.mode;
        assert.deepEqual(
          await resolveAzureExpectedTarget(collection, "project", "token", environment),
          { status: "unknown", reason: scenario.reason },
        );
      });
    }

    const beforeUnsupported = requests;
    assert.deepEqual(await resolveAzureExpectedTarget(collection, "project", "token", {
      ...environment,
      BUILD_REPOSITORY_PROVIDER: "TfsVersionControl",
    }), { status: "unknown", reason: "unsupported-repository-provider" });
    assert.equal(requests, beforeUnsupported);

    assert.deepEqual(await resolveAzureExpectedTarget(collection, "project", "token", {
      ...environment,
      BUILD_REPOSITORY_PROVIDER: "Git",
    }), { status: "unknown", reason: "repository-not-checked-out" });
    assert.equal(requests, beforeUnsupported);
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure discovery reports Build Service permissions and clears a rejected token", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-permission-"));
  const target = path.join(directory, "app.mstat");
  await fs.writeFile(target, "mstat");
  const server = createServer((_request, response) => {
    response.statusCode = 403;
    response.end("forbidden");
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    assert.ok(address && typeof address !== "string");
    const environment: NodeJS.ProcessEnv = {
      SYSTEM_TEAMFOUNDATIONCOLLECTIONURI: `http://127.0.0.1:${address.port}`,
      SYSTEM_TEAMPROJECTID: "project",
      SYSTEM_DEFINITIONID: "12",
      SYSTEM_JOBNAME: "size",
      BUILD_SOURCEBRANCH: "refs/heads/main",
      BUILD_BUILDID: "99",
      BUILD_SOURCESDIRECTORY: directory,
      AGENT_TEMPDIRECTORY: directory,
      ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
    };

    await assert.rejects(
      () => discoverAzureBaseline(inputs(target), "linux-x64", environment),
      /HTTP 403.*Build Service permission/u,
    );
    assert.equal(environment.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN, undefined);
  } finally {
    server.close();
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure local Git resolution uses exact objects and reports availability failures", async () => {
  const repository = path.resolve(__dirname, "../../..");
  const head = execFileSync("git", ["-C", repository, "rev-parse", "HEAD"], { encoding: "utf8" }).trim();
  const readmeBlob = execFileSync("git", ["-C", repository, "rev-parse", "HEAD:README.md"],
    { encoding: "utf8" }).trim();

  assert.deepEqual(
    await resolveLocalMergeTargetCommit(path.join(repository, "missing-checkout"), head),
    { status: "unknown", reason: "repository-not-checked-out" },
  );
  assert.deepEqual(
    await resolveLocalMergeTargetCommit(repository, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
    { status: "unknown", reason: "commit-not-found" },
  );
  assert.deepEqual(
    await resolveLocalMergeTargetCommit(repository, head, undefined, path.join(repository, "missing-git")),
    { status: "unknown", reason: "git-unavailable" },
  );
  assert.deepEqual(
    await resolveLocalMergeTargetCommit(repository, readmeBlob),
    { status: "unknown", reason: "not-a-test-merge" },
  );
  assert.deepEqual(parseGitCommitParents([
    "tree aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "parent 1111111111111111111111111111111111111111",
    "parent 2222222222222222222222222222222222222222",
    "author Dotsider <dotsider@example.test> 0 +0000",
    "",
    "Recorded merge object headers",
  ].join("\n")), [
    "1111111111111111111111111111111111111111",
    "2222222222222222222222222222222222222222",
  ]);
});

test("Azure ZIP extraction rejects parent traversal", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-zip-traversal-"));
  try {
    await assert.rejects(
      () => extractZipArchive(storedZip([["../secret", Buffer.from("secret")]]), directory),
      /Unsafe ZIP path/u,
    );
    await assert.rejects(() => fs.stat(path.join(directory, "..", "secret")));
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("Azure ZIP extraction rejects an entry whose bytes do not match its CRC", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-zip-crc-"));
  try {
    const archive = storedZip([["baseline/file.mstat", Buffer.from("real bytes")]]);
    const nameLength = archive.readUInt16LE(26);
    archive[30 + nameLength] = archive[30 + nameLength]! ^ 0xff;
    await assert.rejects(
      () => extractZipArchive(archive, directory),
      /CRC validation/u,
    );
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

function inputs(target: string): SizeCheckInputs {
  return {
    target,
    baselineKey: "app",
    budgets: ["max=25mb"],
    top: 10,
    why: false,
    dotsiderVersion: "latest",
    reportDirectory: path.join(path.dirname(target), "report"),
    publishSummary: true,
    publishReports: true,
    artifactName: "dotsider-size-check",
  };
}

function expectedArtifact(target: string): string {
  return baselineArtifactName(createBaselineIdentity(
    "github-actions", "owner/repo/ci.yml", "size", target, "linux-x64", "app",
    path.dirname(target), path.dirname(target),
  ));
}

function report(binary: string, mstat: string, dgml?: string): SizeReport {
  return {
    schemaVersion: 2,
    target: binary,
    baseline: null,
    targetArtifacts: {
      inputPath: binary,
      binaryPath: binary === mstat ? undefined : binary,
      mstatPath: mstat,
      dgmlPath: dgml,
    },
    totalBasis: "fileSize",
    leftTotal: null,
    rightTotal: 13,
    summary: { delta: 13 },
  };
}

function storedZip(entries: readonly (readonly [string, Buffer])[]): Buffer {
  const localEntries: Buffer[] = [];
  const centralEntries: Buffer[] = [];
  let localOffset = 0;
  for (const [name, contents] of entries) {
    const nameBytes = Buffer.from(name);
    const crc = crc32(contents);
    const local = Buffer.alloc(30 + nameBytes.length + contents.length);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(contents.length, 18);
    local.writeUInt32LE(contents.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);
    nameBytes.copy(local, 30);
    contents.copy(local, 30 + nameBytes.length);
    localEntries.push(local);

    const central = Buffer.alloc(46 + nameBytes.length);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(contents.length, 20);
    central.writeUInt32LE(contents.length, 24);
    central.writeUInt16LE(nameBytes.length, 28);
    central.writeUInt32LE(localOffset, 42);
    nameBytes.copy(central, 46);
    centralEntries.push(central);
    localOffset += local.length;
  }
  const central = Buffer.concat(centralEntries);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(entries.length, 8);
  eocd.writeUInt16LE(entries.length, 10);
  eocd.writeUInt32LE(central.length, 12);
  eocd.writeUInt32LE(localOffset, 16);
  return Buffer.concat([...localEntries, central, eocd]);
}

function crc32(buffer: Buffer): number {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
}
