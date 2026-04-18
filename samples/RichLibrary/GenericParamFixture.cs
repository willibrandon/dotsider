namespace RichLibrary;

/// <summary>
/// Generic-parameter fixture. Emits bare ELEMENT_TYPE_VAR / ELEMENT_TYPE_MVAR
/// TypeSpecs so navigation on them can be exercised.
/// </summary>
/// <typeparam name="TKey">Exercises ELEMENT_TYPE_VAR at index 0.</typeparam>
/// <typeparam name="TValue">Exercises ELEMENT_TYPE_VAR at index 1.</typeparam>
public class GenericParamFixture<TKey, TValue>
{
    /// <summary>Emits initobj !1 — zero-initializing a TValue local.</summary>
    public TValue DefaultValue()
    {
        TValue v = default!;
        return v;
    }

    /// <summary>Emits initobj !!0 — zero-initializing a method-level generic parameter.</summary>
    public T DefaultMethodParam<T>()
    {
        T v = default!;
        return v;
    }
}
