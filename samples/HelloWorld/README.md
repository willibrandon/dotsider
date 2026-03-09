# HelloWorld

Minimal console app used as the primary test fixture for basic assembly analysis and runtime tracing.

- Prints a greeting and triggers GC allocations and exception handling for trace event testing
- Contains overloaded `Formatter.Format` methods (int and string) to test method disambiguation
- Referenced by `SampleAssemblyFixture` in the test suite
