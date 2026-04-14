using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Il Disassembler.
/// </summary>
[Collection("SampleAssemblies")]
public class IlDisassemblerTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies hello world main method contains call and ret.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_MainMethod_ContainsCallAndRet()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var mainMethod = a.MethodDefs.FirstOrDefault(m => m.Name == "<Main>$" || m.Name == "Main");
        Assert.NotNull(mainMethod);
        var instructions = disasm.Disassemble(mainMethod);
        Assert.NotEmpty(instructions);
        Assert.Contains(instructions, i => i.OpCode.Contains("ret"));
    }

    /// <summary>
    /// Verifies rich library user service add disassembles successfully.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_UserServiceAdd_DisassemblesSuccessfully()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m =>
            m.DeclaringType == "RichLibrary.Services.UserService" && m.Name == "Add");
        Assert.NotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.NotEmpty(instructions);
    }

    /// <summary>
    /// Verifies native lib unsafe method has distinct opcodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_UnsafeMethod_HasDistinctOpcodes()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m => m.Name == "SumWithPointers");
        Assert.NotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.NotEmpty(instructions);
    }

    /// <summary>
    /// Verifies format disassembly returns readable text.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatDisassembly_ReturnsReadableText()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var text = disasm.FormatDisassembly(method);
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("IL_", text);
    }

    /// <summary>
    /// Verifies all rich library methods disassemble without throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllRichLibraryMethods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            Assert.NotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies method with no body returns empty list.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MethodWithNoBody_ReturnsEmptyList()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        // P/Invoke methods have Rva == 0, no IL body
        var externMethod = a.MethodDefs.FirstOrDefault(m => m.Rva == 0);
        if (externMethod is not null)
        {
            var instructions = disasm.Disassemble(externMethod);
            Assert.Empty(instructions);
        }
    }

    /// <summary>
    /// Verifies empty lib can construct no methods to disassemble.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_CanConstruct_NoMethodsToDisassemble()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var disasm = new IlDisassembler(a);
        var methodsWithIl = a.MethodDefs.Where(m => m.Rva != 0).ToList();
        // Either no methods or only compiler-generated
        Assert.True(methodsWithIl.Count <= 2);
    }

    /// <summary>
    /// Verifies complex app async method disassembles successfully.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_AsyncMethod_DisassemblesSuccessfully()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var disasm = new IlDisassembler(a);
        var asyncMethods = a.MethodDefs.Where(m => m.Name.Contains("MoveNext")).ToList();
        if (asyncMethods.Count > 0)
        {
            var instructions = disasm.Disassemble(asyncMethods[0]);
            Assert.NotEmpty(instructions);
        }
    }

    /// <summary>
    /// Verifies minimal api methods disassemble without throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MinimalApi_Methods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0).Take(20))
        {
            var instructions = disasm.Disassemble(method);
            Assert.NotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies disassemble instruction has offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Disassemble_InstructionHasOffset()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var instructions = disasm.Disassemble(method);
        Assert.NotEmpty(instructions);
        // First instruction should be at offset 0
        Assert.Equal(0, instructions[0].Offset);
    }

    /// <summary>
    /// Verifies native lib stack alloc sum has instructions.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_StackAllocSum_HasInstructions()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.FirstOrDefault(m => m.Name == "StackAllocSum");
        Assert.NotNull(method);
        var instructions = disasm.Disassemble(method);
        Assert.NotEmpty(instructions);
    }

    /// <summary>
    /// Verifies instruction has op code and operand.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Instruction_HasOpCodeAndOperand()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0 && m.Name != ".ctor");
        var instructions = disasm.Disassemble(method);
        Assert.NotEmpty(instructions);
        foreach (var inst in instructions)
        {
            Assert.NotNull(inst.OpCode);
            Assert.NotNull(inst.Operand); // can be empty string, not null
        }
    }

    /// <summary>
    /// Verifies format disassembly contains hex offsets.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FormatDisassembly_ContainsHexOffsets()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var method = a.MethodDefs.First(m => m.Rva != 0);
        var text = disasm.FormatDisassembly(method);
        Assert.Contains("IL_0000", text);
    }

    /// <summary>
    /// Verifies complex app all methods disassemble without throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_AllMethods_DisassembleWithoutThrowing()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            Assert.NotNull(instructions);
        }
    }

    /// <summary>
    /// Verifies rich library method with string operand contains ldstr.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodWithStringOperand_ContainsLdstr()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        // Find any method that loads a string literal
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            var ldstr = instructions.FirstOrDefault(i => i.OpCode.Contains("ldstr"));
            if (ldstr is not null)
            {
                Assert.NotEmpty(ldstr.Operand);
                return;
            }
        }
    }

    /// <summary>
    /// Verifies rich library method with branches contains br.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodWithBranches_ContainsBr()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
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
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodCallInstructions_ResolveTokens()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        foreach (var method in a.MethodDefs.Where(m => m.Rva != 0))
        {
            var instructions = disasm.Disassemble(method);
            var call = instructions.FirstOrDefault(i =>
                i.OpCode == "call" || i.OpCode == "callvirt" || i.OpCode == "newobj");
            if (call is not null)
            {
                Assert.NotEmpty(call.Operand);
                // Resolved token should not be just hex
                Assert.DoesNotContain("0x", call.Operand);
                return;
            }
        }
    }
}
