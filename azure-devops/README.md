# Dotsider for Azure Pipelines

`DotsiderSizeCheck@1` enforces NativeAOT size budgets and publishes the same JSON and Markdown reports as `dotsider size-check`.

Publish the application with NativeAOT size sidecars, then invoke the task on pull requests
and on the target branch:

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/myapp'
    budgets: max=25mb
```

The first successful branch build enforces absolute limits and stores a managed baseline.
Later branch builds compare with the preceding successful build, and pull requests compare
with the newest successful build of their target branch. The task downloads the matching
released Dotsider binary, verifies its published SHA-256 checksum, and caches it by version
and runtime identifier. Set `dotsiderPath` to use an existing installation without network
access.

Exit code 2 means an error-severity budget was exceeded. Reports and the pipeline summary are published before the task fails. Exit code 1 means the command or its inputs were invalid.

Add growth budgets immediately. When no stored baseline exists, they are reported as deferred
while absolute limits still run; the successful branch build establishes the baseline:

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/myapp'
    budgets: |
      total:max=25mb
      total:growth=50kb
      ns=MyApp.Serialization:growth=10kb
    why: true
```

No setting changes between the first run and later comparisons. Set `baseline` only to
override the managed lifecycle with an explicit file; automatic discovery and publication
are disabled for that invocation. Use `baselineKey` only when the target path is unstable.

Application publishing stays explicit because the project chooses its target framework,
runtime identifier, and publish options. Managed artifacts are isolated by pipeline
definition, job, logical target, and RID, and only wholly successful builds are eligible.

See the [CI integration reference](https://dotsider.dev/reference/ci-integrations/) for
inputs, outputs, compatibility, and complete examples. Source and issue tracking are on
[GitHub](https://github.com/willibrandon/dotsider).
