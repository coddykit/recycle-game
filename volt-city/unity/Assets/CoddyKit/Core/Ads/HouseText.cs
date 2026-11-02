// CoddyKit (Unity) — house-ad text + link rules, ported 1:1 from CoddyBanner.gd / CoddyAds.gd (Patron #110, owner 10-25):
//   * subtitle = ONLY the creative's sloganI18n[lang] (<= 34 chars, ja/ko/zh <= 17, no "…", no newline, no free / trial /
//     store promise) — otherwise none, the title stands alone (never another language, never subtitleI18n);
//   * short title = shortNameI18n[lang] / shortName, else the part before the first ':' / ' - ' / ' – ' / ' — ';
//   * Install label = the game's own translation, else the built-in table (en fallback);
//   * Play links get referrer = backend androidReferrer, else utm_source=<host>&utm_medium=house_<format>&utm_campaign=coddyads;
//   * hero frames: heroI18n[lang][orientation] -> en -> other orientation -> heroImageUrls -> interstitialImageUrl (<= 3, https).
using System;
using System.Collections.Generic;

namespace CoddyKit.Ads
{
    public static class HouseText
    {
        public const int SloganMax = 34;
        public const int SloganMaxCjk = 17;
        static readonly string[] Cjk = { "ja", "ko", "zh" };

        public static readonly Dictionary<string, string> Install = new Dictionary<string, string>
        {
            { "en", "Install" }, { "de", "Laden" }, { "tr", "Yükle" }, { "fr", "Installer" }, { "es", "Instalar" },
            { "pt", "Instalar" }, { "ru", "Скачать" }, { "ja", "インストール" }, { "ko", "설치" }, { "zh", "安装" },
            { "hi", "इंस्टॉल" }, { "id", "Instal" },
        };

        /// <summary>A banner subtitle never promises price / trial / store (store policy + owner rule). Lower-case match.</summary>
        public static readonly string[] SubBanned =
        {
            "free", "trial", "google play", "play store", "app store", "gratis", "gratuit", "kostenlos", "grátis",
            "ücretsiz", "bedava", "deneme", "бесплат", "无料", "無料", "免费", "免費", "무료", "मुफ़्त", "मुफ्त", "gratuito", "uji coba",
        };

        static readonly string[] ShortSeparators = { ":", " - ", " – ", " — " };

        /// <summary>The player's language from a locale ("pt-BR", "de_DE", "hi") -> 2 letters lower case.</summary>
        public static string Lang2(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return "en";
            string l = locale.Trim().ToLowerInvariant();
            return l.Length >= 2 ? l.Substring(0, 2) : "en";
        }

        /// <summary>Text for lang, else en, else the first value ("" when none).</summary>
        public static string PickI18n(object map, string lang)
        {
            if (!(map is Dictionary<string, object> m) || m.Count == 0) return "";
            foreach (string k in new[] { lang, "en" })
                if (k != null && m.TryGetValue(k, out object v) && v is string s && s != "") return s;
            foreach (var kv in m) return kv.Value as string ?? "";
            return "";
        }

        public static string SafeSubtitle(string text)
        {
            string t = (text ?? "").Trim();
            string low = t.ToLowerInvariant();
            foreach (string w in SubBanned)
                if (low.Contains(w)) return "";
            return t;
        }

        public static string SloganFor(Dictionary<string, object> c, string lang)
        {
            if (!(MiniJson.Get(c, "sloganI18n") is Dictionary<string, object> m) || lang == null || !m.TryGetValue(lang, out object v)) return "";
            string t = (v as string ?? "").Trim();
            int limit = Array.IndexOf(Cjk, lang) >= 0 ? SloganMaxCjk : SloganMax;
            if (t.Length == 0 || TextLength(t) > limit || t.Contains("…") || t.Contains("...") || t.Contains("\n")) return "";
            return SafeSubtitle(t);
        }

        /// <summary>Length in text elements (a Devanagari conjunct / emoji counts once, like Godot's String.length on
        /// composed text is close enough for the 34-char rule).</summary>
        public static int TextLength(string s) => new System.Globalization.StringInfo(s).LengthInTextElements;

        public static string InstallText(string lang, string gameTranslation = null)
        {
            if (!string.IsNullOrEmpty(gameTranslation) && gameTranslation != "Install") return gameTranslation;
            return lang != null && Install.TryGetValue(lang, out string s) ? s : Install["en"];
        }

        public static string ShortTitle(Dictionary<string, object> c, string full, string lang)
        {
            string cfg = "";
            if (MiniJson.Get(c, "shortNameI18n") is Dictionary<string, object> m)
            {
                cfg = MiniJson.Str(m, lang ?? "en");
                if (cfg == "") cfg = MiniJson.Str(m, "en");
            }
            if (cfg == "") cfg = MiniJson.Str(c, "shortName");
            if (cfg != "" && cfg != full) return cfg;
            full = full ?? "";
            int cut = full.Length;
            foreach (string sep in ShortSeparators)
            {
                int i = full.IndexOf(sep, StringComparison.Ordinal);
                if (i > 0 && i < cut) cut = i;
            }
            string t = full.Substring(0, cut).Trim();
            return t != "" && t != full ? t : "";
        }

        public static string StoreUrl(Dictionary<string, object> c, string platform = "android")
        {
            return MiniJson.Str(c, platform == "ios" ? "appStoreUrl" : "playUrl");
        }

        public static string StoreLink(string url, string androidReferrer, string hostAppId, string format)
        {
            if (string.IsNullOrEmpty(url) || !url.Contains("play.google.com") || url.Contains("referrer=")) return url ?? "";
            string reff = !string.IsNullOrEmpty(androidReferrer)
                ? androidReferrer
                : $"utm_source={hostAppId}&utm_medium=house_{format}&utm_campaign=coddyads";
            return url + (url.Contains("?") ? "&" : "?") + "referrer=" + Uri.EscapeDataString(reff);
        }

        public static List<string> HeroUrls(Dictionary<string, object> c, string lang = "en", bool portrait = true)
        {
            string orient = portrait ? "portrait" : "landscape";
            string other = portrait ? "landscape" : "portrait";
            if (MiniJson.Get(c, "heroI18n") is Dictionary<string, object> m)
            {
                foreach (var key in new[] { (lang, orient), ("en", orient), (lang, other), ("en", other) })
                {
                    if (MiniJson.Get(m, key.Item1) is Dictionary<string, object> e && MiniJson.Get(e, key.Item2) is List<object> list)
                    {
                        var got = Https3(list);
                        if (got.Count > 0) return got;
                    }
                }
            }
            var outList = Https3(MiniJson.Arr(c, "heroImageUrls") ?? new List<object>());
            string single = MiniJson.Str(c, "interstitialImageUrl");
            if (outList.Count == 0 && single.StartsWith("http", StringComparison.Ordinal)) outList.Add(single);
            return outList;
        }

        static List<string> Https3(List<object> a)
        {
            var outList = new List<string>();
            foreach (object u in a)
            {
                string s = u as string ?? "";
                if (s.StartsWith("http", StringComparison.Ordinal) && !outList.Contains(s) && outList.Count < 3) outList.Add(s);
            }
            return outList;
        }
    }
}
