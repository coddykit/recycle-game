// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VoltCity.Sim
{
    /// <summary>
    /// Minimal JSON reader / writer for data files and saves. Objects are Dictionary&lt;string, object&gt; (insertion
    /// order kept by the writer through JObj), arrays List&lt;object&gt;, integral numbers long (so integer state round-trips
    /// exactly — LL Pit Wall P1: JSON floats do not), other numbers double, plus string / bool / null.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("null json");
            var p = new Parser(text);
            p.SkipWs();
            var v = p.Value();
            p.SkipWs();
            if (!p.End) throw new FormatException("trailing characters at " + p.Pos);
            return v;
        }

        /// <summary>Parse without throwing: null on any error.</summary>
        public static object TryParse(string text)
        {
            try { return Parse(text); } catch (Exception) { return null; }
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("0"); return; }
                    if (d == Math.Floor(d) && Math.Abs(d) < 9.0e15) { sb.Append(((long)d).ToString(CultureInfo.InvariantCulture)); sb.Append(".0"); return; }
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                case float f: WriteValue(sb, (double)f); return;
                case JObj o: WriteObject(sb, o.Keys, k => o[k]); return;
                case IDictionary<string, object> dict: WriteObject(sb, dict.Keys, k => dict[k]); return;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool first = true;
                    foreach (var x in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, x);
                    }
                    sb.Append(']');
                    return;
                default:
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                    return;
            }
        }

        static void WriteObject(StringBuilder sb, IEnumerable<string> keys, Func<string, object> get)
        {
            sb.Append('{');
            bool first = true;
            foreach (var k in keys)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, k);
                sb.Append(':');
                WriteValue(sb, get(k));
            }
            sb.Append('}');
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
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string t;
            int i;
            public Parser(string text) { t = text; }
            public bool End => i >= t.Length;
            public int Pos => i;

            public void SkipWs()
            {
                while (i < t.Length && (t[i] == ' ' || t[i] == '\t' || t[i] == '\n' || t[i] == '\r')) i++;
            }

            public object Value()
            {
                if (i >= t.Length) throw new FormatException("unexpected end");
                char c = t[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == 't' && Match("true")) return true;
                if (c == 'f' && Match("false")) return false;
                if (c == 'n' && Match("null")) return null;
                return Num();
            }

            bool Match(string w)
            {
                if (string.CompareOrdinal(t, i, w, 0, w.Length) != 0) throw new FormatException("bad literal at " + i);
                i += w.Length;
                return true;
            }

            JObj Obj()
            {
                var o = new JObj();
                i++;
                SkipWs();
                if (i < t.Length && t[i] == '}') { i++; return o; }
                while (true)
                {
                    SkipWs();
                    if (i >= t.Length || t[i] != '"') throw new FormatException("key expected at " + i);
                    string k = Str();
                    SkipWs();
                    if (i >= t.Length || t[i] != ':') throw new FormatException("colon expected at " + i);
                    i++;
                    SkipWs();
                    o[k] = Value();
                    SkipWs();
                    if (i >= t.Length) throw new FormatException("unexpected end in object");
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == '}') { i++; return o; }
                    throw new FormatException("comma expected at " + i);
                }
            }

            List<object> Arr()
            {
                var a = new List<object>();
                i++;
                SkipWs();
                if (i < t.Length && t[i] == ']') { i++; return a; }
                while (true)
                {
                    SkipWs();
                    a.Add(Value());
                    SkipWs();
                    if (i >= t.Length) throw new FormatException("unexpected end in array");
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == ']') { i++; return a; }
                    throw new FormatException("comma expected at " + i);
                }
            }

            string Str()
            {
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= t.Length) throw new FormatException("unterminated string");
                    char c = t[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= t.Length) throw new FormatException("bad escape");
                    char e = t[i++];
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
                            if (i + 4 > t.Length) throw new FormatException("bad unicode escape");
                            sb.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException("bad escape");
                    }
                }
            }

            object Num()
            {
                int start = i;
                bool real = false;
                if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
                while (i < t.Length)
                {
                    char c = t[i];
                    if (c >= '0' && c <= '9') { i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+') { real = true; i++; continue; }
                    break;
                }
                string s = t.Substring(start, i - start);
                if (s.Length == 0) throw new FormatException("value expected at " + start);
                if (!real && long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw new FormatException("bad number at " + start);
            }
        }
    }

    /// <summary>JSON object that keeps insertion order (deterministic saves / hashes).</summary>
    public sealed class JObj : IDictionary<string, object>
    {
        readonly List<string> order = new List<string>();
        readonly Dictionary<string, object> map = new Dictionary<string, object>();

        public object this[string key]
        {
            get => map[key];
            set
            {
                if (!map.ContainsKey(key)) order.Add(key);
                map[key] = value;
            }
        }

        public ICollection<string> Keys => order.AsReadOnly();
        public ICollection<object> Values { get { var l = new List<object>(); foreach (var k in order) l.Add(map[k]); return l; } }
        public int Count => order.Count;
        public bool IsReadOnly => false;
        public void Add(string key, object value) { if (map.ContainsKey(key)) throw new ArgumentException("duplicate key " + key); this[key] = value; }
        public void Add(KeyValuePair<string, object> item) => Add(item.Key, item.Value);
        public void Clear() { order.Clear(); map.Clear(); }
        public bool Contains(KeyValuePair<string, object> item) => map.ContainsKey(item.Key) && Equals(map[item.Key], item.Value);
        public bool ContainsKey(string key) => map.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) { foreach (var k in order) array[arrayIndex++] = new KeyValuePair<string, object>(k, map[k]); }
        public bool Remove(string key) { if (!map.Remove(key)) return false; order.Remove(key); return true; }
        public bool Remove(KeyValuePair<string, object> item) => Remove(item.Key);
        public bool TryGetValue(string key, out object value) => map.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() { foreach (var k in order) yield return new KeyValuePair<string, object>(k, map[k]); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Typed, non-throwing readers over parsed JSON (data loading and the save codec).</summary>
    public static class J
    {
        public static JObj Obj(object o, string key)
        {
            if (o is JObj j && j.TryGetValue(key, out var v) && v is JObj r) return r;
            return null;
        }

        public static List<object> Arr(object o, string key)
        {
            if (o is JObj j && j.TryGetValue(key, out var v) && v is List<object> r) return r;
            return null;
        }

        public static bool Has(object o, string key) => o is JObj j && j.ContainsKey(key);

        public static object Get(object o, string key) => o is JObj j && j.TryGetValue(key, out var v) ? v : null;

        public static bool IsInt(object v) => v is long || v is int || (v is double d && d == Math.Floor(d) && Math.Abs(d) <= 9007199254740991.0);

        public static bool IsNum(object v) => v is long || v is int || (v is double d && !double.IsNaN(d) && !double.IsInfinity(d));

        public static long L(object v, long def = 0)
        {
            switch (v)
            {
                case long l: return l;
                case int i: return i;
                case double d when !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) <= 9.0e18: return (long)d;
                default: return def;
            }
        }

        public static int I(object v, int def = 0)
        {
            long l = L(v, def);
            if (l > int.MaxValue || l < int.MinValue) return def;
            return (int)l;
        }

        public static double D(object v, double def = 0.0)
        {
            switch (v)
            {
                case long l: return l;
                case int i: return i;
                case double d: return d;
                default: return def;
            }
        }

        public static string Str(object v, string def = "") => v is string s ? s : def;

        public static bool B(object v, bool def = false) => v is bool b ? b : def;

        public static long L(object o, string key, long def = 0) => L(Get(o, key), def);
        public static int I(object o, string key, int def = 0) => I(Get(o, key), def);
        public static double D(object o, string key, double def = 0.0) => D(Get(o, key), def);
        public static string S(object o, string key, string def = "") => Str(Get(o, key), def);
        public static bool B(object o, string key, bool def = false) => B(Get(o, key), def);

        public static int[] IntArr(object o, string key)
        {
            var a = Arr(o, key);
            if (a == null) return new int[0];
            var r = new int[a.Count];
            for (int k = 0; k < a.Count; k++) r[k] = I(a[k]);
            return r;
        }

        public static double[] DblArr(object o, string key)
        {
            var a = Arr(o, key);
            if (a == null) return new double[0];
            var r = new double[a.Count];
            for (int k = 0; k < a.Count; k++) r[k] = D(a[k]);
            return r;
        }

        public static List<string> StrList(object o, string key)
        {
            var a = Arr(o, key);
            var r = new List<string>();
            if (a == null) return r;
            foreach (var x in a) r.Add(Str(x));
            return r;
        }
    }
}
