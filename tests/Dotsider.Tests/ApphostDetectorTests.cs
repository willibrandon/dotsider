using System.Text;
using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class ApphostDetectorTests(SampleAssemblyFixture samples) : IDisposable
{
    private readonly List<string> _tempFiles = [];
    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_Apphost_ReturnsCompanionDllPath()
    {
        var result = ApphostDetector.FindCompanionDll(samples.HelloWorldExe);

        Assert.NotNull(result);
        Assert.EndsWith(".dll", result!, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result));
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_ManagedDll_ReturnsNull()
    {
        var result = ApphostDetector.FindCompanionDll(samples.HelloWorldDll);

        Assert.Null(result);
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_NativeAotExe_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var result = ApphostDetector.FindCompanionDll(samples.NativeAotConsoleExe!);

        Assert.Null(result);
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_NonExeExtension_ReturnsNull()
    {
        var result = ApphostDetector.FindCompanionDll(samples.RichLibraryDll);

        Assert.Null(result);
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_NonExistentFile_ReturnsNull()
    {
        var result = ApphostDetector.FindCompanionDll(
            Path.Combine(Path.GetTempPath(), "nonexistent-assembly.exe"));

        Assert.Null(result);
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_NativeExeWithDllNameButNoHostfxr_ReturnsNull()
    {
        // Simulate a native launcher that embeds the DLL name for its own reasons
        // but is not a .NET apphost (no hostfxr reference).
        var dir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var fakeDllName = "FakeLauncher.dll";
        var fakeExePath = Path.Combine(dir, "FakeLauncher.exe");
        var fakeDllPath = Path.Combine(dir, fakeDllName);

        // Write a fake .exe containing the DLL name but NOT hostfxr
        var exeContent = new byte[512];
        Encoding.UTF8.GetBytes(fakeDllName).CopyTo(exeContent, 64);
        File.WriteAllBytes(fakeExePath, exeContent);
        _tempFiles.Add(fakeExePath);

        // Copy a real managed .dll so the companion check would pass
        File.Copy(samples.HelloWorldDll, fakeDllPath);
        _tempFiles.Add(fakeDllPath);
        _tempFiles.Add(dir);

        var result = ApphostDetector.FindCompanionDll(fakeExePath);

        Assert.Null(result);
    }

    [Fact(Timeout = 30_000)]
    public void FindCompanionDll_DottedNameApphost_ReturnsCompanionDllPath()
    {
        var result = ApphostDetector.FindCompanionDll(samples.DottedNameAppExe);

        Assert.NotNull(result);
        Assert.EndsWith("Dotted.Name.App.dll", result!, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result));
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }
}
