namespace RichLibrary.Services;

/// <summary>
/// Helpers that exercise function-pointer IL patterns for diff testing.
/// </summary>
public static class FunctionPointerHelpers
{
    /// <summary>Invokes a function pointer with managed calling convention.</summary>
    public static unsafe int InvokeCallback(nint fp, int x)
    {
        return ((delegate* managed<int, int>)fp)(x);
    }

    /// <summary>Checks whether a managed function pointer is non-null.</summary>
    public static unsafe bool HasCallback(nint fp)
    {
        delegate* managed<int, int> callback = (delegate* managed<int, int>)fp;
        return callback != null;
    }
}
