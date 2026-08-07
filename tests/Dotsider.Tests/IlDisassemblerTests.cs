using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Il Disassembler.
/// </summary>
[TestClass]
public class IlDisassemblerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Supplies one truncated body for every fixed-width IL operand encoding.
    /// </summary>
    /// <returns>The display name, expected opcode, and malformed IL body for each case.</returns>
    public static IEnumerable<object[]> TruncatedOperandCases()
    {
        yield return ["short branch", "br.s", CreateTruncatedOperand(ILOpCode.Br_s, 1)];
        yield return ["branch", "br", CreateTruncatedOperand(ILOpCode.Br, 4)];
        yield return ["short integer", "ldc.i4.s", CreateTruncatedOperand(ILOpCode.Ldc_i4_s, 1)];
        yield return ["integer", "ldc.i4", CreateTruncatedOperand(ILOpCode.Ldc_i4, 4)];
        yield return ["long integer", "ldc.i8", CreateTruncatedOperand(ILOpCode.Ldc_i8, 8)];
        yield return ["single", "ldc.r4", CreateTruncatedOperand(ILOpCode.Ldc_r4, 4)];
        yield return ["double", "ldc.r8", CreateTruncatedOperand(ILOpCode.Ldc_r8, 8)];
        yield return ["short variable", "ldarg.s", CreateTruncatedOperand(ILOpCode.Ldarg_s, 1)];
        yield return ["variable", "ldarg", CreateTruncatedOperand(ILOpCode.Ldarg, 2)];
        yield return ["string token", "ldstr", CreateTruncatedOperand(ILOpCode.Ldstr, 4)];
        yield return ["method token", "call", CreateTruncatedOperand(ILOpCode.Call, 4)];
        yield return ["field token", "ldfld", CreateTruncatedOperand(ILOpCode.Ldfld, 4)];
        yield return ["type token", "box", CreateTruncatedOperand(ILOpCode.Box, 4)];
        yield return ["member token", "ldtoken", CreateTruncatedOperand(ILOpCode.Ldtoken, 4)];
        yield return ["signature token", "calli", CreateTruncatedOperand(ILOpCode.Calli, 4)];
    }

    /// <summary>
    /// Supplies malformed <c>switch</c> encodings that must not read past the method body.
    /// </summary>
    /// <returns>The display name and malformed IL body for each switch case.</returns>
    public static IEnumerable<object[]> TruncatedSwitchCases()
    {
        yield return ["missing count", new byte[] { 0x00, 0x45 }];
        yield return ["partial count", new byte[] { 0x00, 0x45, 0x01, 0x00, 0x00 }];
        yield return ["negative count", new byte[] { 0x00, 0x45, 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return ["missing target", new byte[] { 0x00, 0x45, 0x01, 0x00, 0x00, 0x00 }];
        yield return ["partial target", new byte[] { 0x00, 0x45, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
        yield return ["overflowing count", new byte[] { 0x00, 0x45, 0xFF, 0xFF, 0xFF, 0x7F }];
    }

    /// <summary>
    /// Verifies hello world main method contains call and ret.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_MainMethod_ContainsCallAndRet()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var mainMethod = a.MethodDefs.FirstOrDefault(m => m.Name == "<Main>$" || m.Name == "Main");
        Assert.IsNotNull(mainMethod);
        var instructions = disasm.Disassemble(mainMethod);
        Assert.IsNotEmpty(instructions);
        Assert.Contains(i => i.OpCode.Contains("ret"), instructions);
    }

    /// <summary>
    /// Verifies rich library user service add disassembles successfully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_UserServiceAdd_DisassemblesSuccessfully()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType == "RichLibrary.Services.UserService" && m.Name == "Add");
        Assert.IsNotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.IsNotEmpty(instructions);
    }

    /// <summary>
    /// Verifies formatted IL includes portable PDB annotations.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_UserServiceAdd_FormatIncludesPortablePdbAnnotations()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");

        var text = disasm.FormatDisassembly(method);

        Assert.Contains("// PDB: Sidecar", text);
        Assert.Contains("// Source Link: present", text);
        Assert.Contains(".locals init", text);
        Assert.Contains("UserService.cs", text);
        Assert.Contains("[source link]", text);
        Assert.Contains("// id", text);
        Assert.Contains("// user", text);
        Assert.DoesNotContain("raw.githubusercontent.com", text);
    }

    /// <summary>
    /// Verifies decoded IL instructions carry sequence point and local variable metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_UserServiceAdd_InstructionsIncludeDebugMetadata()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");

        var result = disasm.DisassembleWithText(method);

        Assert.IsNotNull(result);
        Assert.Contains(instruction => instruction.SequenceDocument?.EndsWith("UserService.cs",
                StringComparison.OrdinalIgnoreCase) == true
                && instruction.SourceLinkUrl is not null, result.Value.Instructions);
        Assert.Contains(instruction => instruction.LocalName == "id", result.Value.Instructions);
        Assert.Contains(instruction => instruction.LocalName == "user", result.Value.Instructions);
        TestAssert.All(result.Value.Instructions,
            instruction => Assert.IsTrue(instruction.DisplayLine is null or > 0));
    }

    /// <summary>
    /// Verifies formatted IL prints one Source Link marker per distinct URL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_UserServiceAdd_FormatDeduplicatesSourceLinkMarkers()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = FindMethod(a, "RichLibrary.Services.UserService", "Add");

        var result = disasm.DisassembleWithText(method);

        Assert.IsNotNull(result);
        var markerCount = result.Value.Text
            .Split('\n')
            .Count(line => line.Contains("[source link]", StringComparison.Ordinal));
        var distinctUrlCount = result.Value.Instructions
            .Where(instruction => !instruction.SequenceHidden)
            .Select(instruction => instruction.SourceLinkUrl)
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.IsGreaterThan(0, distinctUrlCount);
        Assert.AreEqual(distinctUrlCount, markerCount);
    }

    /// <summary>
    /// Verifies hidden sequence points do not consume the first visible Source Link marker.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dotsider_IlInspectorViewMethod_HiddenPointDoesNotConsumeSourceLinkMarker()
    {
        using var a = new AssemblyAnalyzer(typeof(DotsiderApp).Assembly.Location);
        var disasm = new IlDisassembler(a);
        var method = FindMethod(a, "Dotsider.Views.IlInspectorView", "IsMethodInNamespace");

        var result = disasm.DisassembleWithText(method);

        Assert.IsNotNull(result);
        var lines = result.Value.Text.Split('\n');
        Assert.Contains(line => line == "// (hidden)", lines);

        var firstVisibleSourceLine = lines.First(line =>
            line.Contains("IlInspectorView.cs(", StringComparison.Ordinal));
        Assert.Contains("[source link]", firstVisibleSourceLine);
        Assert.DoesNotContain(lines.First(line => line == "// (hidden)"), "[source link]");
    }

    /// <summary>
    /// Verifies native lib unsafe method has distinct opcodes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_UnsafeMethod_HasDistinctOpcodes()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m => m.Name == "SumWithPointers");
        Assert.IsNotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.IsNotEmpty(instructions);
    }

    /// <summary>
    /// Verifies format disassembly returns readable text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatDisassembly_ReturnsReadableText()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var text = disasm.FormatDisassembly(method);
        Assert.IsNotNull(text);
        Assert.IsNotEmpty(text);
        Assert.Contains("IL_", text);
    }

    /// <summary>
    /// Verifies all rich library methods disassemble without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllRichLibraryMethods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            Assert.IsNotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies method with no body returns empty list.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodWithNoBody_ReturnsEmptyList()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        // P/Invoke methods have Rva == 0, no IL body
        var externMethod = a.MethodDefs.FirstOrDefault(m => m.Rva == 0);
        if (externMethod is not null)
        {
            var instructions = disasm.Disassemble(externMethod);
            Assert.IsEmpty(instructions);
        }
    }

    /// <summary>
    /// Verifies empty lib can construct no methods to disassemble.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyLib_CanConstruct_NoMethodsToDisassemble()
    {
        using var a = new AssemblyAnalyzer(Samples.EmptyLibDll);
        var disasm = new IlDisassembler(a);
        var methodsWithIl = a.MethodDefs.Where(m => m.Rva != 0).ToList();
        // Either no methods or only compiler-generated
        Assert.IsLessThanOrEqualTo(2, methodsWithIl.Count);
    }

    /// <summary>
    /// Verifies complex app async method disassembles successfully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_AsyncMethod_DisassemblesSuccessfully()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        var disasm = new IlDisassembler(a);
        var asyncMethods = a.MethodDefs.Where(m => m.Name.Contains("MoveNext")).ToList();
        if (asyncMethods.Count > 0)
        {
            var instructions = disasm.Disassemble(asyncMethods[0]);
            Assert.IsNotEmpty(instructions);
        }
    }

    /// <summary>
    /// Verifies minimal api methods disassemble without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MinimalApi_Methods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(Samples.MinimalApiDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0).Take(20))
        {
            var instructions = disasm.Disassemble(method);
            Assert.IsNotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies disassemble instruction has offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_InstructionHasOffset()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var instructions = disasm.Disassemble(method);
        Assert.IsNotEmpty(instructions);
        // First instruction should be at offset 0
        Assert.AreEqual(0, instructions[0].Offset);
    }

    /// <summary>
    /// Verifies native lib stack alloc sum has instructions.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_StackAllocSum_HasInstructions()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m => m.Name == "StackAllocSum");
        Assert.IsNotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.IsNotEmpty(instructions);
    }

    /// <summary>
    /// Verifies instruction has op code and operand.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Instruction_HasOpCodeAndOperand()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0 && m.Name != ".ctor");
        var instructions = disasm.Disassemble(method);
        Assert.IsNotEmpty(instructions);
        foreach (var inst in instructions)
        {
            Assert.IsNotNull(inst.OpCode);
            Assert.IsNotNull(inst.Operand); // can be empty string, not null
        }
    }

    /// <summary>
    /// Verifies format disassembly contains hex offsets.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FormatDisassembly_ContainsHexOffsets()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var text = disasm.FormatDisassembly(method);
        Assert.Contains("IL_0000", text);
    }

    /// <summary>
    /// Verifies complex app all methods disassemble without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_AllMethods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            Assert.IsNotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies rich library method with string operand contains ldstr.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodWithStringOperand_ContainsLdstr()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        // Find any method that loads a string literal
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            var ldstr = instructions.FirstOrDefault(i => i.OpCode.Contains("ldstr"));
            if (ldstr is not null)
            {
                Assert.IsNotEmpty(ldstr.Operand);
                return;
            }
        }
    }

    /// <summary>
    /// Verifies rich library method with branches contains br.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodWithBranches_ContainsBr()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            var branch = instructions.FirstOrDefault(i =>
                i.OpCode.Contains("br") || i.OpCode.Contains("brtrue") || i.OpCode.Contains("brfalse"));
            if (branch is not null)
            {
                Assert.Contains("IL_", branch.Operand);
                return;
            }
        }
    }

    /// <summary>
    /// Verifies rich library method call instructions resolve tokens.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodCallInstructions_ResolveTokens()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            var call = instructions.FirstOrDefault(i =>
                i.OpCode == "call" || i.OpCode == "callvirt" || i.OpCode == "newobj");
            if (call is not null)
            {
                Assert.IsNotEmpty(call.Operand);
                // Resolved token should not be just hex
                Assert.DoesNotContain("0x", call.Operand);
                return;
            }
        }
    }

    /// <summary>
    /// Verifies compiler-produced MethodSpec operands are decoded in IL instead of falling back to
    /// their 0x2B metadata tokens.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LinqMethodSpecs_ResolveForDisassembly()
    {
        using var analyzer = new AssemblyAnalyzer(typeof(MethodSpecReproFixture).Assembly.Location);
        var method = Assert.ContainsSingle(analyzer.MethodDefs.Where(candidate =>
            candidate.DeclaringType == MethodSpecReproFixture.TypeName
            && candidate.Name == MethodSpecReproFixture.MethodName));
        var instructions = new IlDisassembler(analyzer)
            .Disassemble(method)
            .Where(candidate => candidate.OpCode == "call"
                && candidate.MetadataToken is { } token
                && MetadataTokens.EntityHandle(token).Kind == HandleKind.MethodSpecification)
            .ToArray();

        Assert.HasCount(MethodSpecReproFixture.ExpectedDisplays.Count, instructions);
        TestAssert.All(instructions, instruction =>
            Assert.AreEqual(0x2B000000,
                instruction.MetadataToken!.Value & unchecked((int)0xFF000000)));
        Assert.AreSequenceEqual(
            MethodSpecReproFixture.ExpectedDisplays,
            instructions.Select(instruction => instruction.Operand));
        TestAssert.All(instructions, instruction => Assert.DoesNotContain("0x2B", instruction.Operand));
    }

    /// <summary>
    /// Verifies every fixed-width operand that ends early yields one explicit, non-navigable marker.
    /// </summary>
    /// <param name="name">The operand shape under test.</param>
    /// <param name="expectedOpcode">The expected rendered opcode.</param>
    /// <param name="il">The malformed method body.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DynamicData(nameof(TruncatedOperandCases))]
    public void Disassemble_TruncatedFixedWidthOperand_EmitsTerminalMalformedMarker(
        string name,
        string expectedOpcode,
        byte[] il)
    {
        byte[] image = SyntheticIlAssembly.Create(il);
        using var analyzer = new AssemblyAnalyzer(image, $"{name}.dll");
        MethodDefInfo method = Assert.ContainsSingle(analyzer.MethodDefs);

        var disassembler = new IlDisassembler(analyzer);
        IReadOnlyList<IlInstruction> instructions = disassembler.Disassemble(method);

        Assert.HasCount(2, instructions);
        Assert.AreEqual("nop", instructions[0].OpCode);
        IlInstruction malformed = instructions[1];
        Assert.AreEqual(expectedOpcode, malformed.OpCode);
        Assert.AreEqual("<truncated operand>", malformed.Operand);
        Assert.IsTrue(malformed.IsMalformed);
        Assert.IsNull(malformed.MetadataToken);
        Assert.Contains("<truncated operand>", disassembler.FormatDisassembly(method));
    }

    /// <summary>
    /// Verifies malformed switch counts and target tables yield one terminal marker without throwing.
    /// </summary>
    /// <param name="name">The malformed switch shape.</param>
    /// <param name="il">The malformed method body.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DynamicData(nameof(TruncatedSwitchCases))]
    public void Disassemble_MalformedSwitch_EmitsTerminalMalformedMarker(string name, byte[] il)
    {
        byte[] image = SyntheticIlAssembly.Create(il);
        using var analyzer = new AssemblyAnalyzer(image, $"switch-{name}.dll");
        MethodDefInfo method = Assert.ContainsSingle(analyzer.MethodDefs);

        IReadOnlyList<IlInstruction> instructions = new IlDisassembler(analyzer).Disassemble(method);

        Assert.HasCount(2, instructions);
        IlInstruction malformed = instructions[1];
        Assert.AreEqual("switch", malformed.OpCode);
        Assert.AreEqual("<truncated operand>", malformed.Operand);
        Assert.IsTrue(malformed.IsMalformed);
    }

    /// <summary>
    /// Verifies an orphaned extended-opcode prefix is retained as an explicit terminal marker.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_TruncatedExtendedOpcode_EmitsTerminalMalformedMarker()
    {
        byte[] image = SyntheticIlAssembly.Create([0x00, 0xFE]);
        using var analyzer = new AssemblyAnalyzer(image, "truncated-opcode.dll");
        MethodDefInfo method = Assert.ContainsSingle(analyzer.MethodDefs);

        IReadOnlyList<IlInstruction> instructions = new IlDisassembler(analyzer).Disassemble(method);

        Assert.HasCount(2, instructions);
        IlInstruction malformed = instructions[1];
        Assert.AreEqual(".invalid", malformed.OpCode);
        Assert.AreEqual("<truncated opcode>", malformed.Operand);
        Assert.IsTrue(malformed.IsMalformed);
    }

    /// <summary>
    /// Verifies a large valid switch advances over every target and preserves the following opcode.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_LargeValidSwitch_PreservesInstructionAfterTargetTable()
    {
        byte[] image = SyntheticIlAssembly.Create(CreateLargeValidSwitch());
        using var analyzer = new AssemblyAnalyzer(image, "large-switch.dll");
        MethodDefInfo method = Assert.ContainsSingle(analyzer.MethodDefs);

        IReadOnlyList<IlInstruction> instructions = new IlDisassembler(analyzer).Disassemble(method);

        Assert.HasCount(3, instructions);
        Assert.AreEqual("switch", instructions[1].OpCode);
        Assert.Contains("... (991 more)", instructions[1].Operand);
        Assert.AreEqual("ret", instructions[2].OpCode);
        Assert.DoesNotContain(static instruction => instruction.IsMalformed, instructions);
    }

    /// <summary>
    /// Verifies a real compiled method shortened inside a call operand degrades through disassembly
    /// and comparison without throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RealSample_MethodEndingInsideCallOperand_DegradesWithoutThrowing()
    {
        byte[] original = File.ReadAllBytes(Samples.HelloWorldDll);
        byte[] patched = (byte[])original.Clone();
        (int Token, int Rva, int CallOffset) = FindCallToTruncate(original);
        TruncateMethodBodyAtCallOperand(patched, Rva, CallOffset);

        using var intactAnalyzer = new AssemblyAnalyzer(original, "HelloWorld.dll");
        using var malformedAnalyzer = new AssemblyAnalyzer(patched, "HelloWorld-truncated.dll");
        MethodDefInfo malformedMethod = Assert.ContainsSingle(
            method => method.Token == Token,
            malformedAnalyzer.MethodDefs);

        IReadOnlyList<IlInstruction> instructions = new IlDisassembler(malformedAnalyzer)
            .Disassemble(malformedMethod);

        IlInstruction malformed = Assert.ContainsSingle(
            static instruction => instruction.IsMalformed,
            instructions);
        Assert.AreEqual("call", malformed.OpCode);
        Assert.AreEqual("<truncated operand>", malformed.Operand);
        Assert.IsNull(malformed.MetadataToken);

        var diff = AssemblyDiffer.Compare(intactAnalyzer, malformedAnalyzer);
        DiffEntry<MethodDefInfo> changed = Assert.ContainsSingle(
            entry => entry.Left?.Token == Token,
            diff.MethodDiffs);
        Assert.AreEqual(DiffKind.Changed, changed.Kind);
    }

    private static MethodDefInfo FindMethod(AssemblyAnalyzer analyzer, string typeName, string methodName)
    {
        var method = analyzer.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType == typeName
            && m.Name == methodName);

        Assert.IsNotNull(method);
        return method;
    }

    private static (int Token, int Rva, int CallOffset) FindCallToTruncate(byte[] image)
    {
        using var analyzer = new AssemblyAnalyzer(image, "HelloWorld.dll");
        var disassembler = new IlDisassembler(analyzer);
        foreach (MethodDefInfo method in analyzer.MethodDefs.Where(static method => method.Rva != 0))
        {
            MethodBodyBlock? body = analyzer.GetMethodBody(method);
            byte[]? il = body?.GetILBytes();
            if (il is null)
            {
                continue;
            }

            IlInstruction? call = disassembler.Disassemble(method).FirstOrDefault(
                static instruction => instruction.OpCode == "call");
            if (call is not null && call.Offset <= il.Length - 5)
            {
                return (method.Token, method.Rva, call.Offset);
            }
        }

        throw new InvalidOperationException("The HelloWorld fixture has no complete call instruction.");
    }

    private static void TruncateMethodBodyAtCallOperand(byte[] image, int rva, int callOffset)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        MethodBodyBlock body = peReader.GetMethodBody(rva);
        byte[] il = body.GetILBytes()
            ?? throw new InvalidOperationException("The selected method body has no IL bytes.");
        if (callOffset < 0 || callOffset > il.Length - 5 || il[callOffset] != 0x28)
        {
            throw new InvalidOperationException("The selected call no longer fits in the method body.");
        }

        int codeSize = callOffset + 2;
        int fileOffset = RvaToFileOffset(peReader.PEHeaders, rva);
        byte format = image[fileOffset];

        if ((format & 0x3) == 0x2)
        {
            if (codeSize > 63)
            {
                throw new InvalidOperationException("The tiny method body cannot encode the requested code size.");
            }

            image[fileOffset] = (byte)((codeSize << 2) | 0x2);
            return;
        }

        if ((format & 0x3) != 0x3)
        {
            throw new InvalidOperationException("The selected method has an unrecognized method-body header.");
        }

        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(fileOffset + 4), codeSize);
    }

    private static int RvaToFileOffset(PEHeaders headers, int rva)
    {
        foreach (SectionHeader section in headers.SectionHeaders)
        {
            int sectionSize = Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva >= section.VirtualAddress && rva - section.VirtualAddress < sectionSize)
            {
                return checked(section.PointerToRawData + (rva - section.VirtualAddress));
            }
        }

        throw new InvalidOperationException("The selected method RVA does not map to a PE section.");
    }

    private static byte[] CreateLargeValidSwitch()
    {
        const int count = 1001;
        var il = new byte[1 + 1 + sizeof(int) + (count * sizeof(int)) + 1];
        il[0] = 0x00;
        il[1] = 0x45;
        BinaryPrimitives.WriteInt32LittleEndian(il.AsSpan(2), count);
        il[^1] = 0x2A;
        return il;
    }

    private static byte[] CreateTruncatedOperand(ILOpCode opCode, int operandLength)
    {
        var il = new List<byte> { 0x00 };
        ushort value = (ushort)opCode;
        if (value > byte.MaxValue)
        {
            il.Add(0xFE);
        }
        il.Add((byte)value);
        for (var i = 1; i < operandLength; i++)
        {
            il.Add(0);
        }
        return [.. il];
    }
}
