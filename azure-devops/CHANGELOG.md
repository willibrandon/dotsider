# Changelog

## 0.24.9

- Align the size-check verdict with the rest of the report in GitHub and Azure Pipelines summaries.

## 0.24.8

- Render size summaries as narrow lists with visible section dividers so GitHub and Azure Pipelines remain readable without hiding contributor details.

## 0.24.7

- Remove the mode input. Supplying a baseline now enables comparison budgets; omitting it supports absolute budgets for the current build.

## 0.24.6

- Require an explicit current or compare mode for size checks.
- Show complete contributor names with clear spacing between report sections.

## 0.24.5

- Present checks without a baseline as current-build reports instead of synthetic regressions.
- Put the budget verdict first and shorten oversized contributor names in Markdown summaries.

## 0.24.4

- Keep omitted optional paths unset instead of resolving them to the pipeline working directory.
- Preserve CLR generic names and keep size values on one line in Markdown reports.

## 0.24.3

- Show measured size results in task logs and retain the failure reason when Azure Pipelines reports a failed size check.

## 0.1.0

- Add `DotsiderSizeCheck@1` with typed NativeAOT size-check inputs, verified tool acquisition, reports, summaries, and stable output variables.
