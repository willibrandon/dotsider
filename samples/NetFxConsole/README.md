# NetFxConsole

.NET Framework 4.8 console app used to test dotsider's Dynamic tab guard for non-CoreCLR assemblies.

- Targets `net48` — EventPipe is not available on .NET Framework
- The Dynamic tab should detect the framework moniker and show a friendly message instead of hanging
- Referenced by `SampleAssemblyFixture` in the test suite
