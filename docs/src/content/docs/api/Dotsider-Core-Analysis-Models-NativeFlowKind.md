---
title: "NativeFlowKind"
description: "How a decoded instruction affects control flow. Drives listing navigation (which instructions carry a jumpable target) and future analysis without re-parsing the mnemonic."
slug: api/dotsider.core.analysis.models.nativeflowkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

How a decoded instruction affects control flow. Drives listing navigation (which instructions
carry a jumpable target) and future analysis without re-parsing the mnemonic.

```csharp
public enum NativeFlowKind
```

## Fields

### Call

A direct call to a computed absolute target.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
Call = 1
```

### ConditionalBranch

A conditional branch to a computed absolute target.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
ConditionalBranch = 3
```

### IndirectCall

A call through a register or memory operand; the target is not a direct immediate.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
IndirectCall = 5
```

### IndirectJump

A jump through a register or memory operand; the target is not a direct immediate.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
IndirectJump = 6
```

### Jump

An unconditional direct jump to a computed absolute target.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
Jump = 2
```

### Return

A return from the current function.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
Return = 4
```

### Sequential

Falls through to the next instruction.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
Sequential = 0
```
