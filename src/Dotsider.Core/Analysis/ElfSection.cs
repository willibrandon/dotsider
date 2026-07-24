namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes one ELF section header's identity and file range.
/// </summary>
internal readonly record struct ElfSection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElfSection"/> structure.
    /// </summary>
    /// <param name="name">The section name from the section-header string table.</param>
    /// <param name="type">The <c>sh_type</c> value.</param>
    /// <param name="address">The section's virtual address.</param>
    /// <param name="fileOffset">The section's file offset.</param>
    /// <param name="size">The section's byte size.</param>
    /// <param name="link">The <c>sh_link</c> value.</param>
    /// <param name="info">The <c>sh_info</c> value.</param>
    /// <param name="flags">The <c>sh_flags</c> value.</param>
    internal ElfSection(
        string name,
        uint type,
        ulong address,
        int fileOffset,
        int size,
        uint link,
        uint info,
        ulong flags)
    {
        Name = name;
        Type = type;
        Address = address;
        FileOffset = fileOffset;
        Size = size;
        Link = link;
        Info = info;
        Flags = flags;
    }

    /// <summary>Gets the section's virtual address.</summary>
    internal ulong Address { get; }

    /// <summary>Gets the section's file offset.</summary>
    internal int FileOffset { get; }

    /// <summary>Gets the section flags.</summary>
    internal ulong Flags { get; }

    /// <summary>Gets the section-specific information value.</summary>
    internal uint Info { get; }

    /// <summary>Gets the linked section index.</summary>
    internal uint Link { get; }

    /// <summary>Gets the section name.</summary>
    internal string Name { get; }

    /// <summary>Gets the section's byte size.</summary>
    internal int Size { get; }

    /// <summary>Gets the section type.</summary>
    internal uint Type { get; }
}
