# Changelog

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
