namespace Dotsider.DeployHost;

/// <summary>
/// Formats preflight sections and tracks pass, warning, and failure totals.
/// Human-readable output remains compatible with the existing deployment workflow.
/// A nonzero failure count determines the command exit code.
/// </summary>
internal sealed class PreflightReporter(TextWriter writer)
{
    internal int Passed { get; private set; }

    internal int Failed { get; private set; }

    internal int Warned { get; private set; }

    /// <summary>
    /// Starts a named group of related preflight checks.
    /// Sections are separated by one blank line for readable CI output.
    /// The method does not affect result totals.
    /// </summary>
    /// <param name="name">The section name.</param>
    internal void Section(string name)
    {
        writer.WriteLine();
        writer.WriteLine(name);
    }

    /// <summary>
    /// Records one successful preflight check.
    /// The message is written immediately for streaming CI diagnostics.
    /// Successful checks contribute to the final summary.
    /// </summary>
    /// <param name="message">The successful condition.</param>
    internal void Pass(string message)
    {
        Passed++;
        writer.WriteLine($"  PASS {message}");
    }

    /// <summary>
    /// Records one failed preflight requirement.
    /// Failures are reported without stopping the remaining checks.
    /// Any recorded failure makes the command return a nonzero exit code.
    /// </summary>
    /// <param name="message">The failed requirement and remediation.</param>
    internal void Fail(string message)
    {
        Failed++;
        writer.WriteLine($"  FAIL {message}");
    }

    /// <summary>
    /// Records one non-blocking preflight warning.
    /// Warnings remain visible while preserving a successful exit code.
    /// The final summary reports their count separately.
    /// </summary>
    /// <param name="message">The warning condition.</param>
    internal void Warn(string message)
    {
        Warned++;
        writer.WriteLine($"  WARN {message}");
    }

    /// <summary>
    /// Writes final pass, warning, and failure counts.
    /// The output makes the command result explicit in local and CI runs.
    /// No additional result state is changed.
    /// </summary>
    internal void Summary()
    {
        writer.WriteLine();
        writer.WriteLine($"Summary: {Passed} passed, {Warned} warnings, {Failed} failed");
    }
}
