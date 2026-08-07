using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Verifies resolution of a compiler-built multi-module assembly.
/// </summary>
[TestClass]
public sealed class MultiModuleResolutionTests
{
    /// <summary>
    /// Verifies that a real AssemblyFile export resolves to an authenticated module snapshot
    /// and that the application state exposes the module's types, methods, and fields.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Resolve_CompilerBuiltModule_PushesCompleteAnalyzer()
    {
        var projectDirectory = Path.Combine(
            TestHelpers.GetRepoRoot(),
            "samples",
            "MultiModuleManifest");
        var outputDirectory = TestProcessEnvironment.GetProjectOutputDirectory(
            projectDirectory,
            TestProcessEnvironment.CurrentBuildConfiguration,
            "net10.0");
        var manifestPath = Path.Combine(outputDirectory, "MultiModuleManifest.dll");
        var modulePath = Path.Combine(outputDirectory, "MultiModulePart.netmodule");

        Assert.IsTrue(File.Exists(manifestPath), $"Missing compiler-built manifest: {manifestPath}");
        Assert.IsTrue(File.Exists(modulePath), $"Missing compiler-built module: {modulePath}");

        using (var stream = File.OpenRead(manifestPath))
        using (var peReader = new PEReader(stream))
        {
            var reader = peReader.GetMetadataReader();
            var assembly = reader.GetAssemblyDefinition();
            var fileHandle = Assert.ContainsSingle(reader.AssemblyFiles);
            var file = reader.GetAssemblyFile(fileHandle);

            Assert.AreEqual(AssemblyHashAlgorithm.Sha1, assembly.HashAlgorithm);
            Assert.IsTrue(file.ContainsMetadata);
            Assert.AreEqual("MultiModulePart.netmodule", reader.GetString(file.Name));
            Assert.HasCount(20, reader.GetBlobBytes(file.HashValue));
        }

        ImplementationAssemblyResolver.ClearCache();
        try
        {
            using var manifestAnalyzer = new AssemblyAnalyzer(manifestPath);
            var resolved = ImplementationAssemblyResolver.Resolve(
                manifestPath,
                manifestAnalyzer.AssemblyName!,
                "MultiModuleFixture.ModuleOwnedType",
                manifestAnalyzer.TargetFramework,
                manifestAnalyzer.PreferredRuntimePack);
            var module = Assert.IsExactInstanceOfType<ResolvedModule>(resolved);

            Assert.AreEqual(Path.GetFullPath(modulePath), Path.GetFullPath(module.Path));
            Assert.AreEqual(Path.GetFullPath(manifestPath), module.ManifestPath);
            Assert.AreEqual(manifestAnalyzer.TargetFramework, module.TargetFramework);
            Assert.AreEqual(manifestAnalyzer.PreferredRuntimePack, module.PreferredRuntimePack);
            Assert.AreSequenceEqual(File.ReadAllBytes(modulePath), module.Bytes);

            using var workload = new Hex1bAppWorkloadAdapter();
            using var app = new Hex1bApp(
                _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
                new Hex1bAppOptions { WorkloadAdapter = workload });
            using var state = new DotsiderState(app, manifestPath);

            Assert.IsTrue(state.PushAssembly(module));
            var type = Assert.ContainsSingle(state.Analyzer.TypeDefs.Where(candidate =>
                candidate.FullName == "MultiModuleFixture.ModuleOwnedType"));
            Assert.ContainsSingle(state.Analyzer.MethodDefs.Where(candidate =>
                candidate.DeclaringType == type.FullName && candidate.Name == "Add"));
            Assert.ContainsSingle(state.Analyzer.FieldDefs.Where(candidate =>
                candidate.DeclaringType == type.FullName && candidate.Name == "Kind"));
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
        }
    }
}
