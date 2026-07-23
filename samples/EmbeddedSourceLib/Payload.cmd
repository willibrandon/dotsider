namespace EmbeddedSourceLib;

/// <summary>
/// Provides embedded source whose document carries a command-script extension.
/// </summary>
public static class HostileExtensionFixture
{
    /// <summary>
    /// Returns a stable value used to locate this method in embedded-source tests.
    /// </summary>
    /// <returns>The value 42.</returns>
    public static int Run() => 42;
}
