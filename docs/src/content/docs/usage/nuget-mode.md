---
title: NuGet Mode
description: Browse NuGet package contents and inspect any DLL inside.
---

![NuGet Mode](/screenshots/nuget-mode.png)

Open any `.nupkg` file directly:

```
dotsider package.nupkg
```

dotsider lists the package contents — the `.nuspec` manifest, lib folders per target framework, and any other files. Select a DLL and press `Enter` to open it in the full analyzer with all 8 tabs.

NuGet packages are ZIP files. dotsider extracts assemblies to a temp directory for analysis, so everything works exactly as it does with regular DLLs.
