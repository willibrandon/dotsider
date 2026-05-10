namespace AppLocalRollForward;

/// <summary>
/// Anchors a single TypeRef into Microsoft.Diagnostics.Tracing.TraceEvent so the C# compiler
/// emits the AssemblyRef row that drives the roll-forward scenario this sample exists to test.
/// </summary>
public static class Marker
{
    /// <summary>
    /// Forces a TypeRef into a TraceEvent type so the C# compiler keeps the
    /// Microsoft.Diagnostics.Tracing.TraceEvent AssemblyRef row in the emitted PE.
    /// </summary>
    /// <returns>The TraceEvent type's full name.</returns>
    public static string GetTraceEventTypeName()
        => typeof(Microsoft.Diagnostics.Tracing.ActivityComputer).FullName!;
}
