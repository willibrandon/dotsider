// A NativeAOT-published console app for testing dotsider's Dynamic tab.
// Since .NET 8, NativeAOT supports EventPipe when published with
// EventSourceSupport=true. The Dynamic tab should allow tracing attempts
// for NativeAOT binaries rather than blocking unconditionally.
Console.WriteLine("Hello from NativeAOT!");
