using Dotsider.Core.Protocol;
using Dotsider.Infrastructure;
using System.CommandLine;
using System.Text.Json;

namespace Dotsider.Commands;

/// <summary>
/// Sessions command group: discover and interact with running dotsider instances.
/// </summary>
internal static class SessionsCommand
{
    /// <summary>Tab index → display name mapping (matches TabId constants).</summary>
    private static readonly string[] s_tabNames =
        ["General", "PE/Metadata", "IL / Native", "Strings", "Hex Dump", "Dep Graph", "Size Map", "Dynamic"];

    /// <summary>Returns a human-readable tab name for a numeric tab index.</summary>
    private static string FormatTabName(JsonElement? element, JsonElement? labelElement = null)
    {
        if (element is null) return "unknown";
        if (element.Value.ValueKind == JsonValueKind.Number)
        {
            var tab = element.Value.GetInt32();
            var label = labelElement?.ValueKind == JsonValueKind.String
                ? labelElement.Value.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(label))
                return $"{label} ({tab})";

            var idx = tab - 1;
            return idx >= 0 && idx < s_tabNames.Length ? $"{s_tabNames[idx]} ({tab})" : tab.ToString();
        }

        return element.Value.GetDisplayString("unknown");
    }

    private static readonly Argument<int> s_pidArg = new("pid")
    {
        Description = "Process ID of the dotsider instance"
    };

    /// <summary>
    /// Creates the "sessions" command with all subcommands (list, info, view, navigate, capture, trace).
    /// </summary>
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command("sessions", "Manage running dotsider instances");

        command.Subcommands.Add(CreateListCommand(jsonOption));
        command.Subcommands.Add(CreateInfoCommand(jsonOption));
        command.Subcommands.Add(CreateViewCommand(jsonOption));
        command.Subcommands.Add(CreateNavigateCommand(jsonOption));
        command.Subcommands.Add(CreateCaptureCommand(jsonOption));
        command.Subcommands.Add(CreateTraceCommand(jsonOption));

        return command;
    }

    /// <summary>
    /// Sends a DotsiderRequest to a session identified by PID.
    /// Returns the response or writes an error and returns null.
    /// </summary>
    private static async Task<DotsiderResponse?> SendToSession(
        int pid, DotsiderRequest request, CancellationToken ct)
    {
        var socketPath = SessionDiscovery.GetDotsiderSocketPath(pid);
        if (!File.Exists(socketPath))
        {
            OutputFormatter.WriteError($"Error: No dotsider instance found for PID {pid}");
            return null;
        }

        try
        {
            var response = await DotsiderClient.SendAsync(socketPath, request, ct);
            if (!response.Success)
            {
                OutputFormatter.WriteError($"Error: {response.Error}");
                return null;
            }

            return response;
        }
        catch (Exception ex)
        {
            OutputFormatter.WriteError($"Error: Could not connect to PID {pid}: {ex.Message}");
            return null;
        }
    }

    private static Command CreateListCommand(Option<bool> jsonOption)
    {
        var command = new Command("list", "List running dotsider instances");

        command.SetAction(async (parseResult, ct) =>
        {
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };
            var sessions = SessionDiscovery.Scan();
            if (sessions.Count == 0)
            {
                formatter.WriteLine("No running dotsider instances found.");
                return 0;
            }

            var rows = new List<string[]>();
            var reachable = new List<CliDiscoveredSessionPayload>();

            foreach (var session in sessions)
            {
                var info = await DotsiderClient.TryProbeAsync(session.SocketPath, ct);
                if (info?.Success == true)
                {
                    var data = info.Data;
                    var mode = data?.GetPropertyOrNull("mode")?.GetString() ?? "standard";
                    var fileName = data?.GetPropertyOrNull("fileName")?.GetString() ?? "unknown";
                    var assemblyName = data?.GetPropertyOrNull("assemblyName")?.GetString() ?? "";

                    rows.Add([session.Pid.ToString(), mode, fileName, assemblyName]);
                    reachable.Add(new CliDiscoveredSessionPayload(
                        session.Pid,
                        session.SocketPath,
                        mode,
                        fileName,
                        assemblyName));
                }
            }

            if (rows.Count == 0)
            {
                formatter.WriteLine("No reachable dotsider instances found.");
                return 0;
            }

            if (json)
                formatter.WriteJson(reachable);
            else
                formatter.WriteTable(["PID", "Mode", "File", "Assembly"], rows);

            return 0;
        });

        return command;
    }

    private static Command CreateInfoCommand(Option<bool> jsonOption)
    {
        var command = new Command("info", "Show assembly info and current view for a running instance")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var infoResponse = await SendToSession(pid,
                new DotsiderRequest { Method = "assembly-info" }, ct);
            if (infoResponse is null) return 1;

            var viewResponse = await SendToSession(pid,
                new DotsiderRequest { Method = "get-current-view" }, ct);
            if (viewResponse is null) return 1;

            if (json)
            {
                formatter.WriteJson(new CliSessionInfoPayload(
                    infoResponse.Data, viewResponse.Data));
            }
            else
            {
                var info = infoResponse.Data;
                var view = viewResponse.Data;

                formatter.WriteLine($"PID:        {pid}");
                formatter.WriteLine($"File:       {info?.GetPropertyOrNull("fileName")?.GetString() ?? "unknown"}");
                formatter.WriteLine($"Assembly:   {info?.GetPropertyOrNull("assemblyName")?.GetString() ?? ""}");
                formatter.WriteLine($"Version:    {info?.GetPropertyOrNull("assemblyVersion")?.GetString() ?? ""}");
                formatter.WriteLine($"Framework:  {info?.GetPropertyOrNull("targetFramework")?.GetString() ?? ""}");
                formatter.WriteLine($"Arch:       {info?.GetPropertyOrNull("architecture")?.GetString() ?? ""}");
                formatter.WriteLine($"Types:      {info?.GetPropertyOrNull("typeCount")?.GetInt32() ?? 0}");
                formatter.WriteLine($"Methods:    {info?.GetPropertyOrNull("methodCount")?.GetInt32() ?? 0}");
                formatter.WriteLine("");
                var displayName = info?.GetPropertyOrNull("displayName")?.GetString();
                if (displayName is not null && displayName != info?.GetPropertyOrNull("fileName")?.GetString())
                    formatter.WriteLine($"Display:    {displayName} (from bundle)");

                var runtimePack = info?.GetPropertyOrNull("preferredRuntimePack")?.GetString();
                if (runtimePack is not null)
                    formatter.WriteLine($"Runtime Pack: {runtimePack}");

                var hasEntryPoint = view?.GetPropertyOrNull("hasEntryPoint")?.GetBoolean();
                var isNativeAot = view?.GetPropertyOrNull("isNativeAot")?.GetBoolean();
                var isNetFx = view?.GetPropertyOrNull("isNetFramework")?.GetBoolean();
                var traceable = (hasEntryPoint == true || isNativeAot == true) && isNetFx != true;
                formatter.WriteLine($"Traceable:  {(traceable ? "yes" : "no")}");

                formatter.WriteLine("");
                formatter.WriteLine($"Tab:        {FormatTabName(view?.GetPropertyOrNull("tab"), view?.GetPropertyOrNull("tabLabel"))}");
                formatter.WriteLine($"Tracer:     {view?.GetPropertyOrNull("tracerState")?.GetDisplayString("none") ?? "none"}");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateViewCommand(Option<bool> jsonOption)
    {
        var command = new Command("view", "Show current view state of a running instance")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "get-current-view" }, ct);
            if (response is null) return 1;

            if (json)
            {
                formatter.WriteJson(response.Data);
            }
            else
            {
                var data = response.Data;
                formatter.WriteLine($"Tab:           {FormatTabName(data?.GetPropertyOrNull("tab"), data?.GetPropertyOrNull("tabLabel"))}");
                formatter.WriteLine($"PE Sub-tab:    {data?.GetPropertyOrNull("peSubTab")?.GetDisplayString() ?? ""}");
                formatter.WriteLine($"Dynamic Sub:   {data?.GetPropertyOrNull("dynamicSubTab")?.GetDisplayString() ?? ""}");
                formatter.WriteLine($"Assembly:      {data?.GetPropertyOrNull("assemblyPath")?.GetDisplayString() ?? ""}");
                formatter.WriteLine($"Nav Depth:     {data?.GetPropertyOrNull("navigationDepth")?.GetDisplayString("0") ?? "0"}");
                formatter.WriteLine($"Tracer:        {data?.GetPropertyOrNull("tracerState")?.GetDisplayString("none") ?? "none"}");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateNavigateCommand(Option<bool> jsonOption)
    {
        var tabArg = new Argument<int>("tab")
        {
            Description = "Tab to navigate to (1=General, 2=PE/Metadata, 3=IL / Native, 4=Strings, 5=Hex Dump, 6=Dep Graph, 7=Size Map, 8=Dynamic)"
        };

        var command = new Command("navigate", "Switch to a specific tab in a running instance")
        {
            s_pidArg,
            tabArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var tabId = parseResult.GetValue(tabArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            if (tabId is < 1 or > 8)
            {
                OutputFormatter.WriteError($"Error: Tab must be 1-8, got {tabId}");
                return 1;
            }

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "navigate", TabId = tabId }, ct);
            if (response is null) return 1;

            if (json)
                formatter.WriteJson(response.Data);
            else
            {
                var data = response.Data;
                formatter.WriteLine(data?.GetPropertyOrNull("message")?.GetString()
                    ?? $"Navigated to tab {tabId}");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateCaptureCommand(Option<bool> jsonOption)
    {
        var command = new Command("capture", "Capture the TUI screen as plain text")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var hex1bSocket = SessionDiscovery.GetHex1bSocketPath(pid);
            if (!File.Exists(hex1bSocket))
            {
                OutputFormatter.WriteError($"Error: No hex1b diagnostics socket found for PID {pid}");
                return 1;
            }

            try
            {
                var requestJson = "{\"method\":\"capture\",\"format\":\"text\"}";

                var responseJson = await DotsiderClient.SendRawAsync(hex1bSocket, requestJson, ct);

                var response = JsonSerializer.Deserialize(responseJson, DotsiderAppJsonContext.Application.JsonElement);
                if (response.TryGetProperty("success", out var success) && success.GetBoolean()
                    && response.TryGetProperty("data", out var data))
                {
                    var content = data.GetString() ?? "";
                    if (json)
                        formatter.WriteJson(new CliCapturePayload(content));
                    else
                        formatter.WriteBlock(content);
                }
                else
                {
                    var error = response.TryGetProperty("error", out var errProp)
                        ? errProp.GetString() : "Unknown error";
                    OutputFormatter.WriteError($"Error: {error}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                OutputFormatter.WriteError($"Error: Could not capture from PID {pid}: {ex.Message}");
                return 1;
            }

            return 0;
        });

        return command;
    }

    private static Command CreateTraceCommand(Option<bool> jsonOption)
    {
        var command = new Command("trace", "Trace commands for a running instance");

        command.Subcommands.Add(CreateTraceEventsCommand(jsonOption));
        command.Subcommands.Add(CreateTraceCountersCommand(jsonOption));
        command.Subcommands.Add(CreateTraceOutputCommand(jsonOption));
        command.Subcommands.Add(CreateTraceStartCommand(jsonOption));
        command.Subcommands.Add(CreateTraceStopCommand(jsonOption));

        return command;
    }

    private static Command CreateTraceEventsCommand(Option<bool> jsonOption)
    {
        var categoryOption = new Option<string?>("--category")
        {
            Description = "Filter by event category (jit, gc, exception, loader, contention)"
        };
        var maxOption = new Option<int?>("--max")
        {
            Description = "Maximum number of events to return"
        };

        var command = new Command("events", "Get trace events from a running instance")
        {
            s_pidArg,
            categoryOption,
            maxOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            var category = parseResult.GetValue(categoryOption);
            var max = parseResult.GetValue(maxOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest
                {
                    Method = "get-trace-events",
                    CategoryFilter = category,
                    MaxResults = max
                }, ct);
            if (response is null) return 1;

            if (json)
            {
                formatter.WriteJson(response.Data);
            }
            else
            {
                var data = response.Data;
                if (data?.ValueKind == JsonValueKind.Array)
                {
                    foreach (var evt in data.Value.EnumerateArray())
                    {
                        var cat = evt.GetPropertyOrNull("category")?.GetString() ?? "";
                        var name = evt.GetPropertyOrNull("name")?.GetString() ?? "";
                        var detail = evt.GetPropertyOrNull("detail")?.GetString() ?? "";
                        formatter.WriteLine($"[{cat}] {name}: {detail}");
                    }
                }
                else
                {
                    formatter.WriteLine("No trace events available.");
                }
            }

            return 0;
        });

        return command;
    }

    private static Command CreateTraceCountersCommand(Option<bool> jsonOption)
    {
        var command = new Command("counters", "Get performance counters from a running instance")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "get-trace-counters" }, ct);
            if (response is null) return 1;

            if (json)
            {
                formatter.WriteJson(response.Data);
            }
            else
            {
                var data = response.Data;
                if (data?.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in data.Value.EnumerateObject())
                        formatter.WriteLine($"{prop.Name}: {prop.Value}");
                }
                else
                {
                    formatter.WriteLine("No counter data available.");
                }
            }

            return 0;
        });

        return command;
    }

    private static Command CreateTraceOutputCommand(Option<bool> jsonOption)
    {
        var command = new Command("output", "Get process output from a running instance")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "get-process-output" }, ct);
            if (response is null) return 1;

            if (json)
            {
                formatter.WriteJson(response.Data);
            }
            else
            {
                var data = response.Data;
                if (data?.ValueKind == JsonValueKind.Array)
                {
                    foreach (var line in data.Value.EnumerateArray())
                    {
                        var stream = line.GetPropertyOrNull("stream")?.GetString() ?? "out";
                        var text = line.GetPropertyOrNull("text")?.GetString() ?? "";
                        var prefix = stream == "stderr" ? "[err] " : "";
                        formatter.WriteLine($"{prefix}{text}");
                    }
                }
                else
                {
                    formatter.WriteLine("No process output available.");
                }
            }

            return 0;
        });

        return command;
    }

    private static Command CreateTraceStartCommand(Option<bool> jsonOption)
    {
        var argumentsArgument = new Argument<string[]>("arguments")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Literal arguments for the traced process"
        };

        var command = new Command("start", "Start tracing in a running instance")
        {
            s_pidArg,
            argumentsArgument
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            var arguments = parseResult.GetValue(argumentsArgument) ?? [];
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "start-trace", Arguments = arguments },
                ct);
            if (response is null) return 1;

            if (json)
                formatter.WriteJson(response.Data);
            else
            {
                var data = response.Data;
                formatter.WriteLine(data?.GetPropertyOrNull("message")?.GetString()
                    ?? "Trace started");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateTraceStopCommand(Option<bool> jsonOption)
    {
        var command = new Command("stop", "Stop tracing in a running instance")
        {
            s_pidArg
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(s_pidArg);
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };

            var response = await SendToSession(pid,
                new DotsiderRequest { Method = "stop-trace" }, ct);
            if (response is null) return 1;

            if (json)
                formatter.WriteJson(response.Data);
            else
            {
                var data = response.Data;
                formatter.WriteLine(data?.GetPropertyOrNull("message")?.GetString()
                    ?? "Trace stopped");
            }

            return 0;
        });

        return command;
    }
}

