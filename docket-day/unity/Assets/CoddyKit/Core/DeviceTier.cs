// CoddyKit (Unity) — device class -> starting graphics tier. Exact port of the fleet rule in
// games/tools/perf/addons/coddy_perf/perf_device.gd (games/CLAUDE.md §10, owner 2026-10-23):
//   flagship -> HIGH, strong -> MEDIUM, weak / unknown handheld -> LOW, desktop -> HIGH.
//   Android flagship = RAM >= 7000 MB AND >= 8 cores AND a flagship GPU (Adreno 8xx / 730+, Mali Immortalis,
//                      Mali-G720/G725/G9xx, Xclipse 9xx).
//   Android strong   = RAM >= 5325 MB AND >= 8 cores AND a GPU outside the weak families (Mali-G5x/G6x, Mali-T8xx,
//                      PowerVR / IMG (incl. BXM), Adreno 5xx / 600-619).
//   iOS (not shipped; kept for parity) flagship RAM >= 7000, strong >= 3482.
// Unity adds one adapter spelling the Godot list did not need: IMG's newer drivers report "IMG BXM-8-256" without the
// word PowerVR (Vitals 2026-10-27, moto g64y) — matched as weak.
// Runtime input (DeviceProbe in CoddyKit.Runtime): SystemInfo.systemMemorySize, processorCount, graphicsDeviceName.
using System;
using System.Text.RegularExpressions;

namespace CoddyKit
{
    public enum Tier { Low = 0, Medium = 1, High = 2 }

    public struct DeviceInfo
    {
        public bool Handheld;
        public string Os;      // "Android", "iOS", "Desktop"
        public int RamMb;      // OS figure, -1 unknown
        public int Cores;
        public string Gpu;     // adapter name

        public override string ToString() => $"{Os} handheld={Handheld} ram={RamMb}MB cores={Cores} gpu='{Gpu}'";
    }

    public static class DeviceTier
    {
        public const int AndroidMinRamMb = 5325;
        public const int AndroidMinCores = 8;
        public const int IosMinRamMb = 3482;
        public const int FlagshipMinRamMb = 7000;

        public const string WeakGpu =
            @"mali-?g[56][0-9](?![0-9])|mali-?t8[0-9][0-9]|powervr|\bimg\b|\bbxm\b|bxm-|ge8[0-9]{3}|adreno[^0-9]*(5[0-9][0-9]|6[01][0-9])(?![0-9])";
        public const string FlagshipGpu =
            @"adreno[^0-9]*(8[0-9][0-9]|7[3-9][0-9])(?![0-9])|immortalis|mali-?g(72[05]|9[0-9][0-9])(?![0-9])|xclipse[^0-9]*9[0-9][0-9](?![0-9])";

        static readonly Regex WeakRe = new Regex(WeakGpu, RegexOptions.CultureInvariant);
        static readonly Regex FlagshipRe = new Regex(FlagshipGpu, RegexOptions.CultureInvariant);

        public static bool IsWeakGpu(string name) => !string.IsNullOrEmpty(name) && WeakRe.IsMatch(name.ToLowerInvariant());
        public static bool IsFlagshipGpu(string name) => !string.IsNullOrEmpty(name) && FlagshipRe.IsMatch(name.ToLowerInvariant());

        /// <summary>"desktop", "flagship", "strong" or "weak" (weak also covers unknown handhelds).</summary>
        public static string Classify(DeviceInfo d)
        {
            if (!d.Handheld) return "desktop";
            int ram = d.RamMb;
            switch (d.Os ?? "")
            {
                case "iOS":
                    if (ram >= FlagshipMinRamMb) return "flagship";
                    return ram >= IosMinRamMb ? "strong" : "weak";
                case "Android":
                    string gpu = (d.Gpu ?? "").Trim();
                    if (ram < AndroidMinRamMb || d.Cores < AndroidMinCores) return "weak";
                    if (gpu.Length == 0 || IsWeakGpu(gpu)) return "weak";
                    if (ram >= FlagshipMinRamMb && IsFlagshipGpu(gpu)) return "flagship";
                    return "strong";
            }
            return "weak";
        }

        public static Tier DefaultTier(DeviceInfo d)
        {
            switch (Classify(d))
            {
                case "desktop":
                case "flagship": return Tier.High;
                case "strong": return Tier.Medium;
                default: return Tier.Low;
            }
        }

