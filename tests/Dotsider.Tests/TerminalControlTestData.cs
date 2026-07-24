namespace Dotsider.Tests;

/// <summary>
/// Shared hostile metadata values and their terminal-safe display projections.
/// </summary>
internal static class TerminalControlTestData
{
    /// <summary>The compiler-emitted user string stored in TerminalControlLib.</summary>
    internal const string CompilerPayload =
        "terminal-prefix\u001B]52;c;cHduZWQ=\u0007-\u001B[31mred\u001B[0m"
        + "\u007F\u0085\u009B\u202E\u2066\u2028\u2029-terminal-suffix";

    /// <summary>The visible projection of <see cref="CompilerPayload"/>.</summary>
    internal const string VisibleCompilerPayload =
        "terminal-prefix␛]52;c;cHduZWQ=␇-␛[31mred␛[0m"
        + "␡\\u0085\\u009B\\u202E\\u2066\\u2028\\u2029-terminal-suffix";

    /// <summary>A TypeDef name containing OSC title text.</summary>
    internal const string TypeName = "Hostile\u001B]0;type\u0007";

    /// <summary>The visible projection of <see cref="TypeName"/>.</summary>
    internal const string VisibleTypeName = "Hostile␛]0;type␇";

    /// <summary>A MethodDef name containing an eight-bit CSI control.</summary>
    internal const string MethodName = "Method\u009B31m";

    /// <summary>The visible projection of <see cref="MethodName"/>.</summary>
    internal const string VisibleMethodName = "Method\\u009B31m";

    /// <summary>A FieldDef name containing a bidirectional override.</summary>
    internal const string FieldName = "Field\u202Ehidden";

    /// <summary>The visible projection of <see cref="FieldName"/>.</summary>
    internal const string VisibleFieldName = "Field\\u202Ehidden";

    /// <summary>A synthetic user string containing an OSC clipboard payload.</summary>
    internal const string UserString = "User\u001B]52;c;cHduZWQ=\u0007";

    /// <summary>The visible projection of <see cref="UserString"/>.</summary>
    internal const string VisibleUserString = "User␛]52;c;cHduZWQ=␇";
}
