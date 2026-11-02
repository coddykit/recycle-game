// CoddyKit (Unity) — small JSON reader/writer for the kit's own state files and the ads-config payload.
// Engine-free (CoddyKit.Core, noEngineReferences). Objects -> Dictionary<string, object> (insertion order kept),
// arrays -> List<object>, numbers -> long when integral and in range, else double; true/false -> bool; null -> null.
// JsonUtility cannot read dictionaries and Newtonsoft is a dependency we do not need for a few KB of config.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CoddyKit
{
    public static class MiniJson
    {
        /// <summary>Parses JSON text; throws FormatException on bad input.</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("null text");
            var p = new Parser(text);
            p.SkipWs();
            object v = p.Value(0);
            p.SkipWs();
            if (!p.End) throw new FormatException("trailing characters at " + p.Pos);
            return v;
        }

        /// <summary>Parses JSON text; null when it is not valid JSON (never throws).</summary>
        public static object TryParse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return Parse(text); } catch (FormatException) { return null; }
        }

        /// <summary>The parsed object when the text is a JSON object, else null.</summary>
        public static Dictionary<string, object> TryParseObject(string text)
        {
            return TryParse(text) as Dictionary<string, object>;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ typed getters (missing / wrong type -> default)

        public static object Get(object o, string key)
        {
            return o is Dictionary<string, object> d && key != null && d.TryGetValue(key, out var v) ? v : null;
        }

        public static Dictionary<string, object> Obj(object o, string key) => Get(o, key) as Dictionary<string, object>;

        public static List<object> Arr(object o, string key) => Get(o, key) as List<object>;

        public static string Str(object o, string key, string def = "")
        {
            object v = Get(o, key);
            if (v == null) return def;
            if (v is string s) return s;
            if (v is bool b) return b ? "true" : "false";
            if (v is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            return def;
        }

        public static double Num(object v, double def = 0.0)
        {
            switch (v)
            {
                case long l: return l;
                case int i: return i;
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? def : d;
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? def : f;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p): return p;
                default: return def;
            }
        }

        public static double Num(object o, string key, double def = 0.0) => Num(Get(o, key), def);

        public static long Long(object o, string key, long def = 0)
        {
            object v = Get(o, key);
            if (v == null) return def;
            double d = Num(v, double.NaN);
            if (double.IsNaN(d)) return def;
            if (d > long.MaxValue || d < long.MinValue) return def;
            return (long)Math.Floor(d);
        }

        public static int Int(object o, string key, int def = 0)
        {
            long l = Long(o, key, def);
            if (l > int.MaxValue) return int.MaxValue;
            if (l < int.MinValue) return int.MinValue;
            return (int)l;
        }

        public static bool Bool(object o, string key, bool def = false)
        {
            object v = Get(o, key);
            return v is bool b ? b : def;
        }

        // ------------------------------------------------------------------ writer

        static void WriteValue(StringBuilder sb, object v, int depth)
        {
            if (depth > 64) throw new FormatException("nesting too deep");
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: WriteDouble(sb, f); return;
                case double d: WriteDouble(sb, d); return;
                case IDictionary<string, object> map:
                    {
                        sb.Append('{');
                        bool first = true;
                        foreach (var kv in map)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteString(sb, kv.Key);
                            sb.Append(':');
                            WriteValue(sb, kv.Value, depth + 1);
                        }
                        sb.Append('}');
                        return;
                    }
                case System.Collections.IEnumerable list:
                    {
                        sb.Append('[');
                        bool first = true;
                        foreach (object item in list)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteValue(sb, item, depth + 1);
                        }
                        sb.Append(']');
                        return;
                    }
                default:
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                    return;
            }
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            if (d == Math.Floor(d) && Math.Abs(d) < 9007199254740992.0)
                sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ parser

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string text) { s = text; }

            public int Pos => i;
            public bool End => i >= s.Length;

            public void SkipWs()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿')) i++;
            }

            public object Value(int depth)
            {
                if (depth > 64) throw new FormatException("nesting too deep");
                SkipWs();
                if (End) throw new FormatException("unexpected end");
                char c = s[i];
                switch (c)
                {
                    case '{': return Object(depth);
                    case '[': return Array(depth);
                    case '"': return String();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Number();
                        throw new FormatException("unexpected '" + c + "' at " + i);
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("expected " + word + " at " + i);
                i += word.Length;
            }

            Dictionary<string, object> Object(int depth)
            {
                var d = new Dictionary<string, object>();
                i++; // {
                SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (End || s[i] != '"') throw new FormatException("expected key at " + i);
                    string key = String();
                    SkipWs();
                    if (End || s[i] != ':') throw new FormatException("expected ':' at " + i);
                    i++;
                    d[key] = Value(depth + 1);
                    SkipWs();
                    if (End) throw new FormatException("unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("expected ',' or '}' at " + i);
                }
            }

            List<object> Array(int depth)
            {
                var a = new List<object>();
                i++; // [
                SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (true)
                {
                    a.Add(Value(depth + 1));
                    SkipWs();
                    if (End) throw new FormatException("unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return a; }
                    throw new FormatException("expected ',' or ']' at " + i);
                }
            }

            string String()
            {
                i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw new FormatException("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw new FormatException("bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("bad \\u escape");
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                                throw new FormatException("bad \\u escape");
                            sb.Append((char)cp);
                            i += 4;
                            break;
                        default: throw new FormatException("bad escape \\" + e);
                    }
                }
            }

            object Number()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                bool frac = false;
                if (i < s.Length && s[i] == '.') { frac = true; i++; while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    frac = true;
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                }
                string t = s.Substring(start, i - start);
                if (t == "-" || t.Length == 0) throw new FormatException("bad number at " + start);
                if (!frac && long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw new FormatException("bad number at " + start);
            }
        }
    }
}
