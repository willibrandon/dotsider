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

### [NativeAotPayloadBuilder](/api/dotsider.core.protocol.nativeaotpayloadbuilder/)

Builds JSON-ready Native AOT payloads shared by direct MCP tools and the diagnostics session
protocol, so the two transports return the same facts and error semantics.

```csharp
public static class NativeAotPayloadBuilder
```

### [ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/)

Serialization-safe representation of an assembly resolution result.
Used in protocol and MCP responses where [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)
cannot be serialized directly (FromBundle contains raw bytes).

```csharp
public sealed record ResolvedAssemblyInfo : IEquatable<ResolvedAssemblyInfo>
```

### [SizeDiffPayloadBuilder](/api/dotsider.core.protocol.sizediffpayloadbuilder/)

Builds the serializable payloads the `diff-size` and `check-size-budgets`
surfaces return. The MCP server's direct mode and the running-session protocol handler
both call these, so the two transports cannot drift apart in shape or semantics.

```csharp
public static class SizeDiffPayloadBuilder
```

### [WasmPayloadBuilder](/api/dotsider.core.protocol.wasmpayloadbuilder/)

Builds JSON-ready WebAssembly payloads shared by direct MCP tools, the CLI, and the
diagnostics session protocol. The payloads describe raw SDK-produced Wasm modules such as
`dotnet.native.wasm`, not ECMA-335 metadata assemblies.

```csharp
public static class WasmPayloadBuilder
```

### [WebcilPayloadBuilder](/api/dotsider.core.protocol.webcilpayloadbuilder/)

Builds JSON-ready Webcil payloads shared by CLI, MCP, and diagnostics session output.
Webcil is a managed assembly container used in browser-wasm publishes, so the payload is
provenance beside the normal metadata/IL facts rather than a separate native module view.

```csharp
public static class WebcilPayloadBuilder
```

