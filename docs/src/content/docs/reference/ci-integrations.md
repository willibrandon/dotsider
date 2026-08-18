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

For pull requests, Dotsider also verifies that the restored baseline commit is the exact
target parent of the merge commit that was actually tested. On GitHub, it reads the first
parent directly from the checked-out merge commit—even in the default depth-1 checkout—rather
than trusting `pull_request.base.sha`, which can lag a regenerated merge ref. This applies to
direct pull-request events and API-backed comment or manual-dispatch workflows. If the commits
differ, the provider log and Markdown summary identify both commits and the source run, then
explain that the target branch needs a successful size-check run to refresh its baseline. The
available comparison and every configured budget still run; an otherwise passing check
reports `passed-with-warnings`.

If GitHub does not have the tested merge commit checked out, freshness is `unknown`; Dotsider
does not substitute potentially stale pull-request metadata. Check out the pull-request merge
ref before building when a comment-triggered or manually dispatched workflow needs freshness
verification.

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

The Azure task uses the current pipeline's Build Service identity to read successful builds,
artifacts, and exact merge-commit metadata from the same project. No PAT is required. If
access was removed, grant that identity read access to builds, artifacts, and source metadata.
For non-Azure source providers, the task reads the checked-out merge commit locally. If that
checkout is unavailable, freshness is `unknown`, an actionable warning is shown, and the
comparison continues without inventing a target commit.

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
`baselineSourceUrl`, `baselineArtifactName`, `baselineTargetCommit`, and
`baselineFreshness`. GitHub spells multiword outputs with hyphens; Azure uses camel case.
`baselineStatus` is `restored`, `explicit`, or `not-found`; `baselineTotal` is empty on a
first run. `baselineFreshness` is `current`, `stale`, or `unknown` for a restored managed
pull-request baseline and empty when freshness does not apply. Full commit IDs are retained
in JSON and outputs while human-readable warnings use their first 12 characters.

`result` is `passed`, `passed-with-warnings`, `budget-failed`, or `error`. A stale or
unverifiable managed baseline upgrades an otherwise passing result to `passed-with-warnings`
without changing exit code 0. A budget failure retains the raw exit code 2 and an input or
execution error retains exit code 1. JSON reports carry `schemaVersion: 2`, resolved
target-side artifact paths, baseline provenance (including target commit and freshness), and
deferred budget metrics so consumers can reject an incompatible future shape explicitly.

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
handlers. Pin an immutable GitHub release such as `@v0.25.2` where supply-chain policy
requires it; `@v0` tracks the latest compatible release.

Release CI tests acquisition on Windows x64 and ARM64, Linux x64 and ARM64, and macOS x64 and
ARM64 before marketplace publication. Release archives and the Azure VSIX carry GitHub build
provenance attestations. Prereleases do not update the Azure Marketplace or moving GitHub
Action tag.
