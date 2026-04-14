namespace Dotsider.Tests;

/// <summary>
/// xUnit collection binding test classes to the shared <see cref="SampleAssemblyFixture"/>.
/// </summary>
[CollectionDefinition("SampleAssemblies")]
public class SampleAssemblyCollection : ICollectionFixture<SampleAssemblyFixture>;
