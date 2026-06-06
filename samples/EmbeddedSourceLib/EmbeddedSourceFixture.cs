namespace EmbeddedSourceLib;

internal static class EmbeddedSourceFixture
{
    internal static int Compute(int value)
    {
        var doubled = value * 2;
        return doubled + 1;
    }
}
