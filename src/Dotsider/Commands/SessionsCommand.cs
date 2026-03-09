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

        return command;
    }

    private static Command CreateListCommand(Option<bool> jsonOption)
    {
        var command = new Command("list", "List running dotsider instances");

        command.SetAction(async (parseResult, ct) =>
        {
            var json = parseResult.GetValue(jsonOption);
            using var formatter = new OutputFormatter { JsonMode = json };
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

}
