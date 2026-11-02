// Volt City P4 — sold looks (docs/IAP.md #9-#15 + the Chief Engineer Edition's gold theme) -> what the diorama / UI draws.
// Engine-free. Every look is a uniform on the DEFAULT model (docs/ART-IDS.md): truck livery `_LiveryA/_LiveryB` (VcProp +
// VcPropLow), `_BladeStyle` (VcBlades, every tier), globals `_VcSkylineStyle` / `_VcPylonDeco` (VcWorldCore = Medium AND
// Low), UI themes = a USS class on the screen root (kit palette override only). So a sold look is visible on every tier and
// never worse than the default (LL D6 "sold cosmetics never drawn" — Travel Tycoon 10-19).
// Slots: truck (sunset / arctic / gold) and room (retro / gold) are exclusive; pylons / blades / skyline are on/off.
// Selection persists in GameSettings ("looks"); a new purchase equips its look; only OWNED looks ever resolve.
using System;
using System.Collections.Generic;

namespace VoltCity.Game
{
    public sealed class LookState
    {
        public string Truck = "", Room = "";
        public bool Pylons, Blades, Skyline;

        /// <summary>Truck livery primary / secondary (sRGB hex) for `_LiveryA/_LiveryB`.</summary>
        public string LiveryA => Truck == "livery_sunset" ? "f2603a" : Truck == "livery_arctic" ? "eef4fa" : Truck == "theme_gold" ? "e8b84a" : Looks.DefaultLiveryA;
        public string LiveryB => Truck == "livery_sunset" ? "b8322a" : Truck == "livery_arctic" ? "3b6fb5" : Truck == "theme_gold" ? "7a5a1c" : Looks.DefaultLiveryB;
        public float BladeStyle => Blades ? 1f : 0f;
        public float SkylineStyle => Skyline ? 1f : 0f;
        public float PylonDeco => Pylons ? 1f : 0f;
        /// <summary>USS class on the screen root ("" = the default palette).</summary>
        public string RoomClass => Room == "room_retro" ? "vc-theme--retro" : Room == "theme_gold" ? "vc-theme--gold" : "";

        public string Sig() => Truck + "|" + Room + "|" + Pylons + Blades + Skyline;
    }

    public static class Looks
    {
        public const string DefaultLiveryA = "f2b81f", DefaultLiveryB = "c48f12";

        /// <summary>Every look entitlement the game can draw (theme_gold comes with the Chief Engineer Edition / bundle).</summary>
        public static readonly string[] All = { "livery_sunset", "livery_arctic", "theme_gold", "pylons_deco", "blades_candy", "skyline_neon", "room_retro" };

        public static string[] Slots(string look)
        {
            switch (look)
            {
                case "livery_sunset": case "livery_arctic": return new[] { "truck" };
                case "theme_gold": return new[] { "truck", "room" };
                case "room_retro": return new[] { "room" };
                case "pylons_deco": return new[] { "pylons" };
                case "blades_candy": return new[] { "blades" };
                case "skyline_neon": return new[] { "skyline" };
            }
            return new string[0];
        }

        public static bool InUse(IDictionary<string, string> sel, string look)
        {
            var s = Slots(look);
            if (s.Length == 0 || sel == null) return false;
            foreach (string slot in s) if (!sel.TryGetValue(slot, out var v) || v != look) return false;
            return true;
        }

        /// <summary>Use / stop using a look (the shop's "Use" / "In use" pill). Returns the new in-use state.</summary>
        public static bool Toggle(IDictionary<string, string> sel, string look)
        {
            bool on = !InUse(sel, look);
            foreach (string slot in Slots(look))
            {
                if (on) sel[slot] = look;
                else if (sel.TryGetValue(slot, out var v) && v == look) sel[slot] = "";
            }
            return on;
        }

        public static void Equip(IDictionary<string, string> sel, string look)
        {
            if (!InUse(sel, look)) Toggle(sel, look);
        }

        /// <summary>What to draw: the selection, owned looks only.</summary>
        public static LookState Resolve(IDictionary<string, string> sel, Func<string, bool> has)
        {
            var st = new LookState();
            if (sel == null || has == null) return st;
            string Get(string slot) => sel.TryGetValue(slot, out var v) && !string.IsNullOrEmpty(v) && has(v) ? v : "";
            st.Truck = Get("truck");
            st.Room = Get("room");
            st.Pylons = Get("pylons") != "";
            st.Blades = Get("blades") != "";
            st.Skyline = Get("skyline") != "";
            return st;
        }
    }
}
