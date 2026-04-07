namespace SelfContainedConsole;

/// <summary>
/// Fixture exercising external type references for bundle resolution testing.
/// Each method produces a known external assembly reference that tests can target.
/// </summary>
public class BundleResolutionFixture
{
    private int _counter;

    /// <summary>Calls Console.WriteLine (System.Console reference).</summary>
    public void CallConsoleWriteLine() { _counter++; Console.WriteLine("test"); }

    /// <summary>Uses List&lt;int&gt; (System.Collections.Generic reference).</summary>
    public List<int> UseGenericList() { _counter++; return [1, 2, 3]; }

    /// <summary>Reads Environment.ProcessPath (System.Runtime reference).</summary>
    public string? ReadProcessPath() { _counter++; return Environment.ProcessPath; }
}
