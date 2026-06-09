using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JJSploitPremium
{
    public sealed class ScriptTabData
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string FilePath { get; set; }
    }

    public static class ScriptWorkspace
    {
        private static readonly string WorkspacePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workspace", "tabs.json");

        public static List<ScriptTabData> Load()
        {
            try
            {
                if (!File.Exists(WorkspacePath))
                    return DefaultTabs();

                string json = File.ReadAllText(WorkspacePath);
                return ParseTabs(json);
            }
            catch
            {
                return DefaultTabs();
            }
        }

        public static void Save(IReadOnlyList<ScriptTabData> tabs)
        {
            try
            {
                string dir = Path.GetDirectoryName(WorkspacePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(WorkspacePath, SerializeTabs(tabs), Encoding.UTF8);
            }
            catch { }
        }

        private static List<ScriptTabData> DefaultTabs()
        {
            return new List<ScriptTabData>
            {
                new ScriptTabData
                {
                    Title = "Script 1",
                    Content = "-- JJSploit Premium\n-- Write your Luau code here\n\nprint('Hello World!')"
                }
            };
        }

        private static string SerializeTabs(IReadOnlyList<ScriptTabData> tabs)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < tabs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var tab = tabs[i];
                sb.Append('{');
                sb.Append("\"Title\":").Append(JsonString(tab.Title ?? "Script"));
                sb.Append(",\"Content\":").Append(JsonString(tab.Content ?? string.Empty));
                sb.Append(",\"FilePath\":").Append(JsonString(tab.FilePath ?? string.Empty));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static List<ScriptTabData> ParseTabs(string json)
        {
            var tabs = new List<ScriptTabData>();
            if (string.IsNullOrWhiteSpace(json))
                return DefaultTabs();

            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '[')
                return DefaultTabs();
            i++;

            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']') break;
                if (i >= json.Length || json[i] != '{') break;

                tabs.Add(ParseObject(json, ref i));

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }

            return tabs.Count > 0 ? tabs : DefaultTabs();
        }

        private static ScriptTabData ParseObject(string json, ref int i)
        {
            var tab = new ScriptTabData();
            i++; // skip {

            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}') { i++; break; }

                string key = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ':') i++;
                SkipWs(json, ref i);

                string val = ReadJsonString(json, ref i);

                if (key == "Title") tab.Title = val;
                else if (key == "Content") tab.Content = val;
                else if (key == "FilePath" && !string.IsNullOrEmpty(val)) tab.FilePath = val;

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }

            if (string.IsNullOrWhiteSpace(tab.Title))
                tab.Title = "Script";

            return tab;
        }

        private static void SkipWs(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        }

        private static string ReadJsonString(string json, ref int i)
        {
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '"') return string.Empty;

            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"') break;
                if (c == '\\' && i < json.Length)
                {
                    char esc = json[i++];
                    switch (esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'u':
                            if (i + 3 < json.Length &&
                                int.TryParse(json.Substring(i, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
