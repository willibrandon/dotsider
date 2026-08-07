---
title: "PolicyLayer"
description: "Identifies which layer of .NET Framework binding policy rewrote a requested assembly identity. The CLR walks app config first, then publisher policy (unless bypassed by &lt;publisherPolicy apply=\"no\"/&gt;), then machine.config; later layers override earlier ones, so the effective winner is machine.config &gt; publisher &gt; app &gt; framework unification."
slug: api/dotsider.core.analysis.models.policylayer
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Identifies which layer of .NET Framework binding policy rewrote a requested assembly
identity. The CLR walks app config first, then publisher policy (unless bypassed by
`&lt;publisherPolicy apply="no"/&gt;`), then machine.config; later layers override
earlier ones, so the effective winner is machine.config &gt; publisher &gt; app &gt;
framework unification.

```csharp
public enum PolicyLayer
```

## Fields

### AppConfig

A redirect declared in the application's `*.exe.config`/`*.dll.config`.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
AppConfig = 3
```

### CodeBase

The effective identity was anchored by a `&lt;codeBase&gt;` element rather than a
version redirect — codeBase entries can come from any policy layer above.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
CodeBase = 4
```

### FrameworkUnification

The CLR's built-in unification of well-known framework public key tokens.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
FrameworkUnification = 0
```

### MachineConfig

A redirect declared in the architecture-correct
`%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319\Config\machine.config`.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
MachineConfig = 1
```

### PublisherPolicy

A redirect declared in a GAC-resident
`policy.&lt;major&gt;.&lt;minor&gt;.&lt;simpleName&gt;` publisher-policy assembly.

**Returns:** [PolicyLayer](/api/dotsider.core.analysis.models.policylayer/)

```csharp
PublisherPolicy = 2
```
