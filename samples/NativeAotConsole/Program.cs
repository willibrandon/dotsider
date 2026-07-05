// A NativeAOT-published console app for testing dotsider's Dynamic tab.
// Since .NET 8, NativeAOT supports EventPipe when published with
// EventSourceSupport=true. The Dynamic tab should allow tracing attempts
// for NativeAOT binaries rather than blocking unconditionally.
using System.Runtime.CompilerServices;

Console.WriteLine("Hello from NativeAOT!");

// Correlation fixture surface: each shape below pins one managed↔native join rule
// (see ManagedNativeIndexTests / PreIlcAnalyzerTests).
var greeter = new Greeter("dotsider");
Console.WriteLine(greeter.Greet("world"));
Console.WriteLine(greeter.Greet(42));
Console.WriteLine(Greeter.Describe(7));
Console.WriteLine(Greeter.Describe("seven"));
Console.WriteLine(greeter.Name);

/// <summary>
/// Exercises the managed↔native correlation joins: overloads (shared/ambiguous evidence),
/// a generic with two instantiations (multi-symbol exact), an explicit constructor and
/// property accessor, and a never-called method that ILC trims from the image. Every
/// asserted member is <see cref="MethodImplOptions.NoInlining"/> so it survives as a
/// distinct native symbol.
/// </summary>
internal sealed class Greeter
{
    private readonly string _name;

    // Explicit (non-primary) constructor with a guard clause: the guard keeps it a real
    // method body so [MethodImpl(NoInlining)] can hold .ctor in the native image for the
    // correlation fixture.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Greeter(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _name = name;
    }

    /// <summary>The name this greeter greets as.</summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _name;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(string whom) => $"{_name} greets {whom}";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(int count) => $"{_name} greets {count} times";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Describe<T>(T value) => $"{typeof(T).Name}: {value}";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NeverCalled() => "trimmed away";
}
