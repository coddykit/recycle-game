// CoddyKit (Unity) — graphics tier at runtime (games/CLAUDE.md §10): device class -> default tier, the player's choice
// persisted, one-time corrections (FixWrongLow / FlagshipUpgrade), PerfGuard step-down (10 s < 25 FPS uncapped, once,
// with a one-time notice), and the GPU shader-crash breadcrumb (Vitals 10-27 §B, IMG BXM).
// Applying a tier = QualitySettings level (0 Low, 1 Medium, 2 High; each level carries its own URP asset with the tier's
// render scale / MSAA / shadows, created by CoddyKit/Editor/ProjectSetup) + Application.targetFrameRate. The kit runtime
// has no URP dependency on purpose. UNCOMPILED until the editor is installed.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CoddyKit
{
    public static class DeviceProbe
    {
        public static DeviceInfo Read()
        {
            bool handheld = Application.isMobilePlatform;
            int ram = SystemInfo.systemMemorySize;
            if (ram <= 0 && Application.platform == RuntimePlatform.Android)
            {
                try { ram = DeviceTier.ParseMeminfoMb(File.ReadAllText("/proc/meminfo")); } catch (Exception) { ram = -1; }
            }
            string os = Application.platform == RuntimePlatform.Android ? "Android" : Application.platform == RuntimePlatform.IPhonePlayer ? "iOS" : "Desktop";
            return new DeviceInfo { Handheld = handheld, Os = os, RamMb = ram, Cores = SystemInfo.processorCount, Gpu = SystemInfo.graphicsDeviceName ?? "" };
        }
    }

    public sealed class QualityTiers
    {
        public const string StateFile = "graphics.json";
        public const string BreadcrumbFile = "gpu_breadcrumb.txt";

        public Tier Current { get; private set; }
        public Tier DeviceDefault { get; private set; }
        public DeviceInfo Device { get; private set; }
        public bool PlayersChoice { get; private set; }
        public bool SafeShaders { get; private set; }
        public TierSpec Spec => TierSpec.For(Current);
        /// <summary>The game caps FPS on purpose right now (idle window after 1.5 s without touch, LESSONS "20 FPS cap").</summary>
        public bool IdleCapActive;
        public event Action<Tier> Changed;
        /// <summary>One-time notice after an automatic step-down ("You can change it in Settings").</summary>
        public event Action SteppedDown;
        /// <summary>Analytics: gpu_shader_crash {stage, gpu}.</summary>
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => CoddyServices.Analytics.Event(n, p);

        readonly string dir;
        readonly JsonStateFile state;
        readonly PerfGuard guard = new PerfGuard();

        public QualityTiers(string kitDir)
        {
            dir = kitDir;
            state = new JsonStateFile(Path.Combine(kitDir, StateFile)).Load();
        }

        /// <summary>Boot: decide the starting tier (call before the first 3D frame).</summary>
        public Tier Init()
        {
            Device = DeviceProbe.Read();
            DeviceDefault = DeviceTier.DefaultTier(Device);
            string crumbPath = Path.Combine(dir, BreadcrumbFile);
            string crumb = File.Exists(crumbPath) ? SaveRepository.SafeRead(crumbPath) : null;
            var d = ShaderCrashGuard.AtBoot(crumb, MiniJson.Bool(state.Data, "safe_shaders"));
            SafeShaders = d.SafeShaders;
            if (d.ReportCrash)
            {
                Analytics("gpu_shader_crash", new Dictionary<string, object> { { "stage", d.Stage }, { "gpu", Device.Gpu } });
                TryDelete(crumbPath);
                state.Data["safe_shaders"] = true;
            }
            bool has = state.Data.ContainsKey("tier");
            PlayersChoice = MiniJson.Bool(state.Data, "players_choice");
            Tier t = has ? (Tier)Mathf.Clamp(MiniJson.Int(state.Data, "tier"), 0, 2) : DeviceDefault;
            if (has && !MiniJson.Bool(state.Data, "fixes_v1"))
            {
                t = DeviceTier.FixWrongLow(t, DeviceDefault, Device.Handheld, PlayersChoice);
                t = DeviceTier.FlagshipUpgrade(t, DeviceDefault, Device.Handheld, PlayersChoice);
                state.Data["fixes_v1"] = true;
            }
            if (SafeShaders) t = Tier.Low;
            Apply(t, false);
            Debug.Log($"[CoddyKit] device {Device} class={DeviceTier.Classify(Device)} default={DeviceDefault} start={Current} safe_shaders={SafeShaders}");
            return Current;
        }

        /// <summary>The player picked a tier in Settings (persisted as their own choice).</summary>
        public void Choose(Tier t) => Apply(t, true);

        void Apply(Tier t, bool byPlayer)
        {
            Current = t;
            if (byPlayer) PlayersChoice = true;
            int level = Mathf.Clamp((int)t, 0, QualitySettings.count - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
            Application.targetFrameRate = Spec.TargetFps;
            QualitySettings.vSyncCount = 0;
            state.Data["tier"] = (long)t;
            state.Data["players_choice"] = PlayersChoice;
            state.Save();
            Changed?.Invoke(t);
        }

        /// <summary>Call every frame (unscaled dt) during play.</summary>
        public void Tick(float dt)
        {
            bool capped = IdleCapActive || Application.targetFrameRate < Spec.TargetFps;
            if (guard.Tick(dt, capped, Current))
            {
                Apply(Current - 1, false);
                SteppedDown?.Invoke();
            }
        }

        /// <summary>Wrap risky first-time shader work (warm-up, first 3D frame). A launch that dies inside leaves the
        /// breadcrumb; the next launch switches to safe shaders.</summary>
        public void BeginShaderWork(string stage)
        {
            try { File.WriteAllText(Path.Combine(dir, BreadcrumbFile), stage ?? "shader"); } catch (Exception) { }
        }

        public void EndShaderWork() => TryDelete(Path.Combine(dir, BreadcrumbFile));

        static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch (Exception) { }
        }
    }
}
