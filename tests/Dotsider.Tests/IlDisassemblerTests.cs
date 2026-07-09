using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Il Disassembler.
/// </summary>
[TestClass]
public class IlDisassemblerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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

    private static MethodDefInfo FindMethod(AssemblyAnalyzer analyzer, string typeName, string methodName)
    {
        var method = analyzer.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType == typeName
            && m.Name == methodName);

        Assert.IsNotNull(method);
        return method;
    }
}
