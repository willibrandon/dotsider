---
title: Size Map
description: Treemap of code size — assembly, namespace, type, method.
---

The **Size Map** tab (`7`) shows a treemap of IL code size:

- **Assembly** at the top level
- **Namespaces** as the first breakdown
- **Types** within each namespace
- **Methods** as leaf nodes, sized by IL byte count

## Drill in

Click or press `Enter` on any region to drill into it. When you reach a method leaf, pressing `Enter` jumps to its IL disassembly in tab 3.

This is useful for finding unexpectedly large methods or identifying which parts of your codebase contribute the most to assembly size.
