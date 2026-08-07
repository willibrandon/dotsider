using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Diagnostics;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Pass the path to a managed executable assembly.");
    return 2;
}

using var tracer = new RuntimeTracer(args[0], [], static () => { });
tracer.Start();

var timeout = Stopwatch.StartNew();
while (timeout.Elapsed < TimeSpan.FromSeconds(45))
{
    if (tracer.ProcessState == TraceProcessState.Error)
    {
        Console.Error.WriteLine(tracer.ErrorMessage);
        return 1;
    }

    if (tracer.GetEvents().Count > 0 && tracer.GetLatestCounters() is not null)
    {
        tracer.Stop();
        Console.WriteLine("Runtime tracing captured events and counters.");
        return 0;
    }

    if (tracer.ProcessState == TraceProcessState.Exited)
    {
        break;
    }

    await Task.Delay(100);
}

Console.Error.WriteLine(
    $"Runtime tracing ended in state {tracer.ProcessState} with " +
    $"{tracer.GetEvents().Count} event(s) and " +
    $"{(tracer.GetLatestCounters() is null ? "no counters" : "counters")}.");
return 1;
