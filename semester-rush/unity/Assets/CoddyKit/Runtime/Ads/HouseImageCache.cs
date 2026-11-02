// CoddyKit (Unity) — house-ad image cache (icons <= 168 px for the banner / 256 for the card, hero frames <= 512 px):
// download once (UnityWebRequest), downscale on the GPU, keep a PNG on disk (persistentDataPath/coddykit/coddyads/img),
// icons stay in memory, hero frames are decoded only when a card opens and released with it (Patron round 2 memory rule).
// A creative is only eligible once its OWN icon decoded (HTTP 200 + decodable) — never an empty box / another game's icon.
// FORMAT RISK: the backend's hero frames are WebP (games/docs/HOUSE_AD_ASSETS_2026-10-26.md); Unity's
// Texture2D.LoadImage decodes PNG / JPEG only. A WebP body goes through HouseImageCache.WebPDecoder (null until a decoder
// package is added, docs/UNITY.md §6) — without it the hero counts as failed and the card uses its icon layout
// (big icon + brand colour), exactly the Godot card's "no hero" branch. Icons are PNG today.
// UNCOMPILED until the editor is installed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CoddyKit.Ads
{
    public sealed class HouseImageCache
    {
        public const int IconPx = 168;
        public const int HeroPx = 512;
        /// <summary>Optional WebP decoder (bytes -> RGBA32 texture). Register from a decoder package; null = WebP unsupported.</summary>
        public static Func<byte[], Texture2D> WebPDecoder;

        readonly string dir;
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        readonly HashSet<string> failed = new HashSet<string>();
        readonly HashSet<string> loading = new HashSet<string>();
        readonly HashSet<string> onDisk = new HashSet<string>();

        public HouseImageCache(string kitDir)
        {
            dir = Path.Combine(kitDir, "coddyads", "img");
            Directory.CreateDirectory(dir);
        }

        public static string Key(string url)
        {
            using (var sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
                var sb = new StringBuilder(40);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        string PathFor(string url) => Path.Combine(dir, Key(url) + ".png");

        public bool Failed(string url) => failed.Contains(url);
        public bool IconReady(string url) => !string.IsNullOrEmpty(url) && icons.ContainsKey(url);
        public Texture2D Icon(string url) => url != null && icons.TryGetValue(url, out var t) ? t : null;
        public bool HeroOnDisk(string url) => onDisk.Contains(url) || File.Exists(PathFor(url));

        /// <summary>Starts (once) the download of an icon; from disk when cached.</summary>
        public IEnumerator FetchIcon(string url)
        {
            if (string.IsNullOrEmpty(url) || icons.ContainsKey(url) || failed.Contains(url) || !loading.Add(url)) yield break;
            string p = PathFor(url);
            Texture2D tex = File.Exists(p) ? Decode(SafeBytes(p)) : null;
            if (tex == null)
            {
                byte[] body = null;
                yield return Download(url, b => body = b);
                tex = Downscaled(Decode(body), IconPx);
                if (tex != null) Save(p, tex);
            }
            loading.Remove(url);
            if (tex == null) { failed.Add(url); yield break; }
            tex.name = "house-icon";
            icons[url] = tex;
        }

        /// <summary>Downloads a hero frame to disk (not decoded / kept in memory).</summary>
        public IEnumerator FetchHero(string url)
        {
            if (string.IsNullOrEmpty(url) || failed.Contains(url) || HeroOnDisk(url) || !loading.Add(url)) yield break;
            byte[] body = null;
            yield return Download(url, b => body = b);
            Texture2D tex = Downscaled(Decode(body), HeroPx);
            loading.Remove(url);
            if (tex == null) { failed.Add(url); yield break; }
            Save(PathFor(url), tex);
            onDisk.Add(url);
            UnityEngine.Object.Destroy(tex);
        }

        /// <summary>Decodes the cached frames of a card (the caller destroys them when the card closes).</summary>
        public List<Texture2D> LoadHeroes(IList<string> urls, bool lowTier)
        {
            var outList = new List<Texture2D>();
            foreach (string u in urls)
            {
                if (!HeroOnDisk(u)) continue;
                var t = Decode(SafeBytes(PathFor(u)));
                if (t != null) outList.Add(t);
                if (lowTier && outList.Count == 1) break; // Low: one still frame
            }
            return outList;
        }

        static IEnumerator Download(string url, Action<byte[]> done)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success && req.responseCode == 200 ? req.downloadHandler.data : null);
            }
        }

        static bool IsWebP(byte[] b) =>
            b != null && b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';

        public static Texture2D Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            if (IsWebP(bytes)) return WebPDecoder?.Invoke(bytes);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(t);
                return null;
            }
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Longest side <= maxPx, aspect kept (GPU blit; the source is destroyed when replaced).</summary>
        public static Texture2D Downscaled(Texture2D src, int maxPx)
        {
            if (src == null) return null;
            int w = src.width, h = src.height;
            if (Mathf.Max(w, h) <= maxPx) return src;
            float k = maxPx / (float)Mathf.Max(w, h);
            int nw = Mathf.Max(1, Mathf.RoundToInt(w * k)), nh = Mathf.Max(1, Mathf.RoundToInt(h * k));
            var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            dst.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(src);
            dst.wrapMode = TextureWrapMode.Clamp;
            return dst;
        }

        static void Save(string path, Texture2D t)
        {
            try { File.WriteAllBytes(path, t.EncodeToPNG()); } catch (Exception e) { Debug.LogWarning("[CoddyKit] image cache: " + e.Message); }
        }

        static byte[] SafeBytes(string p)
        {
            try { return File.ReadAllBytes(p); } catch (Exception) { return null; }
        }
    }
}
