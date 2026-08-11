# Dotsider for Azure Pipelines

`DotsiderSizeCheck@1` enforces NativeAOT size budgets and publishes the same JSON and Markdown reports as `dotsider size-check`.

Publish the application with NativeAOT size sidecars, then start with an absolute limit:

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/myapp'
    budgets: max=25mb
```

The task downloads the matching released Dotsider binary, verifies its published SHA-256 checksum, and caches it by version and runtime identifier. Set `dotsiderPath` to use an existing installation without network access.

Exit code 2 means an error-severity budget was exceeded. Reports and the pipeline summary are published before the task fails. Exit code 1 means the command or its inputs were invalid.

When the pipeline builds both revisions, pass the older binary as `baseline` and add growth budgets:

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/myapp'
    baseline: '$(Agent.TempDirectory)/base/myapp'
    budgets: |
      total:growth=50kb
      ns=MyApp.Serialization:growth=10kb
    why: true
```

No setting needs to change between these uses. An omitted baseline measures the current build and supports `max=` budgets. A supplied baseline enables the comparison and `growth=` budgets. A growth budget without a baseline is an input error.

Application publishing stays explicit because the project chooses its target framework, runtime identifier, and publish options. For pull requests, build the base and current revisions in the same job instead of relying on a retained artifact from a different toolchain.
