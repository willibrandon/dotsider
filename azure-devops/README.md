# Dotsider for Azure Pipelines

`DotsiderSizeCheck@1` enforces NativeAOT size budgets and publishes the same JSON and Markdown reports as `dotsider size-check`.

Publish the application with NativeAOT size sidecars, download the baseline through the pipeline's normal artifact task, then pass both paths to Dotsider:

```yaml
- task: DotsiderSizeCheck@1
  inputs:
    target: '$(Build.ArtifactStagingDirectory)/current/myapp'
    baseline: '$(Pipeline.Workspace)/baseline/myapp'
    budgets: |
      total:growth=50kb
      ns=MyApp.Serialization:growth=10kb
    why: true
```

The task downloads the matching released Dotsider binary, verifies its published SHA-256 checksum, and caches it by version and runtime identifier. Set `dotsiderPath` to use an existing installation without network access.

Exit code 2 means an error-severity budget was exceeded. Reports and the pipeline summary are published before the task fails. Exit code 1 means the command or its inputs were invalid.

Application publishing and baseline retention stay explicit because their artifact lifetime and access rules belong to the pipeline that owns them.
