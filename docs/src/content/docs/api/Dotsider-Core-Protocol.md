---
title: "Dotsider.Core.Protocol"
slug: api/dotsider.core.protocol
sidebar:
  order: 4
---

## Classes

### [DotsiderJsonOptions](/api/dotsider.core.protocol.dotsiderjsonoptions/)

Shared JSON serialization options for the dotsider diagnostics protocol.

```csharp
public static class DotsiderJsonOptions
```

### [DotsiderProtocol](/api/dotsider.core.protocol.dotsiderprotocol/)

Constants for the dotsider diagnostics protocol.

```csharp
public static class DotsiderProtocol
```

### [DotsiderRequest](/api/dotsider.core.protocol.dotsiderrequest/)

JSON request sent to a dotsider diagnostics socket.

```csharp
public sealed class DotsiderRequest
```

### [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

JSON response from a dotsider diagnostics socket.

```csharp
public sealed class DotsiderResponse
```

### [FrameworkAssemblyInfo](/api/dotsider.core.protocol.frameworkassemblyinfo/)

Result of resolving an assembly from the system .NET shared framework.
Includes the full path and the runtime pack that provided it.

```csharp
public sealed record FrameworkAssemblyInfo : IEquatable<FrameworkAssemblyInfo>
```

### [ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/)

Serialization-safe representation of an assembly resolution result.
Used in protocol and MCP responses where [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)
cannot be serialized directly (FromBundle contains raw bytes).

```csharp
public sealed record ResolvedAssemblyInfo : IEquatable<ResolvedAssemblyInfo>
```

