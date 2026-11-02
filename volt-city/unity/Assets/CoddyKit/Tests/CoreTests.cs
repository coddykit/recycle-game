// CoddyKit (Unity) EditMode tests for the engine-free core. They run in the Unity Test Runner (EditMode) AND, before the
// editor existed, under dotnet + NUnit (Tools/dotnet_check.sh) — so they never touch UnityEngine.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using CoddyKit;

namespace CoddyKit.Tests
{
    public class MiniJsonTests
    {
        [Test]
        public void RoundTripKeepsTypesAndOrder()
        {
            var o = MiniJson.TryParseObject("{\"b\":1,\"a\":[true,null,2.5,\"x\\n\\u00e9\"],\"c\":{\"d\":-3}}");
            Assert.IsNotNull(o);
            Assert.AreEqual(new[] { "b", "a", "c" }, new List<string>(o.Keys).ToArray());
            Assert.AreEqual(1L, o["b"]);
            var a = (List<object>)o["a"];
            Assert.AreEqual(true, a[0]);
            Assert.IsNull(a[1]);
            Assert.AreEqual(2.5, a[2]);
            Assert.AreEqual("x\né", a[3]);
            Assert.AreEqual(-3, MiniJson.Int(MiniJson.Obj(o, "c"), "d"));
            var again = MiniJson.TryParseObject(MiniJson.Write(o));
            Assert.AreEqual(MiniJson.Write(o), MiniJson.Write(again));
        }

        [Test]
        public void BadJsonIsNullNotException()
        {
            Assert.IsNull(MiniJson.TryParse("{\"a\":"));
            Assert.IsNull(MiniJson.TryParse("[1,]x"));
            Assert.IsNull(MiniJson.TryParse(""));
            Assert.IsNull(MiniJson.TryParseObject("[1]"));
        }

        [Test]
        public void TypedGettersFallBack()
        {
            var o = MiniJson.TryParseObject("{\"n\":\"12\",\"f\":3.9,\"s\":5}");
            Assert.AreEqual(12, MiniJson.Int(o, "n"));
            Assert.AreEqual(3, MiniJson.Int(o, "f"));
            Assert.AreEqual("5", MiniJson.Str(o, "s"));
            Assert.AreEqual(7, MiniJson.Int(o, "missing", 7));
            Assert.IsTrue(MiniJson.Bool(o, "missing", true));
        }
    }

    public class SaveRepositoryTests
    {
        string dir;