        /// <summary>"MemTotal:  11534336 kB" -> 11264 (MB); -1 without a usable line (/proc/meminfo fallback).</summary>
        public static int ParseMeminfoMb(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
                string[] parts = line.Substring(9).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && long.TryParse(parts[0], out long kb) && kb > 0) return (int)(kb / 1024);
            }
            return -1;
        }

        /// <summary>One-time correction of a wrong Low (fleet 10-23): a strong handheld goes back to Medium unless the tier
        /// is the player's own choice.</summary>
        public static Tier FixWrongLow(Tier current, Tier deviceDefault, bool handheld, bool playersChoice)
        {
            if (!handheld || playersChoice || deviceDefault < Tier.Medium) return current;
            return current < Tier.Medium ? Tier.Medium : current;
        }

        /// <summary>One-time flagship upgrade: a flagship still on the old automatic Medium goes to High (never lowers).</summary>
        public static Tier FlagshipUpgrade(Tier current, Tier deviceDefault, bool handheld, bool playersChoice)
        {
            if (!handheld || playersChoice || deviceDefault < Tier.High || current != Tier.Medium) return current;
            return Tier.High;
        }
    }

    /// <summary>The fleet tier table (games/CLAUDE.md §10 + W67, LESSONS A2/A10b). One place the runtime reads.
    /// Low: no shadows, 0.85 render scale, NO MSAA, 30 FPS cap, crowds / particles <= 50 %, textures <= 1024, per-vertex
    /// lit "Low" shaders, no post, 15 Hz traffic updates. Medium: simple main-light shadows, 1.0 + MSAA 2x, 60 FPS.
    /// High: full shadows, MSAA 4x, extra effects (sparks, heat shimmer).</summary>
    public sealed class TierSpec
    {
        public Tier Tier;
        public float RenderScale;
        public int Msaa;
        public bool MainLightShadows;
        public bool SoftShadows;
        public float ShadowDistance;
        public int TargetFps;
        public float CrowdShare;     // cars / trucks / particles relative to Medium
        public int TextureMipLimit;  // QualitySettings.globalTextureMipmapLimit (1 = 2048 sources drawn at <= 1024)
        public bool PerVertexLowShaders;
        public bool ExtraEffects;    // rain / smoke (Medium+), sparks / shimmer (High)
        public float SimVisualHz;    // how often moving props are rewritten (Low 15 Hz)

        public static TierSpec For(Tier t)
        {
            switch (t)
            {
                case Tier.High:
                    return new TierSpec { Tier = t, RenderScale = 1f, Msaa = 4, MainLightShadows = true, SoftShadows = true, ShadowDistance = 2800f, TargetFps = 60, CrowdShare = 1f, TextureMipLimit = 0, PerVertexLowShaders = false, ExtraEffects = true, SimVisualHz = 60f };
                case Tier.Medium:
                    return new TierSpec { Tier = t, RenderScale = 1f, Msaa = 2, MainLightShadows = true, SoftShadows = false, ShadowDistance = 2800f, TargetFps = 60, CrowdShare = 1f, TextureMipLimit = 0, PerVertexLowShaders = false, ExtraEffects = true, SimVisualHz = 30f };
                default:
                    return new TierSpec { Tier = Tier.Low, RenderScale = 0.85f, Msaa = 1, MainLightShadows = false, SoftShadows = false, ShadowDistance = 0f, TargetFps = 30, CrowdShare = 0.5f, TextureMipLimit = 1, PerVertexLowShaders = true, ExtraEffects = false, SimVisualHz = 15f };
            }
        }

        /// <summary>The fleet's hard rules for a tier table (validate() in perf_profiles.gd). Empty = valid.</summary>
        public static string Validate(TierSpec s)
        {
            if (s.Tier == Tier.Low)
            {
                if (s.Msaa > 1) return "Low must not use MSAA";
                if (s.MainLightShadows) return "Low must not cast shadows";
                if (s.RenderScale < 0.80f || s.RenderScale > 0.85f) return "Low render scale must be 0.80-0.85";
                if (s.TargetFps > 30) return "Low caps at 30 FPS";
                if (s.CrowdShare > 0.5f) return "Low crowds <= 50 %";
                if (s.ExtraEffects) return "Low has no extra effects";
            }
            else if (s.RenderScale < 1f) return "Medium/High render at native scale";
            return "";
        }
    }

    /// <summary>Dynamic step-down (games/CLAUDE.md §10, LESSONS A "PerfGuard"): 10 s in a row under 25 FPS of UNCAPPED
    /// frames -> one tier down, once per launch, with a one-time notice. Frames the game caps on purpose (window open,
    /// 30 FPS Low cap reached exactly, background) never count as slow. Never steps up by itself.</summary>
    public sealed class PerfGuard
    {
        public const float WindowS = 10f;
        public const float MinFps = 25f;
        float slowFor;
        public bool SteppedThisLaunch { get; private set; }

        /// <summary>Feed one frame. capped = the frame was limited on purpose. Returns true when the game should step down now.</summary>
        public bool Tick(float dt, bool capped, Tier current)
        {
            if (SteppedThisLaunch || current == Tier.Low || dt <= 0f) return false;
            if (capped) { slowFor = 0f; return false; }
            float fps = 1f / dt;
            if (fps < MinFps) slowFor += dt; else slowFor = 0f;
            if (slowFor >= WindowS)
            {
                SteppedThisLaunch = true;
                slowFor = 0f;
                return true;
            }
            return false;
        }
    }

    /// <summary>GPU shader-compiler crash breadcrumb (Vitals 2026-10-27 §B: IMG BXM-8-256 crashed in glCompileShader on the
    /// first 3D frame, Low did not help). Before the warm-up / first 3D frame the runtime writes the breadcrumb, after a
    /// clean frame it removes it. A breadcrumb that survives into the next launch = the previous launch died compiling
    /// shaders: report `gpu_shader_crash` once and switch the persisted `safe_shaders` flag on (Low tier + the simplest
    /// shader variants + no warm-up batching). Pure state machine; CoddyKit.Runtime does the file IO.</summary>
    public static class ShaderCrashGuard
    {
        public struct Decision
        {
            public bool ReportCrash;
            public bool SafeShaders;
            public string Stage;
        }

        /// <summary>breadcrumb: content of the file from the previous launch (null = none). safeShadersBefore: the flag.</summary>
        public static Decision AtBoot(string breadcrumb, bool safeShadersBefore)
        {
            var d = new Decision { SafeShaders = safeShadersBefore, Stage = "" };
            if (!string.IsNullOrEmpty(breadcrumb))
            {
                d.ReportCrash = true;
                d.SafeShaders = true;
                d.Stage = breadcrumb.Length > 64 ? breadcrumb.Substring(0, 64) : breadcrumb;
            }
            return d;
        }
    }
}
