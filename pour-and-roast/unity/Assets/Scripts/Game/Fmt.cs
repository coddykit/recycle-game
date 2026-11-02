// Pour & Roast (from Volt City) — number / money / time formatting for the UI (engine-free, deterministic: no CultureInfo lookups, so IL2CPP
// globalisation stripping and device locales can never change a label). Separators follow the UI language.
using System;
using System.Globalization;

namespace PourAndRoast.Game
{
    public static class Fmt
    {
        /// <summary>Decimal separator per UI language (tr de es fr pt ru use a comma).</summary>
        public static string Dec(string lang) => lang == "tr" || lang == "de" || lang == "es" || lang == "fr" || lang == "pt" || lang == "ru" ? "," : ".";

        /// <summary>Thousands separator per UI language.</summary>
        public static string Group(string lang)
        {
            switch (lang)
            {
                case "tr": case "de": case "es": case "pt": return ".";
                case "fr": case "ru": return " "; // narrow no-break space
                default: return ",";
            }
        }

        /// <summary>Whole number with grouping: 3400 -> "3,400" (en) / "3.400" (de).</summary>
        public static string Int(long v, string lang = "en")
        {
            string s = Math.Abs(v).ToString(CultureInfo.InvariantCulture);
            string g = Group(lang);
            var sb = new System.Text.StringBuilder();
            int lead = s.Length % 3;
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (i - lead) % 3 == 0) sb.Append(g);
                sb.Append(s[i]);
            }
            return (v < 0 ? "−" : "") + sb;
        }

        static string OneDec(double v, string lang)
        {
            string s = v.ToString("0.0", CultureInfo.InvariantCulture);
            if (s.EndsWith(".0")) s = s.Substring(0, s.Length - 2);
            return s.Replace(".", Dec(lang));
        }

        /// <summary>Full money: $1,950 (prices, report lines).</summary>
        public static string Money(long v, string lang = "en") => (v < 0 ? "−$" : "$") + Int(Math.Abs(v), lang);

        /// <summary>Compact money for the HUD: $930 · $12.4k · $1.2M · $3.03B.</summary>
        public static string MoneyShort(long v, string lang = "en")
        {
            long a = Math.Abs(v);
            string sign = v < 0 ? "−$" : "$";
            if (a < 10000) return sign + Int(a, lang);
            if (a < 1000000) return sign + OneDec(Math.Floor(a / 100.0) / 10.0, lang) + "k";
            if (a < 1000000000) return sign + OneDec(Math.Floor(a / 100000.0) / 10.0, lang) + "M";
            return sign + OneDec(Math.Floor(a / 100000000.0) / 10.0, lang) + "B";
        }

        /// <summary>Diorama clock 0..24 h -> "17:30".</summary>
        public static string Clock(double hour)
        {
            double h = ((hour % 24.0) + 24.0) % 24.0;
            int total = (int)Math.Floor(h * 60.0 + 1e-6);
            return (total / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Seconds -> "6 s" / "1 min 20 s" / "3 h 5 min".</summary>
        public static string Dur(double seconds, Loc loc = null)
        {
            long s = (long)Math.Ceiling(Math.Max(0, seconds));
            string su = loc != null ? loc.T("unit.s") : "s";
            string mu = loc != null ? loc.T("unit.min") : "min";
            string hu = loc != null ? loc.T("unit.h") : "h";
            if (s < 60) return s + " " + su;
            if (s < 3600) return (s / 60) + " " + mu + (s % 60 > 0 ? " " + (s % 60) + " " + su : "");
            return (s / 3600) + " " + hu + ((s % 3600) / 60 > 0 ? " " + ((s % 3600) / 60) + " " + mu : "");
        }

        /// <summary>Day minute (420 = 07:00) -> "07:00".</summary>
        public static string ClockMin(double minute) => Clock(minute / 60.0);

        /// <summary>0..1 -> "86 %".</summary>
        public static string Pct(double f, string lang = "en")
        {
            string n = ((long)Math.Round(Math.Max(0, f) * 100)).ToString(CultureInfo.InvariantCulture);
            return lang == "tr" ? "%" + n : n + " %";   // Turkish writes the sign first (%80)
        }

        /// <summary>Stock amounts: "4.2 kg" / "12 L" (one decimal below 10).</summary>
        public static string Amount(double v, string unit, string lang = "en") => (Math.Abs(v) < 10 ? OneDec(Math.Round(v, 1), lang) : Int((long)Math.Round(v), lang)) + " " + unit;

        /// <summary>Reputation stars 0..5 -> "3.4" (one decimal, language separator).</summary>
        public static string Stars(double r, string lang = "en") => OneDecFixed(Math.Max(0, Math.Min(5, r)), lang);

        static string OneDecFixed(double v, string lang) => v.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", Dec(lang));

        /// <summary>Quality 0..100 -> "78".</summary>
        public static string Q(double q) => ((long)Math.Round(Math.Max(0, Math.Min(100, q)))).ToString(CultureInfo.InvariantCulture);
    }
}
