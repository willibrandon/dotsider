---
title: "TypeRefInfo"
description: "Information about a referenced type from the TypeRef metadata table."
slug: api/dotsider.core.analysis.models.typerefinfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a referenced type from the TypeRef metadata table.

```csharp
public sealed record TypeRefInfo : IEquatable<TypeRefInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TypeRefInfo**

## Implements

- [IEquatable\<TypeRefInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TypeRefInfo(int, string, string, string, string, string)

Information about a referenced type from the TypeRef metadata table.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token for this type reference.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace of the referenced type.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the referenced type.
- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified name (Namespace.Name).
- `ResolutionScope` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The scope in which the type is defined, rendered as a human-readable string — the
referenced assembly's simple name, the enclosing type's full name, or the scope kind
for module and module-reference scopes.
- `ResolutionScopeId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The full-identity identifier of the referenced assembly, when the resolution scope ultimately
derives from an `AssemblyReference`. For TypeRefs whose scope is another TypeRef
(nested-type scopes) this carries the enclosing type's resolution-scope id by walking the
nested chain to its root. Empty for module or module-reference scopes, where no referenced
assembly is involved. Used by the dependency-graph builder to group TypeRefs by full
identity so per-edge counts are correct even when two references share a simple name.

```csharp
public TypeRefInfo(int Token, string Namespace, string Name, string FullName, string ResolutionScope, string ResolutionScopeId)
```

## Properties

### FullName

The fully qualified name (Namespace.Name).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullName { get; init; }
```

### Name

The simple name of the referenced type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

The namespace of the referenced type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### ResolutionScope

The scope in which the type is defined, rendered as a human-readable string — the
referenced assembly's simple name, the enclosing type's full name, or the scope kind
for module and module-reference scopes.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ResolutionScope { get; init; }
```

### ResolutionScopeId

The full-identity identifier of the referenced assembly, when the resolution scope ultimately
derives from an `AssemblyReference`. For TypeRefs whose scope is another TypeRef
(nested-type scopes) this carries the enclosing type's resolution-scope id by walking the
nested chain to its root. Empty for module or module-reference scopes, where no referenced
assembly is involved. Used by the dependency-graph builder to group TypeRefs by full
identity so per-edge counts are correct even when two references share a simple name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ResolutionScopeId { get; init; }
```

### Token

The metadata token for this type reference.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

