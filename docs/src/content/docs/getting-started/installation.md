---
title: Installation
description: How to install dotsider on your system.
---

## dotnet tool (recommended)

```
dotnet tool install -g dotsider
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

On Windows, Linux, and macOS, the .NET SDK selects a self-contained Native AOT tool package for the current operating system and architecture. The install command stays the same on every platform. Other supported environments use the `any` package and require the .NET 10 runtime when dotsider runs.

| Operating system | Architectures | Package runtime identifiers |
|---|---|---|
| Windows | x64, Arm64 | `win-x64`, `win-arm64` |
| Linux (glibc) | x64, Arm64 | `linux-x64`, `linux-arm64` |
| Linux (musl) | x64, Arm64 | `linux-musl-x64`, `linux-musl-arm64` |
| macOS | x64, Arm64 | `osx-x64`, `osx-arm64` |

`dotnet tool` users already meet the Dynamic tab's runtime requirement. See [Dynamic](/usage/dynamic/) for details.

## Homebrew (macOS / Linux)

```
brew install willibrandon/tap/dotsider
```

## WinGet (Windows)

```
winget install willibrandon.dotsider
```

## Scoop (Windows)

```
scoop bucket add dotsider https://github.com/willibrandon/scoop-bucket
scoop install dotsider
```

## Download binary

Grab a Native AOT archive from [Releases](https://github.com/willibrandon/dotsider/releases). Static analysis does not need the .NET SDK or runtime. Live tracing requires a .NET 10-or-later runtime for the included `tracehost` helper; keep the archive's `tracehost` directory beside the executable. Native symbols are available as separate archives for diagnostics.

## CI marketplaces

NativeAOT size gates do not require a separate Dotsider installation. Use
`willibrandon/dotsider@v0` in GitHub Actions or install the public **Dotsider** Azure DevOps
extension and use `DotsiderSizeCheck@1`. Both acquire a matching release, verify its checksum,
and cache it. See [CI integrations](/reference/ci-integrations/).
