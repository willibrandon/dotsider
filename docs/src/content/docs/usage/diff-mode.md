---
title: Diff Mode
description: Side-by-side assembly comparison.
---

Diff mode compares two assemblies side by side:

```
dotsider diff v1.dll v2.dll
```

Each tab shows both assemblies with differences highlighted. Added items appear in green, removed in red, changed in yellow.

## Filters

Press `f` to cycle through diff filters:

| Filter | What it shows |
|--------|---------------|
| **All** | Everything from both assemblies |
| **Added** | Items only in the right assembly |
| **Removed** | Items only in the left assembly |
| **Changed** | Items present in both but different |

This is useful for reviewing breaking changes between library versions, auditing what changed in a new build, or understanding the impact of a refactoring.
