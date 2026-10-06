using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Dotsider.Website.Tests;

/// <summary>Checks that native console assets survive single-file publishing intact.</summary>
[TestClass]
public sealed class NativePublishLayoutTests
{
    /// <summary>
    /// Hex1b's x64 Windows payload also includes the ARM64 console host.
    /// Both must remain loose in their own directories instead of colliding or bundling.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PublishedWebsite_PreservesNativeConsoleArchitectures()
    {
        string output = SampleAssemblyHost.Instance.WebsitePublishedDir;
        Assert.IsFalse(File.Exists(Path.Join(output, "OpenConsole.exe")),
            "Console hosts must retain their architecture directories.");
        Assert.IsTrue(File.Exists(Path.Join(output, "conpty.dll")));
        AssertConsoleMachine(output, "arm64", Machine.Arm64);
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            AssertConsoleMachine(output, "x64", Machine.Amd64);
    }

    private static void AssertConsoleMachine(string output, string architecture, Machine expected)
    {
        string path = Path.Join(output, architecture, "OpenConsole.exe");
        Assert.IsTrue(File.Exists(path), $"Native console host not published: {path}");
        using FileStream stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        Assert.AreEqual(expected, reader.PEHeaders.CoffHeader.Machine);
    }
}
