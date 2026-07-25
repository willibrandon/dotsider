using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Produces structurally valid in-memory ReadyToRun images whose real section routing reaches a
/// deliberately over-deep method signature.
/// </summary>
internal static class ReadyToRunImagePatcher
{
    private const byte FixupMethodEntry = 0x13;
    private const byte FixupHelper = 0x1A;
    private const int ImportSectionRecordSize = 20;

    /// <summary>
    /// Replaces the import directory with two records whose cumulative slot count can exercise the
    /// image-wide traversal budget. The first slot of each record resolves to
    /// <c>DelayLoad_MethodCall</c>; every remaining signature is nil.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <param name="firstCount">The first import record's slot count.</param>
    /// <param name="secondCount">The second import record's slot count.</param>
    /// <returns>The patched image and the first slot address of each record.</returns>
    internal static (
        byte[] Image,
        ulong FirstSlotVirtualAddress,
        ulong SecondSlotVirtualAddress)
        PatchImportSlotBudget(string path, int firstCount, int secondCount)
    {
        if (firstCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstCount));
        }

        if (secondCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(secondCount));
        }

        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var imageBase = analyzer.PeHeaders?.ImageBase
            ?? throw new InvalidOperationException("The ReadyToRun fixture has no PE image base.");

        var firstSlotsOffset = ImportSectionRecordSize * 2;
        var firstSignaturesOffset = checked(firstSlotsOffset + firstCount);
        var secondSlotsOffset = checked(firstSignaturesOffset + firstCount * sizeof(uint));
        var secondSignaturesOffset = checked(secondSlotsOffset + secondCount);
        var helperOffset = checked(secondSignaturesOffset + secondCount * sizeof(uint));
        var payload = new byte[checked(helperOffset + 2)];
        payload[helperOffset] = FixupHelper;
        payload[helperOffset + 1] = 0x08;

        var appended = AppendPayload(original, payload);
        WriteImportRecord(
            appended.Image,
            appended.Offset,
            checked(appended.Rva + firstSlotsOffset),
            firstCount,
            checked(appended.Rva + firstSignaturesOffset));
        WriteImportRecord(
            appended.Image,
            appended.Offset + ImportSectionRecordSize,
            checked(appended.Rva + secondSlotsOffset),
            secondCount,
            checked(appended.Rva + secondSignaturesOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(appended.Offset + firstSignaturesOffset),
            checked((uint)(appended.Rva + helperOffset)));
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(appended.Offset + secondSignaturesOffset),
            checked((uint)(appended.Rva + helperOffset)));
        PatchReadyToRunSection(
            appended.Image,
            info,
            ReadyToRunSectionType.ImportSections,
            appended.Rva,
            ImportSectionRecordSize * 2);

        return (
            appended.Image,
            checked(imageBase + (uint)(appended.Rva + firstSlotsOffset)),
            checked(imageBase + (uint)(appended.Rva + secondSlotsOffset)));
    }

    /// <summary>
    /// Replaces the import directory with a two-slot record whose first helper is valid and whose
    /// second helper is truncated at the final file-backed byte.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <returns>The patched image and the virtual address of each slot.</returns>
    internal static (
        byte[] Image,
        ulong ValidSlotVirtualAddress,
        ulong MalformedSlotVirtualAddress)
        PatchImportValidThenMalformedSlots(string path)
    {
        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var imageBase = analyzer.PeHeaders?.ImageBase
            ?? throw new InvalidOperationException("The ReadyToRun fixture has no PE image base.");

        const int slotsOffset = ImportSectionRecordSize;
        const int signaturesOffset = slotsOffset + 2;
        const int helperOffset = signaturesOffset + 2 * sizeof(uint);
        var payload = new byte[helperOffset + 4];
        payload[helperOffset] = FixupHelper;
        payload[helperOffset + 1] = 0x08;

        var appended = AppendPayload(original, payload);
        WriteImportRecord(
            appended.Image,
            appended.Offset,
            checked(appended.Rva + slotsOffset),
            slotCount: 2,
            checked(appended.Rva + signaturesOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(appended.Offset + signaturesOffset),
            checked((uint)(appended.Rva + helperOffset)));

        var malformedOffset = appended.Image.Length - 2;
        var malformedRva = checked(appended.Rva + malformedOffset - appended.Offset);
        appended.Image[malformedOffset] = FixupHelper;
        appended.Image[malformedOffset + 1] = 0xE0;
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(appended.Offset + signaturesOffset + sizeof(uint)),
            checked((uint)malformedRva));
        PatchReadyToRunSection(
            appended.Image,
            info,
            ReadyToRunSectionType.ImportSections,
            appended.Rva,
            ImportSectionRecordSize);

        var firstSlotAddress = checked(imageBase + (uint)(appended.Rva + slotsOffset));
        return (
            appended.Image,
            firstSlotAddress,
            checked(firstSlotAddress + 1));
    }

    /// <summary>
    /// Replaces the import directory with one record whose slot or signature-table RVA is forged
    /// while its other ranges remain file-backed.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <param name="forgeSlotsRva">
    /// Whether <paramref name="forgedRva"/> replaces the slots RVA; otherwise it replaces the
    /// signature-table RVA.
    /// </param>
    /// <param name="forgedRva">The signed RVA written to the selected record field.</param>
    /// <returns>The patched image and the address the valid slot range would occupy.</returns>
    internal static (byte[] Image, ulong ValidSlotVirtualAddress) PatchImportRvaBoundary(
        string path,
        bool forgeSlotsRva,
        int forgedRva)
    {
        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var imageBase = analyzer.PeHeaders?.ImageBase
            ?? throw new InvalidOperationException("The ReadyToRun fixture has no PE image base.");

        const int slotsOffset = ImportSectionRecordSize;
        const int signaturesOffset = slotsOffset + 1;
        const int helperOffset = signaturesOffset + sizeof(uint);
        var payload = new byte[helperOffset + 2];
        payload[helperOffset] = FixupHelper;
        payload[helperOffset + 1] = 0x08;

        var appended = AppendPayload(original, payload);
        var slotsRva = checked(appended.Rva + slotsOffset);
        var signaturesRva = checked(appended.Rva + signaturesOffset);
        WriteImportRecord(
            appended.Image,
            appended.Offset,
            forgeSlotsRva ? forgedRva : slotsRva,
            slotCount: 1,
            forgeSlotsRva ? signaturesRva : forgedRva);
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(appended.Offset + signaturesOffset),
            checked((uint)(appended.Rva + helperOffset)));
        PatchReadyToRunSection(
            appended.Image,
            info,
            ReadyToRunSectionType.ImportSections,
            appended.Rva,
            ImportSectionRecordSize);

        return (
            appended.Image,
            checked(imageBase + (uint)slotsRva));
    }

    /// <summary>
    /// Replaces <c>InstanceMethodEntryPoints</c> with a one-entry NativeHashtable whose payload is
    /// a depth-129 method-instantiation signature followed by runtime-function index zero.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <returns>The patched image and exact offsets of the table and signature payload.</returns>
    internal static (byte[] Image, int TableOffset, int SignatureOffset)
        PatchInstanceMethodEntryPoints(string path)
    {
        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);

        var signature = BuildDeepMethodSignature();
        var table = new byte[5 + signature.Length + 1];
        table[0] = 0x00; // one bucket, one-byte bucket indexes
        table[1] = 0x02; // bucket starts two bytes after the NativeHashtable base
        table[2] = 0x04; // bucket ends after the hash byte and signed payload delta
        table[3] = 0x00; // low hash code
        table[4] = 0x02; // signed delta +1: payload begins at the next byte
        signature.CopyTo(table, 5);
        table[^1] = 0x00; // runtime-function index zero

        var appended = AppendPayload(original, table);
        PatchReadyToRunSection(
            appended.Image,
            info,
            ReadyToRunSectionType.InstanceMethodEntryPoints,
            appended.Rva,
            table.Length);
        return (appended.Image, appended.Offset, appended.Offset + 5);
    }

    /// <summary>
    /// Repoints one import slot that resolves in the original image at a depth-129 method-entry
    /// fixup, leaving every other record and slot untouched.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <returns>
    /// The patched image, exact method-signature offset, affected slot address and name, and the
    /// original number of named imports.
    /// </returns>
    internal static (
        byte[] Image,
        int SignatureOffset,
        ulong SlotVirtualAddress,
        string OriginalName,
        int OriginalCount)
        PatchImportMethodEntry(string path)
    {
        var methodSignature = BuildDeepMethodSignature();
        var fixup = new byte[1 + methodSignature.Length];
        fixup[0] = FixupMethodEntry;
        methodSignature.CopyTo(fixup, 1);
        var patched = PatchImportFixup(path, fixup);
        return (
            patched.Image,
            patched.FixupOffset + 1,
            patched.SlotVirtualAddress,
            patched.OriginalName,
            patched.OriginalCount);
    }

    /// <summary>
    /// Repoints one named import slot in a real ReadyToRun image at the supplied fixup bytes.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <param name="fixup">The complete ReadyToRun fixup payload.</param>
    /// <returns>
    /// The patched image, exact fixup offset, affected slot address and name, and the original
    /// number of named imports.
    /// </returns>
    internal static (
        byte[] Image,
        int FixupOffset,
        ulong SlotVirtualAddress,
        string OriginalName,
        int OriginalCount)
        PatchImportFixup(string path, byte[] fixup)
    {
        ArgumentNullException.ThrowIfNull(fixup);
        if (fixup.Length == 0)
        {
            throw new ArgumentException("A ReadyToRun fixup cannot be empty.", nameof(fixup));
        }

        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var imports = ReadyToRunImportMap.Build(analyzer)
            ?? throw new InvalidOperationException("The ReadyToRun fixture has no import map.");
        var section = info.Sections.Single(
            static section => section.Type == (int)ReadyToRunSectionType.ImportSections);
        if (section.FileOffset is not { } sectionOffset || section.Size <= 0)
        {
            throw new InvalidOperationException("The ReadyToRun import section is not file-backed.");
        }

        var imageBase = analyzer.PeHeaders?.ImageBase
            ?? throw new InvalidOperationException("The ReadyToRun fixture has no PE image base.");
        var pointerSize = GetPointerSize(original);
        var end = checked(sectionOffset + section.Size);
        int? signatureRvaFieldOffset = null;
        ulong selectedSlot = 0;
        string? originalName = null;
        for (var record = sectionOffset;
             record + ImportSectionRecordSize <= end && signatureRvaFieldOffset is null;
             record += ImportSectionRecordSize)
        {
            var slotsRva = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(record));
            var slotsSize = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(record + 4));
            int entrySize = original[record + 11];
            if (entrySize == 0)
            {
                entrySize = pointerSize;
            }

            var signaturesRva = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(record + 12));
            if (slotsRva <= 0 || slotsSize <= 0 || entrySize <= 0 || slotsSize % entrySize != 0
                || signaturesRva <= 0)
            {
                continue;
            }

            var signaturesOffset = RvaToFileOffset(original, signaturesRva);
            var count = slotsSize / entrySize;
            for (var slot = 0; slot < count; slot++)
            {
                var slotAddress = checked(imageBase + (uint)slotsRva + (uint)(slot * entrySize));
                if (!imports.TryResolve(slotAddress, out var symbol))
                {
                    continue;
                }

                signatureRvaFieldOffset = checked(signaturesOffset + slot * sizeof(uint));
                selectedSlot = slotAddress;
                originalName = symbol.Name;
                break;
            }
        }

        if (signatureRvaFieldOffset is not { } rvaFieldOffset || originalName is null)
        {
            throw new InvalidOperationException("The ReadyToRun fixture has no named import slot to patch.");
        }

        var appended = AppendPayload(original, fixup);
        BinaryPrimitives.WriteUInt32LittleEndian(
            appended.Image.AsSpan(rvaFieldOffset),
            checked((uint)appended.Rva));

        return (appended.Image, appended.Offset, selectedSlot, originalName, imports.Count);
    }

    /// <summary>
    /// Replaces one image-level ReadyToRun section with appended bytes while independently setting
    /// the section's declared size. Bytes beyond that size remain in the image to prove readers do
    /// not escape the declared section boundary.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <param name="sectionType">The image-level section to replace.</param>
    /// <param name="payload">The bytes appended to the image.</param>
    /// <param name="declaredSize">The byte size written to the ReadyToRun section directory.</param>
    /// <returns>The patched image and the appended payload's file offset.</returns>
    internal static (byte[] Image, int PayloadOffset) PatchNativeFormatSection(
        string path,
        ReadyToRunSectionType sectionType,
        byte[] payload,
        int declaredSize)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (declaredSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredSize));
        }

        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var appended = AppendPayload(original, payload);
        PatchReadyToRunSection(
            appended.Image,
            info,
            sectionType,
            appended.Rva,
            declaredSize);
        return (appended.Image, appended.Offset);
    }

    /// <summary>
    /// Replaces an image-wide ReadyToRun table with appended bytes and an independently controlled
    /// signed section size. An absent target can take over an existing section-directory row.
    /// </summary>
    /// <param name="path">The real ReadyToRun image to copy and patch.</param>
    /// <param name="sectionType">The image-wide table to replace.</param>
    /// <param name="payload">The bytes appended to the image.</param>
    /// <param name="declaredSize">The signed byte size written to the ReadyToRun section directory.</param>
    /// <param name="replacementType">
    /// An existing section row to rename when <paramref name="sectionType"/> is absent.
    /// </param>
    /// <returns>The patched image and the appended payload's file offset.</returns>
    internal static (byte[] Image, int PayloadOffset) PatchImageWideTable(
        string path,
        ReadyToRunSectionType sectionType,
        byte[] payload,
        int declaredSize,
        ReadyToRunSectionType? replacementType = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        var appended = AppendPayload(original, payload);
        PatchReadyToRunSection(
            appended.Image,
            info,
            sectionType,
            appended.Rva,
            declaredSize,
            replacementType);
        return (appended.Image, appended.Offset);
    }

    /// <summary>
    /// Replaces one component core header's <c>MethodDefEntryPoints</c> section with appended bytes
    /// while independently setting its declared size.
    /// </summary>
    /// <param name="path">The real composite ReadyToRun image to copy and patch.</param>
    /// <param name="componentMvid">The exact component core header to patch.</param>
    /// <param name="payload">The bytes appended to the composite image.</param>
    /// <param name="declaredSize">The byte size written to the component section directory.</param>
    /// <returns>The patched image and the appended payload's file offset.</returns>
    internal static (byte[] Image, int PayloadOffset) PatchComponentMethodDefEntryPoints(
        string path,
        Guid componentMvid,
        byte[] payload,
        int declaredSize)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (declaredSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(declaredSize));
        }

        var original = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(original, path);
        var info = RequireValidReadyToRunInfo(analyzer);
        if (!info.IsComposite)
        {
            throw new InvalidOperationException("The fixture is not a composite ReadyToRun image.");
        }

        var addressSpace = NativeAddressSpace.Create(original)
            ?? throw new BadImageFormatException("The composite fixture has no native address space.");
        var imageBase = analyzer.PeHeaders?.ImageBase
            ?? throw new BadImageFormatException("The composite fixture has no PE image base.");
        var component = ReadyToRunCompositeReader.ReadComponents(
                original,
                info,
                imageBase,
                addressSpace)
            .Single(candidate => candidate.Mvid == componentMvid);

        var appended = AppendPayload(original, payload);
        var coreHeaderOffset = RvaToFileOffset(appended.Image, component.CoreHeaderRva);
        PatchCoreReadyToRunSection(
            appended.Image,
            coreHeaderOffset,
            ReadyToRunSectionType.MethodDefEntryPoints,
            appended.Rva,
            declaredSize);
        return (appended.Image, appended.Offset);
    }

    private static (byte[] Image, int Offset, int Rva) AppendPayload(byte[] original, byte[] payload)
    {
        var peOffset = ReadPeOffset(original);
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(peOffset + 20));
        var optionalHeader = peOffset + 24;
        var fileAlignment = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(optionalHeader + 36)));
        var sectionAlignment = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(optionalHeader + 32)));
        if (fileAlignment <= 0 || sectionAlignment <= 0)
        {
            throw new BadImageFormatException("The PE image has invalid section alignment.");
        }

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(peOffset + 6));
        var sectionTable = optionalHeader + optionalHeaderSize;
        var lastSection = -1;
        long lastRawEnd = -1;
        uint lastVirtualAddress = 0;
        for (var index = 0; index < sectionCount; index++)
        {
            var section = checked(sectionTable + index * 40);
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(section + 16));
            var rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(section + 20));
            var rawEnd = (long)rawOffset + rawSize;
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(section + 12));
            if (rawSize > 0 && rawEnd > lastRawEnd)
            {
                lastSection = section;
                lastRawEnd = rawEnd;
                lastVirtualAddress = virtualAddress;
            }
        }

        if (lastSection < 0 || lastRawEnd > original.Length)
        {
            throw new BadImageFormatException("The PE image has no valid final file-backed section.");
        }

        for (var index = 0; index < sectionCount; index++)
        {
            var section = checked(sectionTable + index * 40);
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(section + 12));
            if (virtualAddress > lastVirtualAddress)
            {
                throw new BadImageFormatException(
                    "The PE image's final file-backed section is not its final virtual section.");
            }
        }

        var rawPointer = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(lastSection + 20)));
        var virtualAddressBase = checked((int)lastVirtualAddress);
        var appendOffset = AlignUp(Math.Max(original.Length, checked((int)lastRawEnd)), fileAlignment);
        var payloadEndInSection = checked(appendOffset - rawPointer + payload.Length);
        var newRawSize = AlignUp(payloadEndInSection, fileAlignment);
        var newLength = checked(rawPointer + newRawSize);
        var image = new byte[newLength];
        original.CopyTo(image, 0);
        payload.CopyTo(image, appendOffset);

        var oldVirtualSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(lastSection + 8)));
        var newVirtualSize = Math.Max(oldVirtualSize, payloadEndInSection);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(lastSection + 8),
            checked((uint)newVirtualSize));
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(lastSection + 16),
            checked((uint)newRawSize));

        var sizeOfImage = AlignUp(checked(virtualAddressBase + newVirtualSize), sectionAlignment);
        var oldSizeOfImage = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            original.AsSpan(optionalHeader + 56)));
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(optionalHeader + 56),
            checked((uint)Math.Max(oldSizeOfImage, sizeOfImage)));

        return (image, appendOffset, checked(virtualAddressBase + appendOffset - rawPointer));
    }

    private static byte[] BuildDeepMethodSignature()
    {
        const int nestingDepth = 129;
        var signature = new byte[3 + nestingDepth + 1];
        signature[0] = 0x04; // READYTORUN_METHOD_SIG_MethodInstantiation
        signature[1] = 0x01; // MethodDef RID 1
        signature[2] = 0x01; // one method-instantiation argument
        Array.Fill(signature, (byte)0x0F, 3, nestingDepth);
        signature[^1] = 0x08; // I4 leaf
        return signature;
    }

    private static int GetPointerSize(byte[] image)
    {
        var peOffset = ReadPeOffset(image);
        var optionalHeader = peOffset + 24;
        return BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(optionalHeader)) switch
        {
            0x010B => sizeof(uint),
            0x020B => sizeof(ulong),
            _ => throw new BadImageFormatException("The PE image has an unsupported optional header."),
        };
    }

    private static int RvaToFileOffset(byte[] image, int rva)
    {
        if (rva < 0)
        {
            throw new BadImageFormatException("The PE image contains a negative RVA.");
        }

        var peOffset = ReadPeOffset(image);
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 6));
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 20));
        var sectionTable = peOffset + 24 + optionalHeaderSize;
        for (var index = 0; index < sectionCount; index++)
        {
            var section = checked(sectionTable + index * 40);
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(section + 12));
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(section + 16));
            var rawPointer = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(section + 20));
            if ((uint)rva >= virtualAddress && (uint)rva - virtualAddress < rawSize)
            {
                return checked((int)(rawPointer + (uint)rva - virtualAddress));
            }
        }

        throw new BadImageFormatException($"PE RVA 0x{rva:X8} is not file-backed.");
    }

    private static void WriteImportRecord(
        byte[] image,
        int recordOffset,
        int slotsRva,
        int slotCount,
        int signaturesRva)
    {
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(recordOffset), slotsRva);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(recordOffset + 4), slotCount);
        image[recordOffset + 11] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(recordOffset + 12), signaturesRva);
    }

    private static void PatchReadyToRunSection(
        byte[] image,
        ReadyToRunInfo info,
        ReadyToRunSectionType sectionType,
        int rva,
        int size,
        ReadyToRunSectionType? replacementType = null)
    {
        var headerOffset = RvaToFileOffset(image, info.HeaderRva);
        var rows = checked(headerOffset + 16);
        int? replacementRow = null;
        for (var index = 0; index < info.SectionCount; index++)
        {
            var row = checked(rows + index * 12);
            var currentType = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(row));
            if (replacementType is { } replacement && currentType == (int)replacement)
            {
                replacementRow = row;
            }

            if (currentType != (int)sectionType)
            {
                continue;
            }

            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(row + 4), rva);
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(row + 8), size);
            return;
        }

        if (replacementRow is { } substitute)
        {
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(substitute), (int)sectionType);
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(substitute + 4), rva);
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(substitute + 8), size);
            return;
        }

        throw new InvalidOperationException($"The ReadyToRun fixture has no {sectionType} section.");
    }

    private static void PatchCoreReadyToRunSection(
        byte[] image,
        int headerOffset,
        ReadyToRunSectionType sectionType,
        int rva,
        int size)
    {
        if (headerOffset < 0 || headerOffset > image.Length - 8)
        {
            throw new BadImageFormatException("The component ReadyToRun core header is truncated.");
        }

        var sectionCount = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(headerOffset + 4));
        if (sectionCount is < 0 or > 4096)
        {
            throw new BadImageFormatException("The component ReadyToRun core header has an invalid section count.");
        }

        var rows = checked(headerOffset + 8);
        for (var index = 0; index < sectionCount; index++)
        {
            var row = checked(rows + index * 12);
            if (row > image.Length - 12)
            {
                throw new BadImageFormatException("The component ReadyToRun section directory is truncated.");
            }

            if (BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(row)) != (int)sectionType)
            {
                continue;
            }

            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(row + 4), rva);
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(row + 8), size);
            return;
        }

        throw new InvalidOperationException(
            $"The ReadyToRun component fixture has no {sectionType} section.");
    }

    private static ReadyToRunInfo RequireValidReadyToRunInfo(AssemblyAnalyzer analyzer)
    {
        if (analyzer.ReadyToRunInfo is not { Status: ReadyToRunStatus.Valid } info)
        {
            throw new InvalidOperationException("The fixture is not a valid ReadyToRun image.");
        }

        return info;
    }

    private static int ReadPeOffset(byte[] image)
    {
        if (image.Length < 0x40)
        {
            throw new BadImageFormatException("The PE image is truncated.");
        }

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
        if (peOffset <= 0 || peOffset > image.Length - 24
            || BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(peOffset)) != 0x0000_4550)
        {
            throw new BadImageFormatException("The PE image has no valid NT headers.");
        }

        return peOffset;
    }

    private static int AlignUp(int value, int alignment) =>
        checked((value + alignment - 1) / alignment * alignment);
}
