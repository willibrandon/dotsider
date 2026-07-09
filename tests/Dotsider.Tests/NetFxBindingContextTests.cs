using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NetFxBindingContext"/> covering the per-root build (TFM gating, app-base
/// detection, architecture decoding, machine.config selection), the layered binding policy
/// (precedence, document order, appliesTo, processorArchitecture, malformed-XML vs invalid-section
/// error split, framework unification), and helpers (<see cref="NetFxBindingContext.GacScanList"/>,
/// <see cref="NetFxBindingContext.FrameworkRuntimeDirectory"/>).
/// </summary>
[TestClass]
public sealed class NetFxBindingContextTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>The fixture-built sample exposes a populated context with all fields.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryBuild_NetFxRoot_PopulatesAllFields()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.AreEqual(Samples.NetFxBindingRedirectsExe, ctx!.EntryAssemblyPath);
        Assert.AreEqual(Path.GetDirectoryName(Samples.NetFxBindingRedirectsExe), ctx.AppBaseDirectory);
        Assert.IsNotNull(ctx.ConfigPath);
        Assert.StartsWith(".NETFramework,Version=v4", ctx.TargetFramework, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotEmpty(ctx.Policy.AppConfigRedirects);
        Assert.Contains(p => p == "lib", ctx.PrivatePaths);
    }

    /// <summary>A non-net48 root produces no binding context.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryBuild_NetCoreRoot_ReturnsNull()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNull(NetFxBindingContext.TryBuild(analyzer));
    }

    /// <summary>The sample EXE is AnyCPU IL-only — on a 64-bit host it should bind as Amd64.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EffectiveArchitecture_AnyCpuIlOnly_OnX64Host_IsAmd64()
    {
        SkipIfNotWindows();
        if (!Environment.Is64BitOperatingSystem) return;
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.AreEqual(NetFxArchitecture.Amd64, ctx!.EffectiveArchitecture);
    }

    /// <summary>GAC scan list for an Amd64 root is GAC_MSIL then GAC_64.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GacScanList_Amd64Root_IsMsilThen64()
    {
        SkipIfNotWindows();
        if (!Environment.Is64BitOperatingSystem) return;
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        var list = ctx!.GacScanList();
        Assert.IsNotEmpty(list);
        Assert.EndsWith("GAC_MSIL", list[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("GAC_64", list[1], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Framework runtime directory for an Amd64 root resolves to Framework64\v4.0.30319.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrameworkRuntimeDir_Amd64Root_IsFramework64()
    {
        SkipIfNotWindows();
        if (!Environment.Is64BitOperatingSystem) return;
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        var dir = ctx!.FrameworkRuntimeDirectory();
        Assert.IsNotNull(dir);
        Assert.Contains("Framework64", dir, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("v4.0.30319", dir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The sample's app config carries the expected Newtonsoft.Json redirect.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_ParsesAllRedirects()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.Contains(r => r.Name == "Newtonsoft.Json" && r.NewVersion == new Version(13, 0, 0, 0), ctx!.Policy.AppConfigRedirects);
    }

    /// <summary>An assemblyBinding with appliesTo="v2.0" is filtered out for net48 roots.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_HonorsAppliesToFilter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-applies-to-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1" appliesTo="v2.0.50727">
                      <dependentAssembly>
                        <assemblyIdentity name="OnlyV2" publicKeyToken="0000000000000001" culture="neutral" />
                        <bindingRedirect oldVersion="0.0.0.0-1.0.0.0" newVersion="1.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var redirects = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig).Redirects;
            Assert.IsEmpty(redirects);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Malformed XML at the document level yields an empty policy (does not throw).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_MalformedXml_ReturnsEmptyPolicy()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-malformed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath, "<configuration><runtime><not-closed");
            var parsed = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig);
            Assert.IsEmpty(parsed.Redirects);
            Assert.IsEmpty(parsed.CodeBases);
            Assert.IsEmpty(parsed.Disabled);
            Assert.IsEmpty(parsed.PrivatePaths);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>An invalid bindingRedirect entry is dropped but sibling entries still apply.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_InvalidSectionDroppedButRestApplied()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-invalid-section-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <dependentAssembly>
                        <assemblyIdentity name="Bad" publicKeyToken="0000000000000001" culture="neutral" />
                        <bindingRedirect oldVersion="not-a-version" newVersion="1.0.0.0" />
                      </dependentAssembly>
                      <dependentAssembly>
                        <assemblyIdentity name="Good" publicKeyToken="0000000000000002" culture="neutral" />
                        <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="1.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var redirects = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig).Redirects;
            Assert.ContainsSingle(redirects);
            Assert.AreEqual("Good", redirects[0].Name);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// First matching <c>&lt;dependentAssembly&gt;</c> in document order wins per CLR rules
    /// when two assemblyBinding blocks redirect the same identity to different versions.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_DocumentOrderAcrossMultipleAssemblyBindingBlocks_FirstMatchWins()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-doc-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <dependentAssembly>
                        <assemblyIdentity name="Same" publicKeyToken="0000000000000001" culture="neutral" />
                        <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="1.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <dependentAssembly>
                        <assemblyIdentity name="Same" publicKeyToken="0000000000000001" culture="neutral" />
                        <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="2.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var redirects = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig).Redirects;
            var policy = new BindingPolicy(
                AppConfigRedirects: redirects,
                PublisherPolicyRedirects: [],
                MachineConfigRedirects: [],
                FrameworkUnificationRedirects: [],
                CodeBases: [],
                PublisherPolicyDisabledFor: []);
            var requested = new AssemblyRefInfo("Same", "0.5.0.0", "neutral", "0000000000000001");
            var (effective, _) = policy.Apply(requested, NetFxArchitecture.Amd64);
            Assert.AreEqual("1.0.0.0", effective.Version);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>processorArchitecture filter excludes non-matching entries.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_AppConfig_ProcessorArchitectureFilter_ExcludesNonMatchingEntries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-arch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <dependentAssembly>
                        <assemblyIdentity name="X86Only" publicKeyToken="0000000000000001" culture="neutral" processorArchitecture="x86" />
                        <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="1.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var redirects = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig).Redirects;
            var policy = new BindingPolicy(
                redirects, [], [], [], [], []);
            var requested = new AssemblyRefInfo("X86Only", "0.5.0.0", "neutral", "0000000000000001");
            var (eAmd64, appliedAmd64) = policy.Apply(requested, NetFxArchitecture.Amd64);
            Assert.IsNull(appliedAmd64);
            Assert.AreEqual(requested.Version, eAmd64.Version);
            var (eX86, appliedX86) = policy.Apply(requested, NetFxArchitecture.X86);
            Assert.IsNotNull(appliedX86);
            Assert.AreEqual("1.0.0.0", eX86.Version);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Framework unification covers the well-known framework PKTs even with no app config.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_FrameworkUnification_CoversWellKnownFrameworkPkts()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        // mscorlib carries PKT b77a5c561934e089 — request 2.0.0.0 should unify to 4.0.0.0.
        var requested = new AssemblyRefInfo("mscorlib", "2.0.0.0", "neutral", "b77a5c561934e089");
        var (effective, applied) = ctx!.Policy.Apply(requested, NetFxArchitecture.Amd64);
        Assert.IsNotNull(applied);
        Assert.AreEqual("4.0.0.0", effective.Version);
        Assert.AreEqual(PolicyLayer.FrameworkUnification, applied!.Source);
    }

    /// <summary>
    /// privatePath segments declared inside an &lt;assemblyBinding appliesTo="v2.0…"&gt; block
    /// must not bleed into a net4 root's probe list.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrivatePaths_HonorAppliesToFilter_DropNonV4Blocks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-privatepath-applies-to-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1" appliesTo="v2.0.50727">
                      <probing privatePath="legacy" />
                    </assemblyBinding>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <probing privatePath="lib" />
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var parsed = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig);
            Assert.DoesNotContain("legacy", parsed.PrivatePaths);
            Assert.Contains("lib", parsed.PrivatePaths);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Privatepath entries are read from app.config.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrivatePaths_ParsedAndRootedAtAppBase()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.Contains(p => p == "lib", ctx!.PrivatePaths);
    }

    /// <summary>
    /// Layered policy chains: app config rewrites 1.0 → 2.0, machine.config covers 2.0 → 3.0.
    /// The binder must apply both, ending at 3.0.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_ChainsLayersSequentially_AppThenMachineCoversIntermediate()
    {
        var app = new BindingRedirect(
            PolicyLayer.AppConfig,
            "Chain", "0000000000000001", "neutral", null,
            new Version(1, 0, 0, 0), new Version(1, 0, 0, 0), new Version(2, 0, 0, 0));
        var machine = new BindingRedirect(
            PolicyLayer.MachineConfig,
            "Chain", "0000000000000001", "neutral", null,
            new Version(2, 0, 0, 0), new Version(2, 0, 0, 0), new Version(3, 0, 0, 0));
        var policy = new BindingPolicy(
            AppConfigRedirects: [app],
            PublisherPolicyRedirects: [],
            MachineConfigRedirects: [machine],
            FrameworkUnificationRedirects: [],
            CodeBases: [],
            PublisherPolicyDisabledFor: []);

        var requested = new AssemblyRefInfo("Chain", "1.0.0.0", "neutral", "0000000000000001");
        var (effective, applied) = policy.Apply(requested, NetFxArchitecture.Amd64);
        Assert.AreEqual("3.0.0.0", effective.Version);
        Assert.IsNotNull(applied);
        Assert.AreEqual(PolicyLayer.MachineConfig, applied!.Source);
        Assert.AreEqual(new Version(1, 0, 0, 0), applied.RequestedVersion);
        Assert.AreEqual(new Version(3, 0, 0, 0), applied.BoundVersion);
    }

    /// <summary>
    /// Runtime-scoped &lt;publisherPolicy apply="no"/&gt; suppresses publisher policy for every
    /// bind in the AppDomain — including identities that have no &lt;dependentAssembly&gt;.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Policy_RuntimeScopedPublisherPolicyApplyNo_SuppressesGloballyForAllIdentities()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-policy-runtime-disable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var configPath = Path.Combine(dir, "fake.exe.config");
            File.WriteAllText(configPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <runtime>
                    <publisherPolicy apply="no" />
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <dependentAssembly>
                        <assemblyIdentity name="Other" publicKeyToken="0000000000000099" culture="neutral" />
                        <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="1.0.0.0" />
                      </dependentAssembly>
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);
            var parsed = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig);
            Assert.IsTrue(parsed.PublisherPolicyDisabledGlobally);

            var pub = new BindingRedirect(
                PolicyLayer.PublisherPolicy,
                "Untouched", "0000000000000077", "neutral", null,
                new Version(0, 0, 0, 0), new Version(9, 9, 9, 9), new Version(5, 0, 0, 0));
            var policy = new BindingPolicy(
                AppConfigRedirects: parsed.Redirects,
                PublisherPolicyRedirects: [pub],
                MachineConfigRedirects: [],
                FrameworkUnificationRedirects: [],
                CodeBases: [],
                PublisherPolicyDisabledFor: parsed.Disabled,
                PublisherPolicyDisabledGlobally: parsed.PublisherPolicyDisabledGlobally);

            var requested = new AssemblyRefInfo("Untouched", "1.0.0.0", "neutral", "0000000000000077");
            var (effective, applied) = policy.Apply(requested, NetFxArchitecture.Amd64);
            Assert.AreEqual(requested.Version, effective.Version);
            Assert.IsNull(applied);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>CodeBase entries are read from app.config.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CodeBases_ParsedFromConfig()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.Contains(c => c.Name == "NetFxBindingRedirects.CodeBaseLib"
              && c.Version == new Version(2, 0, 0, 0)
              && c.Href.EndsWith("NetFxBindingRedirects.CodeBaseLib.dll", StringComparison.OrdinalIgnoreCase), ctx!.Policy.CodeBases);
        Assert.Contains(c => c.Name == "NetFxBindingRedirects.MissingCodeBase"
              && c.Href.EndsWith("Missing.dll", StringComparison.OrdinalIgnoreCase), ctx.Policy.CodeBases);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Test requires Windows (.NET Framework binder).");
    }
}
