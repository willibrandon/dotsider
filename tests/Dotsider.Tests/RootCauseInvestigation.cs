using Dotsider.Core.Analysis;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Regression tests for the ldelema operand table bug that caused IL walk desync.
/// </summary>
[TestClass]
public class IlWalkRegressionTests
{
    /// <summary>
    /// Zero-fallback invariant: walking all token-bearing operands in CoreLib must produce
    /// zero resolver failures. Any failure indicates an operand table bug causing offset desync.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CoreLib_AllTokenOperands_ResolveWithoutFallback()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        using var analyzer = new AssemblyAnalyzer(coreLibPath);
        var reader = analyzer.GetMetadataReader()!;

        var failures = new List<string>();
        int tokensChecked = 0;

        foreach (var method in analyzer.MethodDefs)
        {
            if (method.Rva == 0) continue;
            MethodBodyBlock? body;
            try { body = analyzer.GetMethodBody(method); }
            catch (BadImageFormatException) { continue; }
            if (body is null) continue;

            var il = body.GetILBytes();
            if (il is null) continue;

            int offset = 0;
            while (offset < il.Length)
            {
                int instrOffset = offset;
                var opByte = il[offset++];
                ILOpCode op;
                if (opByte == 0xFE)
                {
                    if (offset >= il.Length) break;
                    op = (ILOpCode)(0xFE00 | il[offset++]);
                }
                else op = (ILOpCode)opByte;

                var kind = IlDisassembler.GetOperandType(op);

                bool isTokenOp = kind is
                    IlDisassembler.OperandKind.InlineMethod or
                    IlDisassembler.OperandKind.InlineField or
                    IlDisassembler.OperandKind.InlineType or
                    IlDisassembler.OperandKind.InlineTok or
                    IlDisassembler.OperandKind.InlineSig;

                if (kind == IlDisassembler.OperandKind.InlineSwitch)
                {
                    if (offset + 4 > il.Length) break;
                    var count = BitConverter.ToInt32(il, offset);
                    offset += 4 + count * 4;
                    continue;
                }

                if (isTokenOp)
                {
                    if (offset + 4 > il.Length) break;
                    var token = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    tokensChecked++;

                    EntityHandle handle;
                    try { handle = MetadataTokens.EntityHandle(token); }
                    catch
                    {
                        failures.Add($"{method.DeclaringType}::{method.Name} @ IL_{instrOffset:X4}: " +
                            $"invalid token 0x{token:X8} after {op}");
                        continue;
                    }

                    int row = MetadataTokens.GetRowNumber(handle);
                    try
                    {
                        // Validate the handle resolves — this is the path that was crashing
                        switch (handle.Kind)
                        {
                            case HandleKind.MethodDefinition:
                                _ = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                                break;
                            case HandleKind.MemberReference:
                                _ = reader.GetMemberReference((MemberReferenceHandle)handle);
                                break;
                            case HandleKind.FieldDefinition:
                                _ = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                                break;
                            case HandleKind.TypeDefinition:
                                _ = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                                break;
                            case HandleKind.TypeReference:
                                _ = reader.GetTypeReference((TypeReferenceHandle)handle);
                                break;
                            case HandleKind.MethodSpecification:
                                _ = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                                break;
                            case HandleKind.TypeSpecification:
                                _ = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
                                break;
                            case HandleKind.StandaloneSignature:
                                _ = reader.GetStandaloneSignature((StandaloneSignatureHandle)handle);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{method.DeclaringType}::{method.Name} @ IL_{instrOffset:X4}: " +
                            $"0x{token:X8} ({handle.Kind} row {row}) after {op} — " +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                }
                else
                {
                    int size = kind switch
                    {
                        IlDisassembler.OperandKind.None => 0,
                        IlDisassembler.OperandKind.ShortBranchTarget or
                        IlDisassembler.OperandKind.ShortInlineI or
                        IlDisassembler.OperandKind.ShortInlineVar => 1,
                        IlDisassembler.OperandKind.InlineVar => 2,
                        IlDisassembler.OperandKind.BranchTarget or
                        IlDisassembler.OperandKind.InlineI or
                        IlDisassembler.OperandKind.ShortInlineR or
                        IlDisassembler.OperandKind.InlineString => 4,
                        IlDisassembler.OperandKind.InlineI8 or
                        IlDisassembler.OperandKind.InlineR => 8,
                        _ => 0
                    };
                    offset += size;
                }
            }
        }

        Assert.IsEmpty(failures,
            $"Found {failures.Count} resolver fallbacks in {tokensChecked} tokens " +
            $"(first 5):\n" + string.Join("\n", failures.Take(5)));
    }

    /// <summary>
    /// Regression test: ldelema must be decoded with InlineType (4-byte operand).
    /// Before the fix, it mapped to None (0 bytes), causing a 4-byte offset desync.
    /// </summary>
    [TestMethod]
    public void GetOperandType_Ldelema_ReturnsInlineType()
    {
        Assert.AreEqual(
            IlDisassembler.OperandKind.InlineType,
            IlDisassembler.GetOperandType(ILOpCode.Ldelema));
    }

    /// <summary>
    /// Regression test: CoreLib distinct-analyzer diff completes without hitting
    /// the BadImageFormatException fallback in ResolveTokenForComparison.
    /// The original crash was a desync from the missing ldelema operand entry.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CoreLib_DistinctAnalyzerDiff_NoResolverFallbacks()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        using var left = new AssemblyAnalyzer(coreLibPath);
        using var right = new AssemblyAnalyzer(coreLibPath);

        // This was the exact path that crashed before the fix
        var result = AssemblyDiffer.Compare(left, right);

        // With correct operand table, identity diff should have zero body changes
        Assert.DoesNotContain(d =>
            d.ChangeDescription?.Contains("body") == true, result.MethodDiffs);
    }
}
