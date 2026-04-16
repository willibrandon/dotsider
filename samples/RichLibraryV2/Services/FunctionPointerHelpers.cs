namespace RichLibrary.Services;

/// <summary>
/// Helpers that exercise function-pointer IL patterns for diff testing.
/// </summary>
public static class FunctionPointerHelpers
{
    /// <summary>Invokes a function pointer with unmanaged Cdecl calling convention.</summary>
    public static unsafe int InvokeCallback(nint fp, int x)
    {
        return ((delegate* unmanaged[Cdecl]<int, int>)fp)(x);
    }

    /// <summary>Checks whether an unmanaged Cdecl function pointer is non-null.</summary>
    public static unsafe bool HasCallback(nint fp)
    {
        delegate* unmanaged[Cdecl]<int, int> callback = (delegate* unmanaged[Cdecl]<int, int>)fp;
        return callback != null;
    }
}
