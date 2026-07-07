// A browser-wasm fixture for testing dotsider's raw WebAssembly and Webcil support.
// The publish emits dotnet.native.wasm, dotnet.native.js.symbols, and Webcil-wrapped
// managed assembly modules under AppBundle/_framework.
using System.Runtime.CompilerServices;

Console.WriteLine("Hello from Wasm!");

var calculator = new WasmCalculator(3);
Console.WriteLine(calculator.Add(4));
Console.WriteLine(calculator.Describe("dotsider"));

/// <summary>A tiny type whose methods stay visible in the Webcil metadata fixture.</summary>
internal sealed class WasmCalculator(int seed)
{
    /// <summary>Adds the seed to the supplied value.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Add(int value) => seed + value;

    /// <summary>Formats a string through ordinary managed IL.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Describe(string name) => $"{name}:{seed}";
}
