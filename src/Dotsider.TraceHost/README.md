# Dotsider Trace Host

Dotsider Trace Host is the framework-dependent EventPipe worker used by the Dynamic tab. It
keeps `Microsoft.Diagnostics.Tracing.TraceEvent` outside the Native AOT-compiled dotsider process
while preserving runtime tracing.

`DynamicAnalysisView` creates a `RuntimeTracer`, and `RuntimeTracer.Start()` launches:

```
dotnet exec tracehost/dotsider-tracehost.dll <assembly-path> [arguments]
```

The host accepts an existing managed DLL or platform executable. DLLs run through the absolute
path of the same `dotnet` host that started TraceHost. Windows direct launches are limited to
`.exe` files, and Unix direct launches must have an executable bit. Arguments always use
`ProcessStartInfo.ArgumentList` with shell execution disabled.

The host starts the target under a suspended reverse diagnostic port, removes inherited switches
that could disable diagnostics, attaches EventPipe before managed entry-point execution, and then
resumes it. Unix sockets live in a mode-0700 directory under the user's temporary directory and
use a compact name that fits platform socket-path limits.

Events, counters, process output, and lifecycle status are written to standard output as
newline-delimited JSON. Output retains the latest 5,000 lines; an individual line is limited to
64 KiB and receives a truncation marker when necessary. Dotsider sends `stop` over standard input
when tracing is stopped. Unknown control messages are ignored, input remains monitored until stop
or EOF, and abort paths terminate the target process tree.

The project targets .NET 10, rolls forward to later major runtimes, and intentionally remains
framework-dependent. Native AOT publish and tool packaging place its deployment files in the
`tracehost` directory beside dotsider. The directory must remain intact; symbols, XML
documentation, and an apphost are not included in the distributed payload.

Build the project directly with:

```
dotnet build src/Dotsider.TraceHost/Dotsider.TraceHost.csproj
```

Building or publishing `src/Dotsider/Dotsider.csproj` also publishes and copies the host through `build/Dotsider.TraceHost.targets`.
