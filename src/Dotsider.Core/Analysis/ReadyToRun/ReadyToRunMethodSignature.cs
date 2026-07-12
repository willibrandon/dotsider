namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Describes the result of walking one ReadyToRun method signature.
/// </summary>
internal readonly record struct ReadyToRunMethodSignature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReadyToRunMethodSignature"/> structure.
    /// </summary>
    /// <param name="offset">The file offset immediately after the signature.</param>
    /// <param name="methodToken">The recovered method token, or zero when unavailable.</param>
    /// <param name="instantiationDisplay">The rendered method instantiation, or null.</param>
    /// <param name="crossModule">Whether the signature overrides the module context.</param>
    /// <param name="moduleIndex">The module override index, or -1 when no override exists.</param>
    public ReadyToRunMethodSignature(
        int offset,
        int methodToken,
        string? instantiationDisplay,
        bool crossModule,
        int moduleIndex)
    {
        Offset = offset;
        MethodToken = methodToken;
        InstantiationDisplay = instantiationDisplay;
        CrossModule = crossModule;
        ModuleIndex = moduleIndex;
    }

    /// <summary>
    /// Gets the file offset immediately after the signature, where the runtime-function index begins.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Gets the recovered MethodDef or MemberRef token, or zero when unavailable.
    /// </summary>
    public int MethodToken { get; }

    /// <summary>
    /// Gets a rendered method instantiation such as <c>&lt;int&gt;</c>, or null.
    /// </summary>
    public string? InstantiationDisplay { get; }

    /// <summary>
    /// Gets a value indicating whether the signature overrides the module context.
    /// </summary>
    public bool CrossModule { get; }

    /// <summary>
    /// Gets the module override index, or -1 when no override exists.
    /// </summary>
    public int ModuleIndex { get; }
}
