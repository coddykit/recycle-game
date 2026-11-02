// Ridgeback Rangers — the Blender kit's merge bank (P2 polish): Resources/kit/props.bytes, written by tools/art/kit.py.
// Every static prop (buildings, trees, tree ferns, rocks, fence posts / rails, shade sails, water points, dig sites, the
// volcano, hills, lamps, kiosks) as raw triangles already in Unity space and on the palette swatches, so MeshKit.Kit()
// appends them into the zone / paddock meshes: the whole diorama stays ONE material and Low gets 0 extra draws.
// "<name>_low" twins never carry more triangles than the procedural shape they replace. Read once (≈ 105 KB), kept as
// arrays; a missing / broken file = every caller falls back to the procedural MeshKit shapes. No per-frame work.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RidgebackRangers.World
{
    public static class KitBank
    {
        public sealed class Entry
        {
            public Vector3[] Pos;
            public Vector3[] Normal;
            public byte[] Swatch;
            public int[] Index;
            public int Tris => Index.Length / 3;
        }

        static Dictionary<string, Entry> bank;

        /// <summary>The prop, or null (no bank / unknown name). low = prefer "&lt;name&gt;_low" when it exists.</summary>
        public static Entry Get(string name, bool low = false)
        {
            if (bank == null) Load();
            if (low && bank.TryGetValue(name + "_low", out var l)) return l;
            return bank.TryGetValue(name, out var e) ? e : null;
        }

        public static bool Has(string name) => Get(name) != null;

        static void Load()
        {
            bank = new Dictionary<string, Entry>();
            var ta = Resources.Load<TextAsset>("kit/props");
            if (ta == null) return;
            try { Parse(ta.bytes, bank); }
            catch (Exception ex) { bank.Clear(); Debug.LogWarning("[KitBank] props.bytes unreadable, procedural fallback: " + ex.Message); }
            Resources.UnloadAsset(ta);
        }

        /// <summary>b"RRKB", u16 version 1, u16 count; per prop: u8 name length, name, u16 vertices, u16 indices,
        /// vertices (3 × i16 position / 500 m, 3 × i8 normal / 127, u8 swatch), indices (u16).</summary>
        public static void Parse(byte[] data, Dictionary<string, Entry> into)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (r.ReadByte() != 'R' || r.ReadByte() != 'R' || r.ReadByte() != 'K' || r.ReadByte() != 'B') throw new InvalidDataException("magic");
                if (r.ReadUInt16() != 1) throw new InvalidDataException("version");
                int count = r.ReadUInt16();
                for (int p = 0; p < count; p++)
                {
                    string name = System.Text.Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                    int nv = r.ReadUInt16(), ni = r.ReadUInt16();
                    var e = new Entry { Pos = new Vector3[nv], Normal = new Vector3[nv], Swatch = new byte[nv], Index = new int[ni] };
                    for (int i = 0; i < nv; i++)
                    {
                        e.Pos[i] = new Vector3(r.ReadInt16() / 500f, r.ReadInt16() / 500f, r.ReadInt16() / 500f);
                        e.Normal[i] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f).normalized;
                        e.Swatch[i] = r.ReadByte();
                    }
                    for (int i = 0; i < ni; i++)
                    {
                        int k = r.ReadUInt16();
                        if (k >= nv) throw new InvalidDataException(name + " index");
                        e.Index[i] = k;
                    }
                    into[name] = e;
                }
            }
        }
    }
}
