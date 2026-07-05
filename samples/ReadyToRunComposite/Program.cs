// A composite ReadyToRun console: its native code and its component library's native code both
// live in the composite executable, while each component .dll carries only metadata plus an
// OwnerCompositeExecutable pointer. Exercises composite resolution in both directions.
using ReadyToRunComponentLib;

Console.WriteLine("Hello from composite ReadyToRun!");
Console.WriteLine(Calculator.Add(2, 3));
Console.WriteLine(Calculator.Multiply(2, 3));
