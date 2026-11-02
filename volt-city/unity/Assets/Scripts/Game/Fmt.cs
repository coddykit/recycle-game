// Volt City — number / money / time formatting for the UI (engine-free, deterministic: no CultureInfo lookups, so IL2CPP
// globalisation stripping and device locales can never change a label). Separators follow the UI language.
using System;
using System.Globalization;

namespace VoltCity.Game
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

        /// <summary>Power from kW: 46 MW, 4.5 MW (below 10 MW one decimal), 0 MW.</summary>
        public static string Mw(long kw, string lang = "en")
        {
            double mw = Math.Abs(kw) / 1000.0;
            string n = mw < 10 ? OneDec(Math.Round(mw, 1), lang) : Int((long)Math.Round(mw), lang);
            return (kw < 0 ? "−" : "") + n + " MW";
        }

        /// <summary>Diorama clock 0..24 h -> "17:30".</summary>
        public static string Clock(double hour)
        {
            double h = ((hour % 24.0) + 24.0) % 24.0;
            int total = (int)Math.Floor(h * 60.0 + 1e-6);
            return (total / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Per-mille -> percent with one decimal below 10 %: 994 -> "99.4 %".</summary>
        public static string Pm(long pm, string lang = "en") => OneDec(pm / 10.0, lang) + " %";

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

        /// <summary>City rating x100 -> "3.2★".</summary>
        public static string Rating(int x100, string lang = "en") => (x100 / 100.0).ToString("0.0", CultureInfo.InvariantCulture).Replace(".", Dec(lang)) + "★";

        /// <summary>Homes behind a demand (fictional: 6 kW per home), rounded to 100.</summary>
        public static long Homes(long kw) => Math.Max(0, (long)Math.Round(kw / 6.0 / 100.0, MidpointRounding.AwayFromZero) * 100);
    }
}
