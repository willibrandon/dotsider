// A .NET Framework 4.8 console app for testing dotsider's Dynamic tab guard.
// EventPipe is not available on .NET Framework, so this assembly should
// trigger a friendly message instead of hanging the tracer.
using System;

namespace NetFxConsole
{
    internal static class Program
    {
        static void Main()
        {
            Console.WriteLine("Hello from .NET Framework 4.8!");
        }
    }
}
