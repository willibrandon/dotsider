namespace Dotsider.Diagnostics;

internal readonly struct BoundedUtf8LineReadResult
{
    internal BoundedUtf8LineReadResult(
        BoundedUtf8LineReadStatus status,
        string? value = null)
    {
        Status = status;
        Value = value;
    }

    internal BoundedUtf8LineReadStatus Status { get; }

    internal string? Value { get; }
}
