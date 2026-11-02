// Engine-free QA: two independent careers, daily disk reload, bounded retained memory and seeded byte corruption.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CoddyKit;
using VoltCity.Sim;
using State = VoltCity.Sim.Simulation;

static class Robustness
{
    static GameData data;
    static void Need(bool ok, string message) { if (!ok) throw new Exception(message); }
    static State Fresh() => SimProbe.CareerAfterDays(data, 77, 2);
    static string Validate(string text) { var r = SaveCodec.Decode(text, data); return r.Ok ? null : r.Error; }
    static State Decode(string text) { var r = SaveCodec.Decode(text, data); Need(r.Ok, "decode: " + r.Error); return r.Sim; }
    static void Step(State s) { SimProbe.DriveStep(s); s.DrainEvents(); }
    static void Start(State s, int day) { Need(s.StartShift(), "start shift"); s.DrainEvents(); }
    static void Collect(State s) { s.Collect(); s.DrainEvents(); }
    static bool Live(State s) => s.Phase == "live";
    static long Heap() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); return GC.GetTotalMemory(true); }
    static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    static void Fuzz(string dir)
    {
        var state = Fresh(); Start(state, 0);
        for (int i = 0; i < 320; i++) Step(state);
        string good = SaveCodec.Encode(state);
        byte[] source = Encoding.UTF8.GetBytes(good);
        var rng = new Random(2305000);
        int accepted = 0, rejected = 0;
        string path = Path.Combine(dir, "fuzz.json");
        File.WriteAllText(path + ".bak", good);
        for (int i = 0; i < 5000; i++)
        {
            byte[] bad;
            if (i % 2 == 0) { bad = new byte[rng.Next(source.Length)]; Array.Copy(source, bad, bad.Length); }
            else { bad = (byte[])source.Clone(); int p = rng.Next(bad.Length); bad[p] ^= (byte)(1 << rng.Next(8)); }
            File.WriteAllBytes(path, bad);
            string mutated = File.ReadAllText(path);
            var decoded = SaveCodec.Decode(mutated, data);
            var loaded = SaveRepository.Load(path, Validate);
            Need(loaded.Ok, "fuzz backup lost at " + i);
            Need(File.ReadAllText(path + ".bak") == good, "fuzz changed backup at " + i);
            if (decoded.Ok)
            {
                accepted++;
                Need(!loaded.Recovered, "valid mutation unexpectedly recovered");
                string canonical = SaveCodec.Encode(decoded.Sim);
                Need(SaveCodec.Encode(Decode(canonical)) == canonical, "accepted mutation is not round-trip stable at " + i);
                // A bit-flip can still be a valid state; acceptance alone is not corruption detection.
                for (int k = 0; k < 8 && Live(decoded.Sim); k++) Step(decoded.Sim);
            }
            else
            {
                rejected++;
                Need(loaded.Recovered && loaded.Source == "bak" && loaded.Text == good, "wrong fallback at " + i);
                Need(loaded.KeptAs.Count > 0 && Convert.ToBase64String(File.ReadAllBytes(loaded.KeptAs[0])) == Convert.ToBase64String(bad), "unreadable bytes not preserved at " + i);
            }
        }
        Console.WriteLine($"SAVE_FUZZ cases=5000 truncations=2500 bit_flips=2500 accepted={accepted} rejected={rejected} exceptions=0 seed=2305000");
    }

    static void Soak(string dir, int seconds)
    {
        var reference = Fresh(); var resumed = Fresh();
        string path = Path.Combine(dir, "career.json");
        int days = 0, reloads = 0, samples = 0;
        long baseline = 0, maxHeap = 0, finalHeap = 0, firstRss = 0, maxRss = 0;
        var clock = Stopwatch.StartNew();
        double nextSample = 0;
        string last = "";
        do
        {
            Start(reference, days); Start(resumed, days);
            int steps = 0;
            while (Live(reference))
            {
                Need(Live(resumed), "reloaded career ended early");
                Step(reference); Step(resumed);
                Need(++steps < 100000, "day failed to finish");
                if (steps == 320) // a live save in addition to the mandatory end-of-day save
                {
                    string mid = SaveCodec.Encode(resumed);
                    Need(SaveRepository.Write(path, mid, Validate) == "", "mid-day disk write");
                    var load = SaveRepository.Load(path, Validate);
                    Need(load.Ok && load.Text == mid, "mid-day disk read");
                    resumed = Decode(load.Text); reloads++;
                    Need(SaveCodec.Encode(resumed) == mid, "mid roundtrip day " + days);

                }
            }
            Need(!Live(resumed), "reloaded career ended late");
            string expected = SaveCodec.Encode(reference), actual = SaveCodec.Encode(resumed);
            if (expected != actual)
            {
                int at = 0; while (at < Math.Min(expected.Length, actual.Length) && expected[at] == actual[at]) at++;
                throw new Exception("determinism at report day " + days + " offset=" + at + " expected=" + expected.Substring(Math.Max(0, at - 60), Math.Min(200, expected.Length - Math.Max(0, at - 60))) + " actual=" + actual.Substring(Math.Max(0, at - 60), Math.Min(200, actual.Length - Math.Max(0, at - 60))));
            }
            Collect(reference); Collect(resumed); days++;
            last = SaveCodec.Encode(resumed);
            Need(SaveRepository.Write(path, last, Validate) == "", "daily disk write");
            var saved = SaveRepository.Load(path, Validate);
            Need(saved.Ok && saved.Source == "main", "daily disk read");
            resumed = Decode(saved.Text); reloads++;
            Need(SaveCodec.Encode(reference) == SaveCodec.Encode(resumed), "determinism after reload day " + days);
            // Warm up caches and the bounded report history. Measure retained objects, not total allocations.
            if (days >= 64 && clock.Elapsed.TotalSeconds >= nextSample)
            {
                finalHeap = Heap();
                using (var p = Process.GetCurrentProcess())
                {
                    p.Refresh(); long rss = p.WorkingSet64;
                    if (samples == 0) { baseline = finalHeap; firstRss = rss; }
                    maxRss = Math.Max(maxRss, rss);
                }
                maxHeap = Math.Max(maxHeap, finalHeap); samples++;
                Need(finalHeap <= baseline + 8 * 1024 * 1024, "retained heap grew over 8 MiB after warmup");
                Console.WriteLine($"SOAK_SAMPLE elapsed_s={clock.Elapsed.TotalSeconds:F1} days={days} heap_bytes={finalHeap} baseline_bytes={baseline}");
                nextSample = clock.Elapsed.TotalSeconds + Math.Min(30, Math.Max(1, seconds / 10));
            }
        } while (clock.Elapsed.TotalSeconds < seconds || days < 128 || samples < 2);
        finalHeap = Heap();
        Need(finalHeap <= baseline + 8 * 1024 * 1024, "final retained heap growth");
        Console.WriteLine($"SIM_SOAK PASS elapsed_s={clock.Elapsed.TotalSeconds:F1} requested_s={seconds} days={days} reloads={reloads} deterministic_days={days} samples={samples} heap_baseline={baseline} heap_final={finalHeap} heap_peak={maxHeap} rss_baseline={firstRss} rss_peak={maxRss} sha256={Hash(last)}");
    }

    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "volt-city-robustness-" + Guid.NewGuid().ToString("N"));
        try
        {
            data = GameData.LoadDirectory(args[0]); Need(data.Ok, string.Join(";", data.Errors));
            if (args.Length > 1 && args[1] == "hash") { Console.WriteLine("DETERMINISM_HASH " + SimProbe.ScriptedShiftHash(data, 77)); return 0; }
            int seconds = args.Length > 1 ? int.Parse(args[1]) : 10;
            Need(seconds >= 1 && seconds <= 86400, "seconds must be 1..86400");
            Directory.CreateDirectory(dir);
            Fuzz(dir); Soak(dir, seconds);
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("ROBUSTNESS FAIL " + e); return 1; }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
