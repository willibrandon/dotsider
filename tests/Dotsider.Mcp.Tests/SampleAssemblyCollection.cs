namespace Dotsider.Mcp.Tests;

/// <summary>
/// Collection definition that binds <see cref="SampleAssemblyFixture"/> to tests opting into the shared fixture.
/// </summary>
[CollectionDefinition("SampleAssemblies")]
public class SampleAssemblyCollection : ICollectionFixture<SampleAssemblyFixture>;
