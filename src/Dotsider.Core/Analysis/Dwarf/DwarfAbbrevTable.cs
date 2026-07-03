namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// One compilation unit's abbreviation table from <c>.debug_abbrev</c>: each declaration gives a
/// DIE shape — its tag, whether it has children, and the attribute/form pairs its instances carry
/// (with the constant value inline for <c>DW_FORM_implicit_const</c>).
/// </summary>
internal sealed class DwarfAbbrevTable
{
    /// <summary>One attribute slot of an abbreviation declaration.</summary>
    /// <param name="Attribute">The <c>DW_AT_*</c> code.</param>
    /// <param name="Form">The <c>DW_FORM_*</c> code.</param>
    /// <param name="ImplicitConst">The inline constant when the form is <c>implicit_const</c>.</param>
    internal readonly record struct AttributeSpec(ulong Attribute, ulong Form, long ImplicitConst);

    /// <summary>One abbreviation declaration.</summary>
    /// <param name="Tag">The <c>DW_TAG_*</c> code.</param>
    /// <param name="HasChildren">Whether DIEs of this shape own children.</param>
    /// <param name="Attributes">The attribute slots, in declaration order.</param>
    internal sealed record Declaration(ulong Tag, bool HasChildren, IReadOnlyList<AttributeSpec> Attributes);

    private readonly Dictionary<ulong, Declaration> _byCode;

    private DwarfAbbrevTable(Dictionary<ulong, Declaration> byCode) => _byCode = byCode;

    /// <summary>Looks up a declaration by its abbreviation code.</summary>
    /// <param name="code">The code a DIE starts with.</param>
    /// <param name="declaration">The declaration when known.</param>
    public bool TryGet(ulong code, out Declaration declaration) =>
        _byCode.TryGetValue(code, out declaration!);

    /// <summary>
    /// Parses the abbreviation table at <paramref name="offset"/> in <c>.debug_abbrev</c>. A
    /// malformed table yields the declarations parsed before the damage.
    /// </summary>
    /// <param name="abbrev">The <c>.debug_abbrev</c> bytes.</param>
    /// <param name="offset">The table's byte offset (the CU header's abbrev offset).</param>
    public static DwarfAbbrevTable Parse(ReadOnlySpan<byte> abbrev, long offset)
    {
        var byCode = new Dictionary<ulong, Declaration>();
        try
        {
            if (offset < 0 || offset >= abbrev.Length) return new DwarfAbbrevTable(byCode);
            var reader = new DwarfDataReader(abbrev) { Position = (int)offset };

            while (reader.Remaining > 0)
            {
                var code = reader.ReadULeb128();
                if (code == 0) break; // end of this unit's table

                var tag = reader.ReadULeb128();
                var hasChildren = reader.ReadU8() != 0;

                var attributes = new List<AttributeSpec>();
                while (true)
                {
                    var attribute = reader.ReadULeb128();
                    var form = reader.ReadULeb128();
                    if (attribute == 0 && form == 0) break;

                    long implicitConst = 0;
                    if (form == DwarfForm.ImplicitConst)
                        implicitConst = reader.ReadSLeb128();

                    attributes.Add(new AttributeSpec(attribute, form, implicitConst));
                }

                byCode[code] = new Declaration(tag, hasChildren, attributes);
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the declarations parsed so far.
        }

        return new DwarfAbbrevTable(byCode);
    }
}
