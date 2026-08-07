using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.TraceHost;
using System.Text.Json;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotsider-tracehost <assembly-path> [arguments]");
    return 2;
}

var writeLock = new Lock();

void Publish(TraceHostMessage message)
{
    var json = JsonSerializer.Serialize(message, TraceHostJsonContext.Default.TraceHostMessage);
    lock (writeLock)
    {
        Console.Out.WriteLine(json);
        Console.Out.Flush();
    }
}

using var tracer = new EventPipeRuntimeTracer(
    args[0],
    args[1..],
    static () => { },
    entry => Publish(new TraceHostMessage(TraceHostMessageKind.Event, Event: entry)),
    counters => Publish(new TraceHostMessage(TraceHostMessageKind.Counters, Counters: counters)),
    output => Publish(new TraceHostMessage(TraceHostMessageKind.Output, Output: output)));

var stopTask = Task.Run(() => TraceHostControlChannel.MonitorAsync(Console.In, tracer.Stop));

tracer.Start();

TraceProcessState? lastState = null;
int? lastProcessId = null;
int? lastExitCode = null;
string? lastError = null;

while (true)
{
    var state = tracer.ProcessState;
    var processId = tracer.ProcessId;
    var exitCode = tracer.ExitCode;
    var error = tracer.ErrorMessage;

    if (state != lastState
        || processId != lastProcessId
        || exitCode != lastExitCode
        || !string.Equals(error, lastError, StringComparison.Ordinal))
    {
        Publish(new TraceHostMessage(
            TraceHostMessageKind.Status,
            state,
            processId,
            exitCode,
            error,
            tracer.Elapsed));

        lastState = state;
        lastProcessId = processId;
        lastExitCode = exitCode;
        lastError = error;
    }

    if (state is TraceProcessState.Exited or TraceProcessState.Error)
    {
        break;
    }

    await Task.Delay(25).ConfigureAwait(false);
}

await stopTask.WaitAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
return tracer.ProcessState == TraceProcessState.Error ? 1 : 0;
