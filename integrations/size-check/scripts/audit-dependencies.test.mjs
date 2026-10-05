import assert from "node:assert/strict";
import { test } from "node:test";
import { evaluateAudit } from "./audit-dependencies.mjs";

const braces = {
  github_advisory_id: "GHSA-vfj7-8cjw-p6xm",
  module_name: "braces",
  patched_versions: "<0.0.0",
  findings: [{ paths: [".>tfx-cli>shelljs>fast-glob>micromatch>braces"] }],
};

function auditResult(advisories) {
  return {
    status: advisories.length > 0 ? 1 : 0,
    stdout: JSON.stringify({
      advisories: Object.fromEntries(advisories.map((advisory, index) => [index, advisory])),
      metadata: { vulnerabilities: { high: advisories.length } },
    }),
  };
}

test("a clean audit passes", () => {
  assert.deepEqual(evaluateAudit(auditResult([]), "azure-devops"), { deferred: [], blocking: [] });
});

test("the known unpatched packaging advisory remains visible without blocking", () => {
  assert.deepEqual(evaluateAudit(auditResult([braces]), "azure-devops"), {
    deferred: [braces], blocking: [],
  });
});

test("the same advisory blocks as soon as the registry lists a patched version", () => {
  const patched = { ...braces, patched_versions: ">=3.0.4" };
  assert.deepEqual(evaluateAudit(auditResult([patched]), "azure-devops"), {
    deferred: [], blocking: [patched],
  });
});

test("other advisories block even when they have no fix", () => {
  const other = { ...braces, github_advisory_id: "GHSA-other-advisory" };
  assert.deepEqual(evaluateAudit(auditResult([braces, other]), "azure-devops"), {
    deferred: [braces], blocking: [other],
  });
});

test("the exception is limited to the Azure packaging dependency graph", () => {
  assert.deepEqual(evaluateAudit(auditResult([braces]), "integrations/size-check"), {
    deferred: [], blocking: [braces],
  });
  const runtime = { ...braces, findings: [{ paths: [".>tfx-cli>braces", ".>runtime>braces"] }] };
  assert.deepEqual(evaluateAudit(auditResult([runtime]), "azure-devops"), {
    deferred: [], blocking: [runtime],
  });
});

test("missing fix or dependency-path details cannot defer a finding", () => {
  for (const incomplete of [
    { ...braces, patched_versions: undefined },
    { ...braces, patched_versions: "" },
    { ...braces, module_name: "another-package" },
    { ...braces, findings: [] },
    { ...braces, findings: [{ paths: [] }] },
  ]) {
    assert.equal(evaluateAudit(auditResult([incomplete]), "azure-devops").blocking.length, 1);
  }
});

test("registry errors, malformed reports, and process failures fail the audit", () => {
  for (const result of [
    { status: 1, stdout: JSON.stringify({ error: { code: "ECONNRESET" } }) },
    { status: 0, stdout: "not json" },
    { status: 0, stdout: "{}" },
    { ...auditResult([]), status: 1 },
    { ...auditResult([braces]), status: 2 },
    { ...auditResult([braces]), signal: "SIGTERM" },
    { ...auditResult([braces]), error: new Error("spawn pnpm ENOENT") },
    { status: 0, stdout: JSON.stringify({ advisories: {}, metadata: { vulnerabilities: { high: 1 } } }) },
  ]) {
    assert.throws(() => evaluateAudit(result, "azure-devops"));
  }
});
