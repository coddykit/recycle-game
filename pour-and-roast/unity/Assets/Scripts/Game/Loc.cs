// Pour & Roast (from Volt City) — the strings table (P3). Every UI text has a key in Assets/Resources/i18n/strings.csv:
//   key,en,tr,de,es,fr,pt,ru,ja,ko,hi        (pt = pt-BR, hi = the 10th language; DESIGN section 10)
// Named placeholders in braces: "Short {mw}" -> F("hud.short", "mw", "4 MW"). Missing cell -> English -> the key itself
// (a visible key is a test failure, never a crash). Engine-free (PourAndRoast.Game, noEngineReferences): the Unity layer
// passes the CSV text (Resources.Load<TextAsset>("i18n/strings").text), tests read the file directly.
using System;
using System.Collections.Generic;
using System.Text;

namespace PourAndRoast.Game
{
    public sealed class Loc
    {
        /// <summary>The 10 game languages, column order of strings.csv after "key".</summary>
        public static readonly string[] Languages = { "en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi" };
        /// <summary>Each language in its own script (the language picker never translates these).</summary>
        public static readonly string[] NativeNames = { "English", "Türkçe", "Deutsch", "Español", "Français", "Português (Brasil)", "Русский", "日本語", "한국어", "हिन्दी" };

        readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
        readonly List<string> keys = new List<string>();
        int li;

        public string Lang => Languages[li];
        public int LangIndex => li;
        public IReadOnlyList<string> Keys => keys;
        public readonly List<string> Errors = new List<string>();
        /// <summary>Raised after SetLanguage: every open view re-localises (P5 gate: "language switch re-localises everything open").</summary>
        public event Action Changed;

        public static int IndexOf(string lang)
        {
            if (string.IsNullOrEmpty(lang)) return -1;
            string l = lang.ToLowerInvariant();
            if (l.StartsWith("pt")) l = "pt";
            if (l.Length > 2 && (l[2] == '-' || l[2] == '_')) l = l.Substring(0, 2);
            return Array.IndexOf(Languages, l);
        }

        public static Loc FromCsv(string csv, string lang = "en")
        {
            var loc = new Loc();
            var rows = ParseCsv(csv ?? "");
            if (rows.Count == 0) { loc.Errors.Add("empty table"); return loc; }
            var head = rows[0];
            var col = new int[Languages.Length];
            for (int i = 0; i < Languages.Length; i++) col[i] = head.IndexOf(Languages[i]);
            if (head.Count == 0 || head[0] != "key" || col[0] != 1) loc.Errors.Add("header must start with key,en");
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0 || string.IsNullOrEmpty(row[0]) || row[0].StartsWith("#")) continue;
                var vals = new string[Languages.Length];
                for (int i = 0; i < Languages.Length; i++)
                    vals[i] = col[i] >= 0 && col[i] < row.Count ? row[col[i]] : "";
                if (loc.table.ContainsKey(row[0])) { loc.Errors.Add("duplicate key " + row[0]); continue; }
                loc.table[row[0]] = vals;
                loc.keys.Add(row[0]);
            }
            loc.SetLanguage(lang, false);
            return loc;
        }

        public void SetLanguage(string lang, bool notify = true)
        {
            int i = IndexOf(lang);
            li = i < 0 ? 0 : i;
            if (notify) Changed?.Invoke();
        }

        public bool Has(string key) => table.ContainsKey(key ?? "");

        /// <summary>Text in the current language (English when the cell is empty, the key when nothing exists).</summary>
        public string T(string key)
        {
            if (key == null) return "";
            if (!table.TryGetValue(key, out var v)) return key;
            string s = v[li];
            if (string.IsNullOrEmpty(s)) s = v[0];
            return string.IsNullOrEmpty(s) ? key : s;
        }

        /// <summary>Raw cell (tests): "" when missing.</summary>
        public string Cell(string key, string lang)
        {
            int i = IndexOf(lang);
            return i >= 0 && table.TryGetValue(key ?? "", out var v) ? v[i] ?? "" : "";
        }

        /// <summary>CLDR plural category of n for a language (cardinal, integers): ru one / few / many; fr, pt (Brazil), hi
        /// one = 0 and 1; ja, ko other only; en, de, es, tr one = 1.</summary>
        public static string PluralCategory(string lang, long n)
        {
            long a = Math.Abs(n);
            switch (lang)
            {
                case "ru":
                    if (a % 10 == 1 && a % 100 != 11) return "one";
                    if (a % 10 >= 2 && a % 10 <= 4 && (a % 100 < 12 || a % 100 > 14)) return "few";
                    return "many";
                case "fr":
                case "pt":
                case "hi":
                    return a <= 1 ? "one" : "other";
                case "ja":
                case "ko":
                    return "other";
                default:
                    return a == 1 ? "one" : "other";
            }
        }

        /// <summary>A counted phrase: key.one / .few / .many / .other by the language's plural rule, {n} filled
        /// ("count.faults" -> "1 fault" / "2 faults", "1 поломка" / "3 поломки" / "5 поломок"). Patron #172: "1 faults".</summary>
        public string N(string key, long n)
        {
            string k = key + "." + PluralCategory(Lang, n);
            string text = Has(k) ? T(k) : T(key + ".other");
            return Fill(text, "n", n);
        }

        /// <summary>T(key) with named placeholders: F("hud.short", "mw", "4 MW").</summary>
        public string F(string key, params object[] pairs) => Fill(T(key), pairs);

        public static string Fill(string text, params object[] pairs)
        {
            if (pairs == null || pairs.Length == 0 || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                sb.Replace("{" + pairs[i] + "}", Convert.ToString(pairs[i + 1], System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Placeholder names of a text ("{mw}" -> mw), for the parity test across languages.</summary>
        public static List<string> Placeholders(string text)
        {
            var o = new List<string>();
            if (string.IsNullOrEmpty(text)) return o;
            int i = 0;
            while ((i = text.IndexOf('{', i)) >= 0)
            {
                int j = text.IndexOf('}', i + 1);
                if (j < 0) break;
                o.Add(text.Substring(i + 1, j - i - 1));
                i = j + 1;
            }
            o.Sort(StringComparer.Ordinal);
            return o;
        }

        /// <summary>RFC 4180 CSV: commas, double quotes ("" = one quote), line breaks inside quotes, CRLF or LF, UTF-8 BOM.</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, any = false;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': quoted = true; any = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); any = true; break;
                    case '\r': break;
                    case '\n':
                        if (any || cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
                        row = new List<string>(); cell.Clear(); any = false;
                        break;
                    default: cell.Append(c); any = true; break;
                }
            }
            if (any || cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
