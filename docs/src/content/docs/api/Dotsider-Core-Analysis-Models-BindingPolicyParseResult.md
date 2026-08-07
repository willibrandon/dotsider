---
title: "BindingPolicyParseResult"
description: "Output of NetFxRuntimeVersion): the redirects, codeBase entries, per-identity publisher-policy disablements, probing privatePath segments, and the runtime-scoped publisher-policy bypass flag found in a single configuration file."
slug: api/dotsider.core.analysis.models.bindingpolicyparseresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Output of NetFxRuntimeVersion): the redirects, codeBase entries,
per-identity publisher-policy disablements, probing privatePath segments, and the
runtime-scoped publisher-policy bypass flag found in a single configuration file.

```csharp
public sealed record BindingPolicyParseResult : IEquatable<BindingPolicyParseResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **BindingPolicyParseResult**

## Implements

- [IEquatable\<BindingPolicyParseResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BindingPolicyParseResult(IReadOnlyList\<BindingRedirect\>, IReadOnlyList\<CodeBaseEntry\>, IReadOnlyCollection\<(string Name, string? PublicKeyToken, string Culture)\>, IReadOnlyList\<string\>, bool)

Output of NetFxRuntimeVersion): the redirects, codeBase entries,
per-identity publisher-policy disablements, probing privatePath segments, and the
runtime-scoped publisher-policy bypass flag found in a single configuration file.

**Parameters:**

- `Redirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): All `&lt;bindingRedirect&gt;` entries parsed from the file.
- `CodeBases` ([IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): All `&lt;codeBase&gt;` entries parsed from the file.
- `Disabled` ([IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3)): Identities whose `&lt;dependentAssembly&gt;` block carried a
`&lt;publisherPolicy apply="no"/&gt;` child.
- `PrivatePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): All `&lt;probing privatePath="..."/&gt;` segments.
- `PublisherPolicyDisabledGlobally` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): true when the file's `&lt;runtime&gt;` element carried a top-level
`&lt;publisherPolicy apply="no"/&gt;`, suppressing publisher policy for every bind in
the AppDomain regardless of `&lt;dependentAssembly&gt;`.

```csharp
public BindingPolicyParseResult(IReadOnlyList<BindingRedirect> Redirects, IReadOnlyList<CodeBaseEntry> CodeBases, IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> Disabled, IReadOnlyList<string> PrivatePaths, bool PublisherPolicyDisabledGlobally)
```

## Properties

### CodeBases

All `&lt;codeBase&gt;` entries parsed from the file.

**Returns:** [IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CodeBaseEntry> CodeBases { get; init; }
```

### Disabled

Identities whose `&lt;dependentAssembly&gt;` block carried a
`&lt;publisherPolicy apply="no"/&gt;` child.

**Returns:** [IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3)

```csharp
public IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> Disabled { get; init; }
```

### PrivatePaths

All `&lt;probing privatePath="..."/&gt;` segments.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> PrivatePaths { get; init; }
```

### PublisherPolicyDisabledGlobally

true when the file's `&lt;runtime&gt;` element carried a top-level
`&lt;publisherPolicy apply="no"/&gt;`, suppressing publisher policy for every bind in
the AppDomain regardless of `&lt;dependentAssembly&gt;`.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool PublisherPolicyDisabledGlobally { get; init; }
```

### Redirects

All `&lt;bindingRedirect&gt;` entries parsed from the file.

**Returns:** [IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<BindingRedirect> Redirects { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<BindingRedirect\>, out IReadOnlyList\<CodeBaseEntry\>, out IReadOnlyCollection\<(string Name, string? PublicKeyToken, string Culture)\>, out IReadOnlyList\<string\>, out bool)

**Parameters:**

- `Redirects` ([IReadOnlyList\<BindingRedirect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `CodeBases` ([IReadOnlyList\<CodeBaseEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Disabled` ([IReadOnlyCollection\<String, String, String\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-3))
- `PrivatePaths` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `PublisherPolicyDisabledGlobally` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out IReadOnlyList<BindingRedirect> Redirects, out IReadOnlyList<CodeBaseEntry> CodeBases, out IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> Disabled, out IReadOnlyList<string> PrivatePaths, out bool PublisherPolicyDisabledGlobally)
```

### Equals(BindingPolicyParseResult?)

**Parameters:**

- `other` ([BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(BindingPolicyParseResult? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(BindingPolicyParseResult?, BindingPolicyParseResult?)

**Parameters:**

- `left` ([BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/))
- `right` ([BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(BindingPolicyParseResult? left, BindingPolicyParseResult? right)
```

### operator ==(BindingPolicyParseResult?, BindingPolicyParseResult?)

**Parameters:**

- `left` ([BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/))
- `right` ([BindingPolicyParseResult](/api/dotsider.core.analysis.models.bindingpolicyparseresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(BindingPolicyParseResult? left, BindingPolicyParseResult? right)
```
