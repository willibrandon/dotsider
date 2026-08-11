---
title: CI Integrations
description: GitHub Actions and Azure Pipelines contracts for NativeAOT size checks.
---

Dotsider publishes a composite GitHub Action and `DotsiderSizeCheck@1` Azure Pipelines task.
Both wrap the existing `dotsider size-check` contract: they pass each typed value as its own
process argument, preserve exit codes 0, 1, and 2, and publish the CLI-generated JSON and
Markdown reports before failing a budget gate.

## GitHub Action

```yaml
- uses: willibrandon/dotsider@v0
  id: size
  with:
    target: out/current/App
    baseline: out/baseline/App.mstat
    budget-file: eng/size-budgets.json
    top: '20'
    why: 'true'
```

Pin an immutable release such as `@v0.7.0` where supply-chain policy requires it. The moving
major tag is advanced only after the release archives pass cross-platform acquisition tests.

## Azure Pipelines task

Install **Dotsider** from the Azure DevOps Marketplace, then use the versioned task name:

```yaml
- task: DotsiderSizeCheck@1
  name: size
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/App'
    baseline: '$(Pipeline.Workspace)/baseline/App.mstat'
    budgetFile: '$(Build.SourcesDirectory)/eng/size-budgets.json'
    top: '20'
    why: true
```

The extension is public under publisher `willibrandon`. `DotsiderSizeCheck@1` requires agent
3.220.0 or newer and supplies Node 24 and Node 20 handlers.

## Inputs

| GitHub | Azure | Meaning |
| --- | --- | --- |
| `target` | `target` | Required NativeAOT binary or `.mstat` report |
| `baseline` | `baseline` | Optional baseline binary or `.mstat` report |
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

## Outputs

Both integrations expose `result`, `exitCode`, `jsonReportPath`, `markdownReportPath`,
`artifactName`, `dotsiderVersion`, `totalBasis`, `baselineTotal`, `currentTotal`, `delta`, and
`violationCount`. GitHub spells multiword outputs with hyphens; Azure uses camel case.

`result` is `passed`, `passed-with-warnings`, `budget-failed`, or `error`. A budget failure
retains the raw exit code 2 and an input or execution error retains exit code 1. JSON reports
carry `schemaVersion: 1` so consumers can reject an incompatible future shape explicitly.

## Acquisition and compatibility

The integrations map Windows, Linux, macOS, x64, and ARM64 agents to the matching NativeAOT
release. Alpine and other detected musl hosts select the musl archive. Each download is size
bounded, checked for unsafe archive paths, verified against the adjacent SHA-256 release
sidecar, and cached by exact version and runtime identifier. An explicit `dotsiderPath` is
never resolved through GitHub and is suitable for offline agents.

Release CI tests acquisition on Windows x64 and ARM64, Linux x64 and ARM64, and macOS x64 and
ARM64 before marketplace publication. Release archives and the Azure VSIX carry GitHub build
provenance attestations. Prereleases do not update the Azure Marketplace or moving GitHub
Action tag.

Publishing the application and retrieving its baseline are intentionally outside the task.
Use each platform's native build and artifact features so retention, permissions, and the
chosen NativeAOT runtime remain explicit.
