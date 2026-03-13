---
title: General
description: Assembly identity, framework, architecture, and dependencies.
---

![General tab](../../../assets/screenshots/general.png)

The **General** tab (`1`) is the first thing you see when opening an assembly. It shows:

- **Assembly identity** — name, version, culture, public key token
- **Target framework** — which .NET version the assembly targets
- **Architecture** — AnyCPU, x64, ARM64, etc.
- **Dependency table** — all referenced assemblies with their versions

## Drill into references

Select any row in the dependency table and press `Enter`. If the referenced assembly exists on disk (next to the current file or in a probing path), dotsider opens it in a new analysis context. Press `Backspace` to return.

This lets you walk an entire dependency chain without leaving the TUI.
