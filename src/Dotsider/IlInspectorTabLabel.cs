namespace Dotsider;

/// <summary>
/// Chooses the user-facing label for tab 3. The internal tab id stays
/// <see cref="TabId.IlInspector"/> because navigation and keybindings are stable.
/// </summary>
internal static class IlInspectorTabLabel
{
    public const string IlInspector = "IL Inspector";
    public const string Disassembly = "Disassembly";
    public const string IlAndNative = "IL + Native";

    public static string For(DotsiderState state)
    {
        if (state.Analyzer.IsReadyToRun)
            return IlAndNative;

        if (state.Analyzer.PreIlcCompanions is not null)
            return state.IlAotTreeNativeView ? Disassembly : IlAndNative;

        return state.Analyzer.HasManagedMetadata ? IlInspector : Disassembly;
    }
}