        [SetUp] public void SetUp() { dir = Path.Combine(Path.GetTempPath(), "coddykit_save_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); }
        [TearDown] public void TearDown() { try { Directory.Delete(dir, true); } catch (IOException) { } }

        static string Valid(string t) => t.StartsWith("{", StringComparison.Ordinal) && MiniJson.TryParseObject(t) != null ? null : "invalid_json";

        [Test]
        public void WriteKeepsPreviousAsBak()
        {
            string p = Path.Combine(dir, "slot1.json");
            Assert.AreEqual("", SaveRepository.Write(p, "{\"v\":1}", Valid));
            Assert.AreEqual("", SaveRepository.Write(p, "{\"v\":2}", Valid));
            Assert.AreEqual("{\"v\":2}", File.ReadAllText(p));
            Assert.AreEqual("{\"v\":1}", File.ReadAllText(p + ".bak"));
            Assert.IsFalse(File.Exists(p + ".tmp"));
        }

        [Test]
        public void CorruptMainLoadsBakAndIsKeptAsUnreadable()
        {
            string p = Path.Combine(dir, "slot1.json");
            SaveRepository.Write(p, "{\"v\":1}", Valid);
            SaveRepository.Write(p, "{\"v\":2}", Valid);
            File.WriteAllText(p, "{\"v\":2,"); // torn write
            var r = SaveRepository.Load(p, Valid);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual("bak", r.Source);
            Assert.IsTrue(r.Recovered);
            Assert.AreEqual(new[] { "invalid_json" }, r.Unreadable.ToArray());
            Assert.AreEqual("{\"v\":2,", File.ReadAllText(p + ".unreadable"));
        }

        [Test]
        public void SavingOverACorruptMainNeverOverwritesTheGoodBak()
        {
            string p = Path.Combine(dir, "slot1.json");
            SaveRepository.Write(p, "{\"v\":1}", Valid);
            SaveRepository.Write(p, "{\"v\":2}", Valid);
            File.WriteAllText(p, "garbage");
            Assert.AreEqual("", SaveRepository.Write(p, "{\"v\":3}", Valid));
            Assert.AreEqual("{\"v\":1}", File.ReadAllText(p + ".bak"), ".bak must stay the last GOOD save");
            Assert.AreEqual("garbage", File.ReadAllText(p + ".unreadable"));
        }

        [Test]
        public void InterruptedWriteRecoversFromTmp()
        {
            string p = Path.Combine(dir, "slot1.json");
            SaveRepository.Write(p, "{\"v\":1}", Valid);
            // crash between "delete main" and "move tmp": only .tmp (complete) and .bak exist
            File.WriteAllText(p + ".tmp", "{\"v\":2}");
            File.Delete(p);
            var r = SaveRepository.Load(p, Valid);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual("tmp", r.Source);
            Assert.AreEqual("{\"v\":2}", r.Text);
            Assert.AreEqual(0, r.Unreadable.Count);
        }

        [Test]
        public void UnreadableCopiesRotateAndDeduplicate()
        {
            string p = Path.Combine(dir, "s.json");
            File.WriteAllText(p, "bad-1");
            SaveRepository.Load(p, Valid);
            SaveRepository.Load(p, Valid); // same body: no second copy
            Assert.IsFalse(File.Exists(p + ".unreadable.1"));
            File.WriteAllText(p, "bad-2");
            SaveRepository.Load(p, Valid);
            Assert.AreEqual("bad-1", File.ReadAllText(p + ".unreadable"));
            Assert.AreEqual("bad-2", File.ReadAllText(p + ".unreadable.1"));
            Assert.IsTrue(File.Exists(p), "an unreadable save is never deleted");
        }

        [Test]
        public void NothingToLoad()
        {
            var r = SaveRepository.Load(Path.Combine(dir, "none.json"), Valid);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("no_save", r.Error);
        }

        [Test]
        public void JsonStateFileRoundTrip()
        {
            var f = new JsonStateFile(Path.Combine(dir, "kit", "state.json"));
            f.Data["count"] = 3L;
            Assert.AreEqual("", f.Save());
            var g = new JsonStateFile(f.Path).Load();
            Assert.AreEqual(3, MiniJson.Int(g.Data, "count"));
        }
    }

    public class DeviceTierTests
    {
        static DeviceInfo Android(int ram, int cores, string gpu) => new DeviceInfo { Handheld = true, Os = "Android", RamMb = ram, Cores = cores, Gpu = gpu };

        [Test]
        public void FleetFixtures()
        {
            Assert.AreEqual(Tier.High, DeviceTier.DefaultTier(Android(11264, 8, "Adreno (TM) 830")));
            Assert.AreEqual(Tier.High, DeviceTier.DefaultTier(Android(7600, 8, "Adreno (TM) 730")));
            Assert.AreEqual(Tier.Medium, DeviceTier.DefaultTier(Android(7600, 8, "Adreno (TM) 725")));
            Assert.AreEqual(Tier.Medium, DeviceTier.DefaultTier(Android(5700, 8, "Adreno (TM) 660")));
            Assert.AreEqual(Tier.High, DeviceTier.DefaultTier(Android(11000, 8, "Mali-G720-Immortalis MC12")));
            Assert.AreEqual(Tier.Medium, DeviceTier.DefaultTier(Android(7600, 8, "Mali-G710 MC10")));
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(7600, 8, "Mali-G57 MC2")));
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(3800, 8, "Adreno (TM) 610")));
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(7600, 8, "PowerVR Rogue GE8320")));
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(7600, 8, "IMG BXM-8-256")), "Vitals 10-27 moto g64y adapter");
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(7600, 6, "Adreno (TM) 730")), "cores");
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(-1, 8, "Adreno (TM) 730")), "unknown RAM");
            Assert.AreEqual(Tier.Low, DeviceTier.DefaultTier(Android(7600, 8, "")), "unknown GPU");
            Assert.AreEqual(Tier.High, DeviceTier.DefaultTier(Android(12000, 8, "ANGLE (Samsung Xclipse 940) on Vulkan 1.3")));
            Assert.AreEqual(Tier.High, DeviceTier.DefaultTier(new DeviceInfo { Handheld = false, Os = "Desktop" }));
        }

        [Test]
        public void Meminfo()
        {
            Assert.AreEqual(5926, DeviceTier.ParseMeminfoMb("MemTotal:        6068684 kB\nMemFree: 1 kB"));
            Assert.AreEqual(-1, DeviceTier.ParseMeminfoMb("nothing"));
        }

        [Test]
        public void OneTimeCorrections()
        {
            Assert.AreEqual(Tier.Medium, DeviceTier.FixWrongLow(Tier.Low, Tier.Medium, true, false));
            Assert.AreEqual(Tier.Low, DeviceTier.FixWrongLow(Tier.Low, Tier.Medium, true, true), "player's own choice stands");
            Assert.AreEqual(Tier.Low, DeviceTier.FixWrongLow(Tier.Low, Tier.Low, true, false));
            Assert.AreEqual(Tier.High, DeviceTier.FlagshipUpgrade(Tier.Medium, Tier.High, true, false));
            Assert.AreEqual(Tier.Low, DeviceTier.FlagshipUpgrade(Tier.Low, Tier.High, true, false), "a PerfGuard step stays");
            Assert.AreEqual(Tier.Medium, DeviceTier.FlagshipUpgrade(Tier.Medium, Tier.Medium, true, false));
        }

        [Test]
        public void TierTableFollowsFleetRules()
        {
            foreach (Tier t in new[] { Tier.Low, Tier.Medium, Tier.High })
                Assert.AreEqual("", TierSpec.Validate(TierSpec.For(t)), t.ToString());
            var low = TierSpec.For(Tier.Low);
            Assert.AreEqual(1, low.Msaa);
            Assert.IsFalse(low.MainLightShadows);
            Assert.AreEqual(30, low.TargetFps);
            Assert.AreEqual(2, TierSpec.For(Tier.Medium).Msaa);
            Assert.AreEqual(4, TierSpec.For(Tier.High).Msaa);
            var bad = TierSpec.For(Tier.Low);
            bad.Msaa = 2;
            Assert.AreNotEqual("", TierSpec.Validate(bad));
        }

        [Test]
        public void PerfGuardStepsDownOnlyOnUncappedSlowFrames()
        {
            var g = new PerfGuard();
            for (int i = 0; i < 400; i++) Assert.IsFalse(g.Tick(1f / 20f, true, Tier.Medium), "capped frames are never slow");
            bool stepped = false;
            for (int i = 0; i < 199; i++) stepped |= g.Tick(1f / 20f, false, Tier.Medium);
            Assert.IsFalse(stepped, "9.95 s");
            Assert.IsTrue(g.Tick(1f / 20f, false, Tier.Medium), "10 s under 25 FPS");
            for (int i = 0; i < 400; i++) Assert.IsFalse(g.Tick(1f / 10f, false, Tier.Medium), "once per launch");
            Assert.IsFalse(new PerfGuard().Tick(1f, false, Tier.Low), "never below Low");
        }

        [Test]
        public void ShaderCrashBreadcrumb()
        {
            var clean = ShaderCrashGuard.AtBoot(null, false);
            Assert.IsFalse(clean.ReportCrash);
            Assert.IsFalse(clean.SafeShaders);
            var crashed = ShaderCrashGuard.AtBoot("warmup:VoltCity/World", false);
            Assert.IsTrue(crashed.ReportCrash);
            Assert.IsTrue(crashed.SafeShaders);
            Assert.IsTrue(ShaderCrashGuard.AtBoot(null, true).SafeShaders, "the flag persists");
        }
    }

    public class SafeAreaTests
    {
        [Test]
        public void NavBarIsClearedEvenWhenTheEngineReportsOnlyTheCutout()
        {
            var cutoutOnly = new IRect(0, 110, 1080, 2290); // engine: status/cutout only (immersive)
            var r = SafeAreaMath.Combine(cutoutOnly, 1080, 2400, new Insets(0, 0, 0, 126));
            Assert.AreEqual(110, r.Y);
            Assert.AreEqual(2400 - 126, r.Bottom);
            var e = SafeAreaMath.ToCanvas(r, 1080, 2400, 720, 1600);
            Assert.AreEqual(84f, e.Bottom, 0.01f);
            Assert.AreEqual(0f, e.Left, 0.01f);
        }

        [Test]
        public void UnityBottomLeftRectIsFlipped()
        {
            var r = SafeAreaMath.FromUnity(0, 126, 1080, 2164, 2400);
            Assert.AreEqual(110, r.Y);
            Assert.AreEqual(2274, r.Bottom);
        }

        [Test]
        public void OverlaysReserveTheStatusBar()
        {
            var full = new IRect(0, 0, 1080, 2400);
            var e = SafeAreaMath.OverlayEdges(full, 1080, 2400, 720, 1600, 96);
            Assert.AreEqual(64f, e.Top, 0.01f);
            Assert.AreEqual(0f, SafeAreaMath.ToCanvas(full, 1080, 2400, 720, 1600).Top, 0.01f);
        }

        [Test]
        public void NonsenseInsetsKeepTheEngineValue()
        {
            var safe = new IRect(0, 0, 1080, 2400);
            Assert.AreEqual(safe.H, SafeAreaMath.Combine(safe, 1080, 2400, new Insets(0, 0, 0, 5000)).H);
        }

        [Test]
        public void ContentBottomIsTheMaxOfSafeAreaAndBanner()
        {
            Assert.AreEqual(120f, SafeAreaMath.ContentBottom(84f, 120f));
            Assert.AreEqual(84f, SafeAreaMath.ContentBottom(84f, 0f));
        }

        [Test]
        public void ParsesAndroidInsetsAndSimulation()
        {
            var i = SafeAreaMath.ParseAndroidInsets("Insets{left=0, top=63, right=0, bottom=126}");
            Assert.AreEqual(new Insets(0, 63, 0, 126), i);
            Assert.IsTrue(SafeAreaMath.ParseSimulation("1080x2400:0,110,0,126", out int w, out int h, out Insets s));
            Assert.AreEqual(1080, w);
            Assert.AreEqual(126f, s.Bottom);
            Assert.IsFalse(SafeAreaMath.ParseSimulation("1080x2400", out _, out _, out _));
        }
    }

    public class TapFilterTests
    {
        static TapEvent E(float x, float y, double t, PointerDevice d = PointerDevice.Touch) => new TapEvent { X = x, Y = y, Time = t, Device = d };

        [Test]
        public void ShortStillPressIsOneTap()
        {
            var f = new TapFilter { PxPerDp = 2.75f };
            f.Press(E(100, 100, 0));
            f.Move(E(105, 102, 0.05));
            Assert.IsTrue(f.Release(E(105, 102, 0.12)));
        }

        [Test]
        public void PanLongPressAndPinchAreNotTaps()
        {
            var f = new TapFilter { PxPerDp = 2.75f };
            f.Press(E(100, 100, 0));
            f.Move(E(160, 100, 0.1));
            Assert.IsFalse(f.Release(E(100, 100, 0.2)), "moved >= 12 dp then back: still a pan");
            f.Press(E(100, 100, 1));
            Assert.IsFalse(f.Release(E(100, 100, 1.5)), "long press");
            f.Press(E(100, 100, 2));
            f.Press(E(300, 300, 2.01));
            Assert.IsFalse(f.Release(E(100, 100, 2.1)), "second finger = pinch");
        }

        [Test]
        public void EchoFromTheOtherDeviceIsDropped()
        {
            var f = new TapFilter { PxPerDp = 2.75f };
            f.Press(E(100, 100, 0));
            Assert.IsTrue(f.Release(E(100, 100, 0.08)));
            f.Press(E(101, 99, 0.1, PointerDevice.Mouse)); // the emulated mouse click of the same tap
            Assert.IsFalse(f.Release(E(101, 99, 0.12, PointerDevice.Mouse)));
            f.Press(E(100, 100, 0.2));
            Assert.IsTrue(f.Release(E(100, 100, 0.25)), "a real second tap by the finger still counts");
        }
    }

    public class ReviewGateTests
    {
        [Test]
        public void FirstSessionAfterFourMinutesAtAWowMoment()
        {
            var g = new ReviewGate();
            g.SessionStarted();
            var ok = new ReviewGate.Context();
            for (int i = 0; i < 239; i++) g.Tick(1.0);
            Assert.AreEqual("play_time", g.Blocker(ok, 1000));
            g.Tick(1.0);
            Assert.AreEqual("", g.Blocker(ok, 1000));
            Assert.AreEqual("tutorial", g.Blocker(new ReviewGate.Context { Tutorial = true }, 1000));
            Assert.AreEqual("window_open", g.Blocker(new ReviewGate.Context { WindowOpen = true }, 1000));
            Assert.AreEqual("background", g.Blocker(new ReviewGate.Context { Background = true }, 1000));
        }

        [Test]
        public void ThreeAttemptsThirtyDaysApart()
        {
            var g = new ReviewGate { Sessions = 1, PlayS = 999 };
            var c = new ReviewGate.Context();
            double t = 1_000_000;
            g.Asked(t);
            Assert.AreEqual("gap", g.Blocker(c, t + 29 * 86400));
            Assert.AreEqual("", g.Blocker(c, t + 30 * 86400));
            g.Asked(t + 30 * 86400);
            g.Asked(t + 60 * 86400);
            Assert.AreEqual("max_asks", g.Blocker(c, t + 400 * 86400));
            var back = ReviewGate.FromJson(MiniJson.TryParseObject(MiniJson.Write(g.ToJson())));
            Assert.AreEqual(3, back.Asks);
        }
    }

    public class PurchaseStateTests
    {
        [Test]
        public void AdFreeOnlyFromTheAdFreeEntitlements()
        {
            var c = new EntitlementCache(new[] { "noads" });
            c.Replace(new[] { "livery_sunset", "speed4" }, 1);
            Assert.IsFalse(c.AdFree, "cheap packs never remove ads (§3)");
            c.Replace(new[] { "noads", "forecast_plus" }, 2);
            Assert.IsTrue(c.AdFree);
            var back = new EntitlementCache(new[] { "noads" });
            back.FromJson(MiniJson.TryParseObject(MiniJson.Write(c.ToJson())));
            Assert.IsTrue(back.AdFree, "offline: the cached right survives a restart");
            Assert.IsTrue(back.Replace(new string[0], 3), "a refund removes it");
            Assert.IsFalse(back.AdFree);
        }

        [Test]
        public void ConsumablesOncePerTransactionAndDailyCap()
        {
            var l = new ConsumableLedger { DailyCap = 3 };
            double day = 1_800_000_000;
            Assert.IsTrue(l.Deliver("GPA.1", day));
            Assert.IsFalse(l.Deliver("GPA.1", day + 1), "same transaction");
            Assert.IsTrue(l.Deliver("GPA.2", day + 2));
            Assert.IsTrue(l.Deliver("GPA.3", day + 3));
            Assert.AreEqual("daily_cap", l.Blocker("GPA.4", day + 4));
            Assert.AreEqual("", l.Blocker("GPA.4", day + 86400));
            Assert.AreEqual("no_transaction_id", l.Blocker("", day));
        }
    }

    public class ToastQueueTests
    {
        [Test]
        public void HoldRangeMergeAndCompress()
        {
            Assert.AreEqual(2.5, ToastQueue.HoldFor("Hi"), 1e-9);
            Assert.AreEqual(5.5, ToastQueue.HoldFor(new string('x', 400)), 1e-9);
            var q = new ToastQueue();
            q.Push("Riverside is back on!");
            q.Push("Riverside is back on!");
            Assert.AreEqual(1, q.Count);
            Assert.AreEqual(2, q.Current.Repeats);
            q.Push("a"); q.Push("b"); q.Push("c");
            Assert.AreEqual(ToastQueue.MaxQueued, q.Count);
            Assert.AreEqual("Riverside is back on!", q.Current.Text, "the toast on screen is never dropped");
            Assert.IsTrue(q.Tick(10));
            Assert.AreEqual("b", q.Current.Text);
        }
    }
}
