---
title: Diff Mode
description: Side-by-side assembly comparison.
---

![Diff Mode](../../../assets/screenshots/diff-mode.png)

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

## What gets compared

Types are matched by fully qualified name. Methods are matched by declaring type, name, and signature. Assembly references are matched by name.

For matched methods, the diff detects changes across several layers:

- **Attributes** — visibility, vtable layout, sealed/abstract flags
- **Implementation attributes** — IL vs native, managed vs unmanaged
- **Local variable signatures** — local types decoded and compared element-by-element
- **Exception regions** — try/catch structure, handler offsets, catch types resolved by name
- **IL instructions** — a normalized walk that resolves metadata token operands (method calls, field accesses, type references, string literals, standalone signatures) to their semantic names before comparing, so metadata table reordering between builds does not produce false positives

The Change column on the Methods tab shows which layers differ: `attributes`, `impl`, `body`, or a combination.

## Copy

The Summary tab has selectable info panels and change statistics. Press `Tab` to cycle focus between them, select text, and press `y` to copy. `iw` and `yiw` work in these panels for quick word-level copying. `V` selects the entire line and `yy` copies it.

On the Types, Methods, and References tabs, focus a row and press `y` to copy it as tab-separated values. A brief flash confirms the yank.
