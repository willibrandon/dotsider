# ComplexApp

Console app with embedded resources and a generic pipeline architecture. Used to test dotsider's resource extraction, versioned assembly metadata, and analysis of generic type patterns.

- Embedded resources: `config.json`, `banner.txt`
- Generic `ProcessingPipeline<T>` with composable `IPipelineStep<T>` steps
- Explicit `AssemblyVersion` (1.0.0.0) for metadata testing
