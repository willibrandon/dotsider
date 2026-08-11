# Compatibility

`DotsiderSizeCheck@1` requires Azure Pipelines agent 3.220.0 or newer. It provides Node 24 and Node 20 handlers so current and older supported agents can run the same task implementation.

| Agent operating system | Architectures | Dotsider release |
| --- | --- | --- |
| Windows | x64, ARM64 | `win-x64` or `win-arm64` |
| Linux using glibc | x64, ARM64 | `linux-x64` or `linux-arm64` |
| Linux using musl | x64, ARM64 | `linux-musl-x64` or `linux-musl-arm64` |
| macOS | x64, ARM64 | `osx-x64` or `osx-arm64` |

Microsoft-hosted and self-hosted agents are supported when their operating system and architecture have a matching Dotsider release. `dotsiderPath` supports custom and offline installations.
