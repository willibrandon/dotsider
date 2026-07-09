// Runtime oracle for the CLR2 NetFxBinder tests. With `--oracle <path>`, this program loads
// each interesting assembly under the live .NET Framework 2.0 / 3.5 CLR and writes a JSON
// document recording what the runtime actually bound to. The dotsider tests compare the
// binder's NetFxBindResult against this JSON to enforce literal CLR accuracy.
//
// SharedDep loads are driven through the UsesSharedV1 / UsesSharedV2 accessor helpers so the
// transitive metadata refs (V1 → SharedDep 1.0.0.0, V2 → SharedDep 2.0.0.0) are exercised
// rather than a standalone Assembly.Load — proving the bindingRedirect collapse on the same
// edge shape the dep graph walks.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace NetFxBindingRedirects.Clr2
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string oraclePath = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--oracle")
                {
                    oraclePath = args[i + 1];
                    break;
                }
            }

            var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);

            // Drive SharedDep through the UsesSharedV* accessors so the transitive metadata
            // edges are what binds. Both calls should land on SharedDep v2.0.0.0 due to the
            // bindingRedirect collapse.
            Capture(entries, "SharedDep_via_UsesV1",
                UsesSharedV1.Accessor.GetSharedAssembly());
            Capture(entries, "SharedDep_via_UsesV2",
                UsesSharedV2.Accessor.GetSharedAssembly());

            Capture(entries, "System.Drawing", typeof(System.Drawing.Color).Assembly);
            Capture(entries, "System.Windows.Forms", typeof(System.Windows.Forms.Application).Assembly);
            Capture(entries, "mscorlib", typeof(object).Assembly);
            Capture(entries, "System", typeof(System.Uri).Assembly);
            // v3.0 framework allowlist coverage — WindowsBase ships in
            // Reference Assemblies\Microsoft\Framework\v3.0 and lives in the GAC.
            Capture(entries, "WindowsBase", typeof(System.Windows.DependencyObject).Assembly);

            // Reached only via probing privatePath="lib".
            try
            {
                var privAsm = Assembly.Load("NetFxBindingRedirects.Clr2.PrivatePathLib");
                Capture(entries, "NetFxBindingRedirects.Clr2.PrivatePathLib", privAsm);
            }
            catch (FileNotFoundException ex)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.Clr2.PrivatePathLib", ex);
            }

            // Reached only via configured codeBase href; full strong name required.
            try
            {
                var cbAsm = Assembly.Load(
                    "NetFxBindingRedirects.Clr2.CodeBaseLib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=d4a9fecb5ef90905");
                Capture(entries, "NetFxBindingRedirects.Clr2.CodeBaseLib", cbAsm);
            }
            catch (Exception ex)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.Clr2.CodeBaseLib", ex);
            }

            // Force the satellite to load (not just the neutral CulturedLib). ResourceManager
            // probes fr/Clr2.CulturedLib.resources.dll under the CLR rules; capture both the
            // neutral assembly AND the satellite under distinct keys.
            //
            // Use reflection so we don't need a compile-time reference to CulturedLib (which
            // would otherwise drag the DLL app-local through the project graph and defeat the
            // probing-only deployment intent).
            try
            {
                var neutral = Assembly.Load("NetFxBindingRedirects.Clr2.CulturedLib");
                Capture(entries, "NetFxBindingRedirects.Clr2.CulturedLib", neutral);

                var fr = new CultureInfo("fr");
                var culturedType = neutral.GetType("NetFxBindingRedirects.Clr2.CulturedLib.CulturedClass", true);
                var greetingMethod = culturedType.GetMethod("Greeting", [typeof(CultureInfo)]);
                var greeting = (string)greetingMethod.Invoke(null, [fr]);

                if (string.IsNullOrEmpty(greeting))
                {
                    CaptureFailure(entries, "NetFxBindingRedirects.Clr2.CulturedLib.resources(fr)",
                        new InvalidOperationException("ResourceManager returned empty fr greeting"));
                }
                else
                {
                    var satellite = neutral.GetSatelliteAssembly(fr);
                    Capture(entries, "NetFxBindingRedirects.Clr2.CulturedLib.resources(fr)", satellite);
                }
            }
            catch (Exception ex)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.Clr2.CulturedLib", ex);
            }

            // Deliberately broken codeBase: must fail fast. Use the full strong name matching
            // app.config's <assemblyIdentity>; otherwise codeBase is not consulted at all.
            try
            {
                var missingAsm = Assembly.Load(
                    "NetFxBindingRedirects.Clr2.MissingCodeBase, Version=9.9.9.9, Culture=neutral, PublicKeyToken=0123456789abcdef");
                Capture(entries, "NetFxBindingRedirects.Clr2.MissingCodeBase", missingAsm);
            }
            catch (Exception ex)
            {
                CaptureFailure(entries, "NetFxBindingRedirects.Clr2.MissingCodeBase", ex);
            }

            var json = ToJson(entries);
            if (oraclePath != null)
                File.WriteAllText(oraclePath, json, new UTF8Encoding(false));
            else
                Console.WriteLine(json);

            return 0;
        }

        private static void Capture(IDictionary<string, Entry> map, string key, Assembly asm)
        {
            // net35 has no Assembly.IsDynamic. Reading Location on a dynamic assembly throws
            // NotSupportedException — catch it instead of dispatching on a missing property.
            string location;
            try { location = asm.Location ?? string.Empty; }
            catch (NotSupportedException) { location = string.Empty; }

            map[key] = new Entry
            {
                FullName = asm.FullName ?? string.Empty,
                Location = location,
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

        private static string JsonString(string s)
        {
            if (s == null) return "null";
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
            public string Error { get; set; }
        }
    }
}
