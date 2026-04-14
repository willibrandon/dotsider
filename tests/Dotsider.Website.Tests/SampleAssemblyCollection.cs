namespace Dotsider.Website.Tests;

/// <summary>
/// xUnit collection binding that shares a single SampleAssemblyFixture across website test classes.
/// </summary>
[CollectionDefinition("SampleAssemblies")]
public class SampleAssemblyCollection : ICollectionFixture<SampleAssemblyFixture>;
