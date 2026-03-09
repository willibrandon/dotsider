using System.CommandLine;
using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Infrastructure;

namespace Dotsider.Commands;

/// <summary>
/// Sessions command group: discover and interact with running dotsider instances.
/// </summary>
internal static class SessionsCommand
{
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command("sessions", "Manage running dotsider instances");

        command.Subcommands.Add(CreateListCommand(jsonOption));
        command.Subcommands.Add(CreateCaptureCommand(jsonOption));
        command.Subcommands.Add(CreateSendKeysCommand());

        return command;
    }

    private static Command CreateListCommand(Option<bool> jsonOption)
    {
        var command = new Command("list", "List running dotsider instances");

        command.SetAction(async (parseResult, ct) =>
        {
            var json = parseResult.GetValue(jsonOption);
            var formatter = new OutputFormatter { JsonMode = json };
            var discovery = new SessionDiscovery();
            var client = new DotsiderClient();

            var sessions = discovery.Scan();
            if (sessions.Count == 0)
            {
                formatter.WriteLine("No running dotsider instances found.");
                return 0;
            }

            var rows = new List<string[]>();
            var reachable = new List<object>();

            foreach (var session in sessions)
            {
                var info = await client.TryProbeAsync(session.SocketPath, ct);
                if (info?.Success == true)
                {
                    var data = info.Data as JsonElement?;
                    var fileName = data?.GetProperty("fileName").GetString() ?? "unknown";
                    var assemblyName = data?.GetProperty("assemblyName").GetString() ?? "";

                    rows.Add([session.Pid.ToString(), fileName, assemblyName]);
                    reachable.Add(new
                    {
                        session.Pid,
                        session.SocketPath,
                        FileName = fileName,
                        AssemblyName = assemblyName
                    });
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
                formatter.WriteTable(["PID", "File", "Assembly"], rows);

            return 0;
        });

        return command;
    }

    private static Command CreateCaptureCommand(Option<bool> jsonOption)
    {
        var pidArg = new Argument<int>("pid") { Description = "PID of the dotsider instance" };
        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "Capture format: text, ansi, html, svg",
            DefaultValueFactory = _ => "text"
        };

        var command = new Command("capture", "Capture screen of a running instance")
        {
            pidArg,
            formatOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(pidArg);
            var format = parseResult.GetValue(formatOption) ?? "text";
            var json = parseResult.GetValue(jsonOption);
            var formatter = new OutputFormatter { JsonMode = json };
            var client = new DotsiderClient();

            var hex1bSocket = SessionDiscovery.GetHex1bSocketPath(pid);
            if (!File.Exists(hex1bSocket))
            {
                formatter.WriteError($"Error: No hex1b socket found for PID {pid}");
                return 1;
            }

            try
            {
                var requestJson = JsonSerializer.Serialize(
                    new { method = "capture", format },
                    DotsiderJsonOptions.Default);

                var responseJson = await client.SendRawAsync(hex1bSocket, requestJson, ct);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("data", out var data))
                    {
                        if (json)
                            formatter.WriteJson(new { Pid = pid, Format = format, Content = data.GetString() });
                        else
                            Console.Write(data.GetString());
                    }

                    return 0;
                }

                var error = response.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
                formatter.WriteError($"Error: {error}");
                return 1;
            }
            catch (Exception ex)
            {
                formatter.WriteError($"Error: Could not connect to PID {pid}: {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateSendKeysCommand()
    {
        var pidArg = new Argument<int>("pid") { Description = "PID of the dotsider instance" };
        var keyOption = new Option<string>("--key", "-k")
        {
            Description = "Key to send (e.g., Enter, Tab, Escape, a-z, F1-F12)",
            Required = true
        };

        var command = new Command("send-keys", "Send keyboard input to a running instance")
        {
            pidArg,
            keyOption
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var pid = parseResult.GetValue(pidArg);
            var key = parseResult.GetValue(keyOption);
            var formatter = new OutputFormatter();
            var client = new DotsiderClient();

            var hex1bSocket = SessionDiscovery.GetHex1bSocketPath(pid);
            if (!File.Exists(hex1bSocket))
            {
                formatter.WriteError($"Error: No hex1b socket found for PID {pid}");
                return 1;
            }

            try
            {
                var requestJson = JsonSerializer.Serialize(
                    new { method = "key", key },
                    DotsiderJsonOptions.Default);

                var responseJson = await client.SendRawAsync(hex1bSocket, requestJson, ct);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (response.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    formatter.WriteLine($"Sent key '{key}' to PID {pid}");
                    return 0;
                }

                var error = response.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
                formatter.WriteError($"Error: {error}");
                return 1;
            }
            catch (Exception ex)
            {
                formatter.WriteError($"Error: Could not connect to PID {pid}: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
