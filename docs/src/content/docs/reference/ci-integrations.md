---
title: CI Integrations
description: GitHub Actions and Azure Pipelines contracts for NativeAOT size checks.
---

Dotsider publishes the
[Dotsider Size Check GitHub Action](https://github.com/marketplace/actions/dotsider-size-check)
and the
[Dotsider Azure DevOps extension](https://marketplace.visualstudio.com/items?itemName=willibrandon.dotsider),
which provides the `DotsiderSizeCheck@1` Azure Pipelines task. Both run the existing
`dotsider size-check` command, preserve its exit codes, publish the CLI-generated JSON and
Markdown reports before failing a budget gate, and manage matching branch baselines without
project-specific artifact queries.

## Automatic branch baselines

Run the integration after the NativeAOT publish on pull requests and on the target branch.
The first successful branch run enforces absolute limits and stores the binary plus its
resolved `.mstat` and optional DGML sidecars. Later branch runs compare with the preceding
successful run; pull requests compare with the newest successful run of their target branch.

```yaml
- uses: willibrandon/dotsider@v0
  id: size
  with:
    target: out/current/App
    budgets: max=25mb
```

The job needs read access to earlier workflow artifacts:

```yaml
permissions:
  actions: read
  contents: read
```

The equivalent Azure Pipelines task is:

```yaml
- task: DotsiderSizeCheck@1
  name: size
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/App'
    budgets: max=25mb
```

The Azure task uses the current pipeline's Build Service identity to read successful builds
and artifacts from the same pipeline definition. No PAT is required. If access was removed,
grant that identity read access to builds and artifacts.

For a pull request, Dotsider warns when the managed baseline comes from a different
target-branch commit. The warning includes both commits and the source run or build, then
asks for a successful size check on the target branch. The size check and budgets still run.
Dotsider uses a baseline from the matching commit when one is available; otherwise it uses
the latest successful baseline.

The report's `baselineComparison` is `current` when the commits match, `mismatched` when they
differ, and `unknown` when commit details are unavailable. Dotsider warns and continues the
size check and budgets. Budget failures and other errors still fail normally. Azure warnings
leave the task `Succeeded`, so a successful branch build can still be used as a future
baseline.

This comparison applies only when Dotsider finds a managed baseline for an open pull
request. The JSON object and its three outputs are omitted for explicit baselines, first
runs, branch builds, and closed or merged pull requests. An `unknown` result includes one of
these reason codes:
`permission-denied`, `merge-not-ready`, `merge-conflict`, `merge-commit-unavailable`,
`provider-unavailable`, `repository-not-checked-out`, `git-unavailable`, `commit-not-found`,
`unsupported-repository-provider`, `not-a-test-merge`, `response-mismatch`, or
`candidate-search-incomplete`. The warning explains what to do next.

GitHub needs `actions: read` to find baselines and `contents: read` to inspect commits. Azure
normally uses the local Git checkout. If Azure Repos must use its API, the pipeline Build
Service identity also needs repository **Read** permission. This comparison supports Git
repositories. A failure to inspect a commit produces `unknown`; a failure to read builds or
artifacts remains an error.

`baselineComparison` covers the stored baseline. For a file supplied as `target`, the
workflow determines which build supplied it. This matters most for comment, review, and
manual workflows that download files from another run.

When no matching baseline exists, `max=` limits still run and `growth=` limits are named as
deferred in the summary and JSON report. A successful branch run then establishes the first
baseline. Network, authentication, corrupt-artifact, and manifest-validation failures are
errors; they never masquerade as a first run.

## Explicit baseline override

Set `baseline` when the job deliberately owns both inputs. That disables automatic discovery
and publication for this invocation:

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

This remains useful for release-to-release checks or pipelines that intentionally rebuild
the base revision with the current toolchain. The [size-regression guide](/usage/size-regression/)
contains complete examples.

## On-demand pull-request reports

Projects that do not want to run the comparison on every pull request can publish the binary
and sidecars from their normal build, then add a trusted `/aot-size` comment workflow. The
command works in the pull-request conversation and in review comments. It
downloads the successful PR and base-branch input artifacts, runs Dotsider with an explicit
baseline, and updates one PR comment. The analysis job never checks out or executes PR code
with a write-capable token, and the Dotsider child process does not inherit provider tokens.

Copy the [on-demand workflow template](https://github.com/willibrandon/dotsider/blob/main/integrations/size-check/examples/github-aot-size.yml),
then change its build workflow, artifact name, target path, and budgets. It also supports a
manual `workflow_dispatch` with a pull-request number.

## Inputs

| GitHub | Azure | Meaning |
| --- | --- | --- |
| `target` | `target` | Required NativeAOT binary or `.mstat` report |
| `baseline` | `baseline` | Optional older binary or `.mstat`; enables comparison and `growth=` budgets |
| `baseline-key` | `baselineKey` | Stable logical target key when a temporary target path changes between runs |
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

An automatically missing baseline defers growth limits only after provider discovery proves
that no matching artifact exists. Direct CLI use remains strict: growth budgets require
`--baseline`.

## Outputs

Both integrations expose `result`, `exitCode`, `jsonReportPath`, `markdownReportPath`,
`artifactName`, `dotsiderVersion`, `totalBasis`, `baselineTotal`, `currentTotal`, `delta`, and
`violationCount`, `baselineStatus`, `baselineSourceId`, `baselineSourceCommit`,
`baselineSourceUrl`, `baselineArtifactName`, `baselineTargetCommit`,
`baselineComparisonStatus`, and `baselineComparisonReason`. GitHub spells multiword outputs
with hyphens; Azure uses camel case. `baselineComparisonStatus` is `current`, `mismatched`, or
`unknown`; it and the other two comparison outputs are empty when alignment is not
applicable. The reason is populated only for `unknown`.
`baselineStatus` is `restored`, `explicit`, or `not-found`; `baselineTotal` is empty on a
first run.

`result` is `passed`, `passed-with-warnings`, `budget-failed`, or `error`. A budget failure
retains the raw exit code 2 and an input or execution error retains exit code 1. JSON reports
carry `schemaVersion: 2`, resolved target-side artifact paths, durable baseline provenance,
the optional invocation comparison, and deferred budget metrics so consumers can reject an
incompatible future shape explicitly. Managed baseline manifests remain schema 1 and never
store the invocation comparison.

Managed artifact names are derived from the workflow or pipeline definition, stable job,
logical target, and detected RID. Discovery searches only successful runs of the exact base
branch. Pull-request artifacts are never eligible as baselines. `baseline-key` is normally
unnecessary; use it when a randomized temporary target path would otherwise change identity.

## Acquisition and compatibility

The integrations map Windows, Linux, macOS, x64, and ARM64 agents to the matching NativeAOT
release. Alpine and other detected musl hosts select the musl archive. Each download is size
bounded, checked for unsafe archive paths, verified against the adjacent SHA-256 release
sidecar, and cached by exact version and runtime identifier. An explicit `dotsiderPath` is
never resolved through GitHub and is suitable for offline agents.

The Azure extension is public under publisher
[`willibrandon`](https://marketplace.visualstudio.com/publishers/willibrandon).
`DotsiderSizeCheck@1` requires agent 3.230.2 or newer and supplies Node 24 and Node 20
handlers. Pin an immutable GitHub release such as `@v0.26.0` where supply-chain policy
requires it; `@v0` tracks the latest compatible release.

Release CI tests acquisition on Windows x64 and ARM64, Linux x64 and ARM64, and macOS x64 and
ARM64 before marketplace publication. Release archives and the Azure VSIX carry GitHub build
provenance attestations. Prereleases do not update the Azure Marketplace or moving GitHub
Action tag.
