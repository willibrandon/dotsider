---
title: CI Integrations
description: GitHub Actions and Azure Pipelines contracts for NativeAOT size checks.
---

Dotsider publishes a composite GitHub Action and `DotsiderSizeCheck@1` Azure Pipelines task.
Both run the existing `dotsider size-check` command, preserve its exit codes, and publish the
CLI-generated JSON and Markdown reports before failing a budget gate.

## Start with an absolute limit

An absolute limit needs only the NativeAOT build you already publish. It is the simplest
useful guardrail and does not require a baseline:

```yaml
- uses: willibrandon/dotsider@v0
  id: size
  with:
    target: out/current/App
    budgets: max=25mb
```

The equivalent Azure Pipelines task is:

```yaml
- task: DotsiderSizeCheck@1
  name: size
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/App'
    budgets: max=25mb
```

Choose the initial cap from a known-good build and leave enough room for ordinary compiler
and dependency changes. Tighten it once the application has a stable size envelope.

## Compare two builds

Add `baseline` when the job already has an older build to compare. This enables `growth=`
budgets and changes the report from current-build totals to a before-and-after comparison:

```yaml
- uses: willibrandon/dotsider@v0
  with:
    target: out/current/App
    baseline: out/base/App
    budgets: |
      total:max=25mb,growth=1%
      ns=MyApp.Generated:growth=10kb
    why: true
```

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/App'
    baseline: '$(Agent.TempDirectory)/dotsider-base/App'
    budgets: |
      total:max=25mb,growth=1%
      ns=MyApp.Generated:growth=10kb
    why: true
```

For pull requests, build the base commit and current commit in the same job. That keeps the
SDK, NativeAOT toolchain, runtime identifier, and runner constant, so the report measures the
code change instead of a toolchain change. The [size-regression guide](/usage/size-regression/)
contains complete GitHub Actions and Azure Pipelines examples.

Dotsider does not search workflow history or manage baseline artifacts. Projects vary too
widely in runtime identifiers, publish properties, retention, permissions, and generated
inputs for that behavior to be reliable inside a size-check action.

## Inputs

| GitHub | Azure | Meaning |
| --- | --- | --- |
| `target` | `target` | Required NativeAOT binary or `.mstat` report |
| `baseline` | `baseline` | Optional older binary or `.mstat`; enables comparison and `growth=` budgets |
| `budgets` | `budgets` | Budget expressions, one per line |
| `budget-file` | `budgetFile` | JSON budget document |
| `top` | `top` | Contributors per section; default 10 |
| `why` | `why` | Attach dependency chains from the target DGML sidecar |
| `dotsider-version` | `dotsiderVersion` | Exact release or `latest` |
| `dotsider-path` | `dotsiderPath` | Existing executable; bypasses acquisition |
| `report-directory` | `reportDirectory` | JSON and Markdown destination |
| `publish-summary` | `publishSummary` | Publish the Markdown summary |
| `upload-reports` | `publishReports` | Publish both reports as an artifact |
| `artifact-name` | `artifactName` | Report artifact name |

Without `baseline`, use absolute `max=` budgets. With `baseline`, both `max=` and `growth=`
budgets are valid. A growth budget without a baseline fails as an input error instead of
silently skipping the check.

## Outputs

Both integrations expose `result`, `exitCode`, `jsonReportPath`, `markdownReportPath`,
`artifactName`, `dotsiderVersion`, `totalBasis`, `baselineTotal`, `currentTotal`, `delta`, and
`violationCount`. GitHub spells multiword outputs with hyphens; Azure uses camel case.
`baselineTotal` is empty when no baseline was supplied.

`result` is `passed`, `passed-with-warnings`, `budget-failed`, or `error`. A budget failure
retains the raw exit code 2 and an input or execution error retains exit code 1. JSON reports
carry `schemaVersion: 1` so consumers can reject an incompatible future shape explicitly.

## Acquisition and compatibility

The integrations map Windows, Linux, macOS, x64, and ARM64 agents to the matching NativeAOT
release. Alpine and other detected musl hosts select the musl archive. Each download is size
bounded, checked for unsafe archive paths, verified against the adjacent SHA-256 release
sidecar, and cached by exact version and runtime identifier. An explicit `dotsiderPath` is
never resolved through GitHub and is suitable for offline agents.

The Azure extension is public under publisher `willibrandon`. `DotsiderSizeCheck@1` requires
agent 3.230.2 or newer and supplies Node 24 and Node 20 handlers. Pin an immutable GitHub
release such as `@v0.24.9` where supply-chain policy requires it; `@v0` tracks the latest
compatible release.

Release CI tests acquisition on Windows x64 and ARM64, Linux x64 and ARM64, and macOS x64 and
ARM64 before marketplace publication. Release archives and the Azure VSIX carry GitHub build
provenance attestations. Prereleases do not update the Azure Marketplace or moving GitHub
Action tag.
