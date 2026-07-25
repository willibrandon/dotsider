---
title: Size Regression
description: Diff two Native AOT builds by size and gate CI on size budgets.
---

![Native AOT size regression delta treemap](../../../assets/screenshots/size-regression.png)

Native AOT binaries grow for reasons the source diff never shows: a new generic instantiation
drags in the type loader, a LINQ call materializes a family of enumerator types, a string
literal freezes into the image. dotsider turns that into a first-class workflow: **diff two
builds' ILC size reports so the regression reads as a treemap**, and **gate CI on size
budgets** with the top contributors printed when one breaks.

## Publishing the inputs

The size report is the `.mstat` file ILC emits when a project publishes with
`IlcGenerateMstatFile`; the dependency graph (`IlcGenerateDgmlFile`) additionally answers
*why* an entry is in the binary:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <IlcGenerateMstatFile>true</IlcGenerateMstatFile>
  <IlcGenerateDgmlFile>true</IlcGenerateDgmlFile>
</PropertyGroup>
```

ILC writes both to the native intermediate directory
(`obj/Release/<tfm>/<rid>/native/`). Copy them beside the published binary so dotsider's
sidecar discovery finds them:

```xml
<Target Name="CopyAotSidecarsToPublish" AfterTargets="Publish">
  <ItemGroup>
    <_AotSidecar Include="$(NativeIntermediateOutputPath)$(TargetName).mstat" />
    <_AotSidecar Include="$(NativeIntermediateOutputPath)$(TargetName).codegen.dgml.xml" />
  </ItemGroup>
  <Copy SourceFiles="@(_AotSidecar)" DestinationFolder="$(PublishDir)"
        Condition="Exists('%(_AotSidecar.Identity)')" SkipUnchangedFiles="true" />
</Target>
```

Every size command accepts either a bare `.mstat` file or an AOT binary with the sidecar
beside it.

## Interactive: the delta treemap

```
dotsider diff before.mstat after.mstat
dotsider diff bin/v1/publish/app bin/v2/publish/app
```

Two mstat-backed inputs open the size-diff TUI — Summary and Size Map tabs — instead of the
managed diff (AOT binaries carry no ECMA-335 metadata, so the managed tabs would be empty
tables). See [Diff Mode](/usage/diff-mode/) for the keys and the treemap encoding. Add
`--json` to skip the TUI and print the machine-readable size-diff document instead.

## Headless: `dotsider size-check`

The CI command. It compares a target against a baseline, renders the report in `text`,
`json`, or `markdown`, evaluates size budgets, and exits non-zero when one breaks:

```
dotsider size-check out/pr/app --baseline baseline/app.mstat --top 20
dotsider size-check out/pr/app --baseline baseline/app.mstat \
  --budget max=25mb --budget growth=1% --budget ns=System.Text.Json:growth=10kb
```

| Exit code | Meaning |
|-----------|---------|
| 0 | Report produced; every error-severity budget passed |
| 1 | Usage or input error (missing file, no mstat, invalid budget) |
| 2 | A budget with `error` severity was exceeded |

### Budget grammar

`[scope:]limit(,limit)*` — repeatable via `--budget`:

| Piece | Forms | Notes |
|-------|-------|-------|
| scope | `total` (default) · `ns=<Namespace>` · `asm=<Assembly>` | `ns=` covers sub-namespaces: `System.Text.Json` includes `System.Text.Json.Serialization`, never `System.Text.Json2` |
| limit | `max=SIZE` · `growth=SIZE` · `growth=PERCENT` | `max=` caps the current value; `growth=` caps the change versus `--baseline` |
| SIZE | `4096`, `4096b`, `10kb`, `25mb`, `1gb` | 1 kb = 1024 bytes; bare numbers are bytes |
| PERCENT | `1%`, `2.5%` | growth only; a brand-new scope (baseline 0) always breaches |

Examples: `max=25mb` · `growth=1%` · `total:max=25mb,growth=50kb` ·
`ns=System.Text.Json:growth=10kb` · `asm=MyApp:max=2mb`.

### Budget files

`--budget-file budgets.json` accepts spec strings and object entries in one document. The
object form is how a team names budgets, downgrades one to a warning (reported, never fails
the gate), or pins a per-budget contributor count:

```json
{
  "budgets": [
    "total:max=25mb",
    "total:growth=1%",
    {
      "name": "JSON serializer growth",
      "description": "System.Text.Json tends to bloat via new converters.",
      "scope": "ns=System.Text.Json",
      "growth": "10kb",
      "severity": "warning",
      "topN": 5
    }
  ]
}
```

### What the numbers measure

Every report states its **basis**. Binaries measure **file size on disk** (`fileSize`); a
bare `.mstat` anywhere in the pair measures **mstat attributable totals** (`mstatTotal`) on
both sides so the figures stay comparable — the two differ by headers, alignment, and bytes
the report does not attribute. Namespace and assembly budgets always measure mstat
aggregates: methods, MethodTables, RVA fields, and frozen objects attributed via their
*owning type* (the code that caused the bytes). Ownerless frozen objects — string literals —
land in an explicit `(unattributed)` bucket that scoped budgets never draw from but the
aggregates always show, so no byte is silently dropped.

### Why did this appear?

`--why` attaches the ILC dependency chain for the top added contributors (requires the
target's DGML sidecar): the root kept X, X kept Y, down to the new entry.

## Wiring it into CI

### GitHub Actions

```yaml
- name: Publish PR build
  run: dotnet publish src/App -c Release -r linux-x64 -o out/pr

- name: Restore baseline size report
  uses: actions/download-artifact@v4
  with:
    name: size-baseline        # published from the main branch build
    path: baseline

- name: Size gate
  run: >
    dotsider size-check out/pr/App
    --baseline baseline/App.mstat
    --budget total:growth=1%
    --budget ns=MyApp.Generated:growth=0
    --format markdown --summary-file "$GITHUB_STEP_SUMMARY"
```

The markdown report lands in the job's step summary; the exit code fails the job when an
error-severity budget breaks. Publish the current `.mstat` as the `size-baseline` artifact
from the main-branch workflow so pull requests always diff against the latest mainline build.

### Azure DevOps

```yaml
- script: >
    dotsider size-check $(Build.SourcesDirectory)/out/pr/App
    --baseline $(Pipeline.Workspace)/size-baseline/App.mstat
    --budget total:growth=1%
    --format markdown --summary-file $(Build.ArtifactStagingDirectory)/size-gate.md
  displayName: Size gate
- task: PublishBuildArtifacts@1
  condition: always()
  inputs:
    pathToPublish: $(Build.ArtifactStagingDirectory)/size-gate.md
    artifactName: size-gate
```

## From an agent

The MCP server exposes the same comparison as [`diff_size` and
`check_size_budgets`](/reference/mcp/), including inline budget JSON with the object form —
so an agent can ask "does this build pass the team's budgets" and get the structured report
back.
