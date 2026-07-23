namespace EmbeddedSourceLib;

/// <summary>
/// Provides embedded source whose document name contains shell metacharacters.
/// </summary>
public static class HostileNameFixture
{
    /// <summary>
    /// Returns a stable value used to locate this method in embedded-source tests.
    /// </summary>
    /// <returns>The value 43.</returns>
    public static int Run() => 43;
}
