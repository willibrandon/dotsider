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
  resolveLocalMergeTargetCommit,
} from "../src/azure-baseline";
import {
  baselineArtifactName,
  createBaselineIdentity,
  detectRidFromHeader,
  detectTargetRid,
  enrichReports,
  formatBaselineWarning,
  restoreBaseline,
  stageBaseline,
  withManagedBaselineFreshness,
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
    const manifest = JSON.parse(manifestText) as { targetPath: string };
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
      commit: "abcdef1234567890",
      artifactName: "baseline",
    });

    const enriched = await fs.readFile(markdown, "utf8");
    assert.match(enriched, /## Size check\r\n\r\n\*\*Baseline:\*\* Restored from run 7 at `abcdef123456` on `main`\.\r\n\r\n---\r\n/u);
    assert.ok(enriched.indexOf("**Baseline:**") < enriched.indexOf("### Overview"));
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("managed baseline freshness only classifies restored pull-request baselines", () => {
  const restored = {
    status: "restored" as const,
    provider: "github-actions" as const,
    branch: "main",
    commit: "ABCDEF1234567890",
    id: "41",
    number: "9",
    url: "https://example.test/run/41",
    artifactName: "baseline",
  };

  const current = withManagedBaselineFreshness(restored, true, "abcdef1234567890");
  assert.deepEqual(current, {
    ...restored,
    targetCommit: "abcdef1234567890",
    freshness: "current",
  });
  assert.equal(formatBaselineWarning(current), undefined);
  const stale = withManagedBaselineFreshness(restored, true, "fedcba0987654321");
  assert.equal(stale.freshness, "stale");
  assert.equal(stale.targetCommit, "fedcba0987654321");
  assert.equal(withManagedBaselineFreshness(restored, true, undefined).freshness, "unknown");
  assert.equal(withManagedBaselineFreshness(restored, false, "fedcba0987654321").freshness, undefined);
  assert.equal(withManagedBaselineFreshness({ status: "explicit", path: "base.mstat" }, true, "target").freshness, undefined);
});

test("stale report enrichment identifies both commits and the source run with refresh guidance", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-stale-baseline-report-"));
  try {
    const json = path.join(directory, "report.json");
    const markdown = path.join(directory, "report.md");
    const source = {
      status: "restored" as const,
      provider: "github-actions" as const,
      branch: "main",
      commit: "1111111111111111111111111111111111111111",
      targetCommit: "2222222222222222222222222222222222222222",
      freshness: "stale" as const,
      id: "41",
      number: "9",
      url: "https://example.test/run/41",
      artifactName: "baseline",
    };
    await fs.writeFile(json, JSON.stringify(report("app", "app.mstat")), "utf8");
    await fs.writeFile(markdown, "## Size check\n\n---\n\n### Overview\n", "utf8");

    const enriched = await enrichReports(json, markdown, source);
    const summary = await fs.readFile(markdown, "utf8");
    const warning = formatBaselineWarning(source);

    assert.equal(enriched.baselineSource?.targetCommit, source.targetCommit);
    assert.equal(enriched.baselineSource?.freshness, "stale");
    assert.match(summary, /> \*\*Warning:\*\* The managed baseline does not match this pull request target/u);
    assert.match(summary, /`222222222222`.*`111111111111`.*\[GitHub Actions run 9\]\(https:\/\/example\.test\/run\/41\)/u);
    assert.match(summary, /target branch `main` needs a successful Dotsider size-check run/u);
    assert.match(summary, /available baseline and all configured budgets are still evaluated/u);
    assert.ok(warning);
    assert.match(warning, /'222222222222'.*'111111111111'.*GitHub Actions run 9/u);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("raw Git commit parsing retains both merge parents from a shallow commit object", () => {
  assert.deepEqual(parseGitCommitParents([
    "tree 0123456789012345678901234567890123456789",
    "parent 1111111111111111111111111111111111111111",
    "parent 2222222222222222222222222222222222222222",
    "author Example <example@example.test> 0 +0000",
    "",
    "Synthetic pull-request merge",
  ].join("\n")), [
    "1111111111111111111111111111111111111111",
    "2222222222222222222222222222222222222222",
  ]);
});

test("Azure external-provider resolution reads the target parent from a shallow merge checkout", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-shallow-merge-"));
  try {
    runGit(directory, "init", "--initial-branch=main");
    runGit(directory, "config", "user.name", "Dotsider Tests");
    runGit(directory, "config", "user.email", "dotsider@example.test");
    await fs.writeFile(path.join(directory, "app.txt"), "base\n");
    runGit(directory, "add", "app.txt");
    runGit(directory, "commit", "-m", "base");
    const targetCommit = runGit(directory, "rev-parse", "HEAD").trim();
    runGit(directory, "switch", "-c", "feature");
    await fs.writeFile(path.join(directory, "feature.txt"), "feature\n");
    runGit(directory, "add", "feature.txt");
    runGit(directory, "commit", "-m", "feature");
    runGit(directory, "switch", "main");
    runGit(directory, "merge", "--no-ff", "feature", "-m", "pull request merge");
    const mergeCommit = runGit(directory, "rev-parse", "HEAD").trim();
    const gitDirectory = runGit(directory, "rev-parse", "--git-dir").trim();
    await fs.writeFile(path.resolve(directory, gitDirectory, "shallow"), `${mergeCommit}\n`, "ascii");

    const resolved = await resolveLocalMergeTargetCommit({
      BUILD_SOURCEVERSION: mergeCommit,
      BUILD_REPOSITORY_LOCALPATH: directory,
    });

    assert.equal(resolved, targetCommit);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("GitHub freshness uses the tested shallow merge parent instead of stale PR base metadata", async t => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-github-discovery-"));
  const target = path.join(directory, "app.mstat");
  const eventPath = path.join(directory, "event.json");
  runGit(directory, "init", "--initial-branch=main");
  runGit(directory, "config", "user.name", "Dotsider Tests");
  runGit(directory, "config", "user.email", "dotsider@example.test");
  await fs.writeFile(path.join(directory, "base.txt"), "initial base\n");
  runGit(directory, "add", "base.txt");
  runGit(directory, "commit", "-m", "initial base");
  const staleBaseCommit = runGit(directory, "rev-parse", "HEAD").trim();
  runGit(directory, "switch", "-c", "feature");
  await fs.writeFile(path.join(directory, "feature.txt"), "pull request\n");
  runGit(directory, "add", "feature.txt");
  runGit(directory, "commit", "-m", "pull request head");
  const headCommit = runGit(directory, "rev-parse", "HEAD").trim();
  runGit(directory, "switch", "main");
  await fs.writeFile(path.join(directory, "base.txt"), "advanced base\n");
  runGit(directory, "add", "base.txt");
  runGit(directory, "commit", "-m", "advance target after PR metadata was captured");
  const targetCommit = runGit(directory, "rev-parse", "HEAD").trim();
  runGit(directory, "merge", "--no-ff", "feature", "-m", "synthetic pull request merge");
  const mergeCommit = runGit(directory, "rev-parse", "HEAD").trim();
  const gitDirectory = runGit(directory, "rev-parse", "--git-dir").trim();
  await fs.writeFile(path.resolve(directory, gitDirectory, "shallow"), `${mergeCommit}\n`, "ascii");
  await fs.writeFile(target, "mstat");
  const server = createServer((request, response) => {
    response.setHeader("content-type", "application/json");
    if (request.url?.includes("/pulls/62")) {
      response.end(JSON.stringify({
        number: 62,
        base: { ref: "main", sha: staleBaseCommit },
        head: { sha: headCommit },
        merge_commit_sha: mergeCommit,
      }));
    } else if (request.url?.includes("/actions/artifacts?")) {
      response.end(JSON.stringify({ artifacts: [
        { id: 8, name: expectedArtifact(target), expired: false, workflow_run: { id: 50 } },
        { id: 7, name: expectedArtifact(target), expired: true, workflow_run: { id: 49 } },
        { id: 6, name: expectedArtifact(target), expired: false, workflow_run: { id: 41 } },
      ] }));
    } else if (request.url?.includes("/runs?")) {
      response.end(JSON.stringify({ workflow_runs: [
        {
          id: 50, run_number: 11, head_branch: "main", head_sha: "pr-sha",
          html_url: "https://example.test/run/50", event: "pull_request", conclusion: "success",
        },
        {
          id: 49, run_number: 10, head_branch: "main", head_sha: "expired-sha",
          html_url: "https://example.test/run/49", event: "push", conclusion: "success",
        },
        {
          id: 41, run_number: 9, head_branch: "main", head_sha: targetCommit,
          html_url: "https://example.test/run/41", event: "push", conclusion: "success",
        },
      ] }));
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
        name: "pull_request event",
        eventName: "pull_request",
        event: {
          pull_request: {
            base: { ref: "main", sha: staleBaseCommit },
            head: { sha: headCommit },
            merge_commit_sha: mergeCommit,
          },
        },
      },
      {
        name: "issue_comment API lookup",
        eventName: "issue_comment",
        event: { issue: { number: 62 } },
      },
      {
        name: "workflow_dispatch API lookup",
        eventName: "workflow_dispatch",
        event: { inputs: { pr_number: "62" } },
      },
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
          GITHUB_SHA: scenario.eventName === "pull_request" ? mergeCommit : staleBaseCommit,
          GITHUB_TOKEN: "token",
          GITHUB_RUN_ID: "99",
          GITHUB_WORKSPACE: directory,
          RUNNER_TEMP: directory,
        });
        assert.equal(discovery.source.status, "restored");
        assert.equal(discovery.source.id, "41");
        assert.equal(discovery.source.branch, "main");
        assert.equal(discovery.source.commit, targetCommit);
        assert.equal(discovery.source.targetCommit, targetCommit);
        assert.equal(discovery.source.freshness, "current");
        assert.equal(discovery.publish, false);
        assert.notEqual(staleBaseCommit, targetCommit);
      });
    }

    await fs.rename(path.resolve(directory, gitDirectory), path.join(directory, "git-metadata-unavailable"));
    await fs.writeFile(eventPath, JSON.stringify({ issue: { number: 62 } }));
    const unknown = await discoverGithubBaseline(inputs(target), "linux-x64", {
      GITHUB_API_URL: `http://127.0.0.1:${address.port}`,
      GITHUB_REPOSITORY: "owner/repo",
      GITHUB_WORKFLOW_REF: "owner/repo/.github/workflows/ci.yml@refs/heads/feature",
      GITHUB_JOB: "size",
      GITHUB_EVENT_NAME: "issue_comment",
      GITHUB_EVENT_PATH: eventPath,
      GITHUB_TOKEN: "token",
      GITHUB_RUN_ID: "99",
      GITHUB_WORKSPACE: directory,
      RUNNER_TEMP: directory,
    });
    assert.equal(unknown.source.status, "restored");
    assert.equal(unknown.source.targetCommit, undefined);
    assert.equal(unknown.source.freshness, "unknown");
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
  } finally {
    server.close();
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

test("Azure pull-request discovery normalizes a short target branch and restores its baseline", async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "dotsider-azure-restore-"));
  const target = path.join(directory, "app.mstat");
  await fs.writeFile(target, "real baseline mstat bytes");
  const identity = createBaselineIdentity(
    "azure-pipelines", "project/12", "size", target, "linux-x64", "app", directory, directory,
  );
  const artifactName = baselineArtifactName(identity);
  const baselineCommit = "1111111111111111111111111111111111111111";
  const mergeCommit = "3333333333333333333333333333333333333333";
  const targetCommit = "2222222222222222222222222222222222222222";
  const source = {
    status: "restored" as const,
    provider: "azure-pipelines" as const,
    branch: "refs/heads/main",
    commit: baselineCommit,
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
      response.end(JSON.stringify({
        commitId: mergeCommit,
        parents: [targetCommit, "4444444444444444444444444444444444444444"],
      }));
    } else if (request.url?.includes("/_apis/build/builds?")) {
      response.setHeader("content-type", "application/json");
      response.end(JSON.stringify({ value: [{
        id: 304,
        buildNumber: "304",
        sourceBranch: "refs/heads/main",
        sourceVersion: baselineCommit,
        result: "succeeded",
      }] }));
    } else if (request.url?.includes("/artifacts?")
        && request.headers.accept === "application/zip") {
      response.setHeader("content-type", "application/zip");
      response.end(archive);
    } else if (request.url?.includes("/artifacts?")) {
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
      BUILD_SOURCEVERSION: mergeCommit,
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
    assert.equal(discovery.source.targetCommit, targetCommit);
    assert.equal(discovery.source.freshness, "stale");
    assert.equal(discovery.publish, false);
    assert.ok(requestedUrls.some(url => url.includes("branchName=refs%2Fheads%2Fmain")));
    assert.ok(discovery.downloadDirectory);
    const restored = await restoreBaseline(discovery.downloadDirectory, identity, discovery.source);
    assert.equal(await fs.readFile(restored.targetPath, "utf8"), "real baseline mstat bytes");
    assert.equal(environment.ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN, undefined);

    const external = await discoverAzureBaseline(inputs(target), "linux-x64", {
      ...environment,
      BUILD_REPOSITORY_PROVIDER: "GitHub",
      BUILD_REPOSITORY_ID: undefined,
      BUILD_SOURCEVERSION: mergeCommit,
      BUILD_REPOSITORY_LOCALPATH: path.join(directory, "missing-checkout"),
      BUILD_SOURCESDIRECTORY: path.join(directory, "missing-checkout"),
      ENDPOINT_AUTH_PARAMETER_SYSTEMVSSCONNECTION_ACCESSTOKEN: "secret-token",
    });
    assert.equal(external.source.status, "restored");
    assert.equal(external.source.targetCommit, undefined);
    assert.equal(external.source.freshness, "unknown");
    assert.match(formatBaselineWarning(external.source) ?? "", /could not determine the target commit/u);
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

function runGit(directory: string, ...args: readonly string[]): string {
  return execFileSync("git", ["-C", directory, ...args], {
    encoding: "utf8",
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
}
