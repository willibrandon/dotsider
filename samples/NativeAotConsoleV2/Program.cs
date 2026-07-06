// FROZEN TEST FIXTURE — do not modify without updating Dotsider.Tests.
// V2 of NativeAotConsole with deliberate size deltas against the V1 mstat:
//   grown  — Greeter.Greet(string) gained a many-branch body (Greet(int) is untouched,
//            so the overload pair proves signature-keyed diff identity),
//   removed — the Name property and its call site are gone (get_Name exists only in V1),
//   added  — the NativeAotConsole.Telemetry namespace (Telemetry.cs) and the embedded
//            Payload.txt manifest resource exist only in V2.
using System.Reflection;
using System.Runtime.CompilerServices;
using NativeAotConsole.Telemetry;

Console.WriteLine("Hello from NativeAOT!");

var greeter = new Greeter("dotsider");
Console.WriteLine(greeter.Greet("world"));
Console.WriteLine(greeter.Greet(42));
Console.WriteLine(Greeter.Describe(7));
Console.WriteLine(Greeter.Describe("seven"));

var metrics = new MetricsCollector();
metrics.Record("greetings", 5);
metrics.Record("startup", 1);
Console.WriteLine(metrics.Summarize());

using var payload = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("NativeAotConsole.Payload.txt");
Console.WriteLine($"Payload bytes: {payload?.Length ?? 0}");

/// <summary>
/// The V2 shape of the correlation fixture class. Relative to V1: <c>Greet(string)</c> grew a
/// many-branch body, the <c>Name</c> property was removed, and everything else is unchanged.
/// Every asserted member is <see cref="MethodImplOptions.NoInlining"/> so it survives as a
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(string whom) => whom switch
    {
        "world" => $"{_name} greets the whole world with unreserved enthusiasm",
        "team" => $"{_name} greets the team gathered around the standup board",
        "friend" => $"{_name} greets a dear friend after far too long apart",
        "stranger" => $"{_name} greets a stranger with polite curiosity",
        "reviewer" => $"{_name} greets the reviewer reading every changed line",
        "compiler" => $"{_name} greets the compiler that trims the unreachable",
        "linker" => $"{_name} greets the linker folding identical bodies",
        "runtime" => $"{_name} greets the runtime that never JITs a thing",
        "profiler" => $"{_name} greets the profiler counting every byte",
        "tester" => $"{_name} greets the tester asserting on real fixtures",
        "morning" => $"{_name} greets the morning with a fresh publish",
        "evening" => $"{_name} greets the evening with a green pipeline",
        "ocean" => $"{_name} greets the ocean of generic instantiations",
        "mountain" => $"{_name} greets the mountain of frozen literals",
        "universe" => $"{_name} greets the universe, dehydrated and rehydrated",
        _ => $"{_name} greets {whom}",
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Greet(int count) => $"{_name} greets {count} times";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Describe<T>(T value) => $"{typeof(T).Name}: {value}";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NeverCalled() => "trimmed away";
}
