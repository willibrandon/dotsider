---
title: Dep Graph
description: Visual dependency graph with edge weights by TypeRef count.
---

![Dep Graph tab](../../../assets/screenshots/dep-graph.png)

The **Dep Graph** tab (`6`) renders a visual dependency graph:

- Your assembly sits at the root
- Each referenced assembly appears as a node
- Edge weights show how many TypeRefs point to that dependency

## Navigation

Press `Enter` on any dependency node to open that assembly in a new analysis context (if the file is found on disk). Press `Backspace` to return.

This gives you a quick sense of which dependencies are most heavily used and lets you drill into any of them.
