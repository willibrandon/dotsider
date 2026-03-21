# NativeAotConsole

NativeAOT-published console app used to test that dotsider's Dynamic tab no longer unconditionally blocks NativeAOT binaries.

- Published with `PublishAot=true` producing a self-contained native executable
- Since .NET 8, NativeAOT supports EventPipe when `EventSourceSupport=true`
- The Dynamic tab should allow tracing attempts rather than blocking with an incorrect message
- Referenced by `SampleAssemblyFixture` in the test suite
