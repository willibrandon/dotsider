// Runtime oracle for NetFxBinder tests. When invoked with `--oracle <path>`, this program
// loads each assembly the binder is expected to resolve and writes a JSON document recording
// what the CLR actually bound to. The dotsider tests compare the binder's NetFxBindResult
// against this JSON to enforce literal CLR accuracy.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace NetFxBindingRedirects
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string? oraclePath = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--oracle")
                {
                    oraclePath = args[i + 1];
                    break;
                }
            }

            var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);

            // Each call below forces a real CLR bind whose outcome (FullName + Location) we
            // capture. Touching members on the loaded assemblies prevents JIT/optimization
            // from eliding the bind.
            Capture(entries, "Newtonsoft.Json", typeof(Newtonsoft.Json.JsonConvert).Assembly);
            Capture(entries, "NetFxBindingRedirects.OldDep",
                typeof(OldDep.OldDepClass).Assembly);
            Capture(entries, "NetFxBindingRedirects.NewDep",
                typeof(NewDep.NewDepClass).Assembly);
            Capture(entries, "System.Drawing",
                typeof(System.Drawing.Color).Assembly);
            Capture(entries, "System.Windows.Forms",
                typeof(System.Windows.Forms.Application).Assembly);
            Capture(entries, "mscorlib", typeof(object).Assembly);
            Capture(entries, "System", typeof(System.Uri).Assembly);

            // Privately-located helpers reached by probing privatePath / codeBase.
            try
            {
                var privAsm = Assembly.Load("NetFxBindingRedirects.PrivatePathLib");
                Capture(entries, "NetFxBindingRedirects.PrivatePathLib", privAsm);
            }
            catch (FileNotFoundException ex)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.PrivatePathLib", ex);
            }

            // <codeBase> is consulted only for strong-named binds with a full identity, so
            // pass the full name. The PKT must match the <assemblyIdentity> in app.config.
            try
            {
                var cbAsm = Assembly.Load(
                    "NetFxBindingRedirects.CodeBaseLib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=e061e779022b0ce6");
                Capture(entries, "NetFxBindingRedirects.CodeBaseLib", cbAsm);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.CodeBaseLib", ex);
            }

            try
            {
                var culAsm = Assembly.Load("CulturedLib");
                Capture(entries, "CulturedLib", culAsm);
            }
            catch (FileNotFoundException ex)
            {
                CaptureFailure(entries, "CulturedLib", ex);
            }

            // Deliberately-broken codeBase: the bind must fail fast. Use the full strong name
            // matching app.config's <assemblyIdentity>, otherwise codeBase is not consulted at all.
            try
            {
                var missingAsm = Assembly.Load(
                    "NetFxBindingRedirects.MissingCodeBase, Version=9.9.9.9, Culture=neutral, PublicKeyToken=0123456789abcdef");
                Capture(entries, "NetFxBindingRedirects.MissingCodeBase", missingAsm);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.MissingCodeBase", ex);
            }

            var json = ToJson(entries);
            if (oraclePath is not null)
                File.WriteAllText(oraclePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            else
                Console.WriteLine(json);

            return 0;
        }

        private static void Capture(IDictionary<string, Entry> map, string key, Assembly asm)
        {
            map[key] = new Entry
            {
                FullName = asm.FullName ?? string.Empty,
                Location = asm.IsDynamic ? string.Empty : asm.Location,
                Loaded = true,
                Error = null,
            };
        }

        private static void CaptureFailure(IDictionary<string, Entry> map, string key, Exception ex)
        {
            map[key] = new Entry
            {
                FullName = string.Empty,
                Location = string.Empty,
                Loaded = false,
                Error = ex.GetType().Name + ": " + ex.Message,
            };
        }

        private static string ToJson(IDictionary<string, Entry> map)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            var first = true;
            foreach (var kv in map)
            {
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("  ").Append(JsonString(kv.Key)).Append(": {\n");
                sb.Append("    \"fullName\": ").Append(JsonString(kv.Value.FullName)).Append(",\n");
                sb.Append("    \"location\": ").Append(JsonString(kv.Value.Location)).Append(",\n");
                sb.Append("    \"loaded\": ").Append(kv.Value.Loaded ? "true" : "false").Append(",\n");
                sb.Append("    \"error\": ").Append(JsonString(kv.Value.Error)).Append('\n');
                sb.Append("  }");
            }
            sb.Append("\n}\n");
            return sb.ToString();
        }

        private static string JsonString(string? s)
        {
            if (s is null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private sealed class Entry
        {
            public string FullName { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public bool Loaded { get; set; }
            public string? Error { get; set; }
        }
    }
}
