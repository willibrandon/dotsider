// A ReadyToRun-published console app for testing dotsider's R2R method-to-native correlation.
// The shapes below pin R2R read rules: ordinary methods (MethodDefEntryPoints), overloads,
// an async state machine (funclets / hot-cold ranges), and generic instantiations over both a
// value type and a reference type (InstanceMethodEntryPoints).
using System.Runtime.CompilerServices;

Console.WriteLine("Hello from ReadyToRun!");

var greeter = new Greeter("dotsider");
Console.WriteLine(greeter.Greet("world"));
Console.WriteLine(greeter.Greet(42));
Console.WriteLine(new Box<int>(7).Describe());
Console.WriteLine(new Box<string>("seven").Describe());
Console.WriteLine(Util.Identity(1));
Console.WriteLine(Util.Identity("one"));
await Task.Yield();

/// <summary>A plain type with overloads and a property, exercising ordinary method entry points.</summary>
internal sealed class Greeter(string name)
{
    /// <summary>The greeter's name.</summary>
    public string Name { [MethodImpl(MethodImplOptions.NoInlining)] get; } = name;

    /// <summary>Greets a named recipient.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(string who) => $"Hello, {who}, from {Name}";

    /// <summary>Greets by number — an overload sharing the name <c>Greet</c>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(int who) => $"Hello, #{who}, from {Name}";
}

/// <summary>A generic type whose <c>int</c> and <c>string</c> instantiations exercise the instance-method entry points.</summary>
internal sealed class Box<T>(T value)
{
    /// <summary>Describes the boxed value.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Describe() => $"Box<{typeof(T).Name}>({value})";
}

/// <summary>A static generic method with two instantiations.</summary>
internal static class Util
{
    /// <summary>Returns its argument unchanged.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T Identity<T>(T value) => value;
}
