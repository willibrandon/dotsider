---
title: IL Inspector
description: Namespace/type/method tree with IL disassembly.
---

![IL Inspector tab](/screenshots/il-inspector.png)

The **IL Inspector** tab (`3`) shows a tree of all namespaces, types, and methods. Select a method to see its IL bytecode in the right pane.

Each instruction shows:

- **Offset** — byte offset within the method body
- **Opcode** — the IL instruction
- **Operand** — decoded metadata tokens, string literals, branch targets

## Cross-tab navigation

- Press `x` on a selected method to jump to its body in the **Hex Dump** tab. The hex view scrolls to the method's RVA.
- Press `Backspace` to return to where you came from.

## Search

Press `/` to search method names or IL content. Matches are highlighted in both the tree and disassembly panes.
