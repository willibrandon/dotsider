namespace Dotsider.Diagnostics;

internal enum BoundedUtf8LineReadStatus
{
    Success,
    EndOfStream,
    InvalidUtf8,
    TooLarge
}
