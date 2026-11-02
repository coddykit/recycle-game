// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System.Collections.Generic;

namespace VoltCity.Sim
{
    /// <summary>
    /// A player (or dispatcher-assistant) command. Validated when queued, applied at the next fixed step
    /// (LL Pit Lane Boss #133: the result never depends on the exact millisecond of the tap).
    /// Types: ramp {Plant, Dir, N} · power {Plant=gas, On} · battery {Mode} · crew {Fault, Crew} · shed / restore {District}
    /// · dr · import (rewarded) · overtime {Fault} (rewarded) · script_fault {Corridor} (tutorial only).
    /// </summary>
    public sealed class Cmd
    {
        public static readonly string[] Types = { "ramp", "power", "battery", "crew", "shed", "restore", "dr", "import", "overtime", "script_fault" };

        public string Type = "", Plant = "", Mode = "", District = "", Corridor = "";
        public int Dir = 1, N = 1, Fault = -1, Crew = -1;
        public bool On = true;

        public static Cmd Ramp(string plant, int dir, int n = 1) => new Cmd { Type = "ramp", Plant = plant, Dir = dir, N = n };
        public static Cmd Power(bool on) => new Cmd { Type = "power", Plant = "gas", On = on };
        public static Cmd Battery(string mode) => new Cmd { Type = "battery", Mode = mode };
        public static Cmd SendCrew(int fault, int crew = -1) => new Cmd { Type = "crew", Fault = fault, Crew = crew };
        public static Cmd Shed(string district) => new Cmd { Type = "shed", District = district };
        public static Cmd Restore(string district) => new Cmd { Type = "restore", District = district };
        public static Cmd DemandResponse() => new Cmd { Type = "dr" };
        public static Cmd Import() => new Cmd { Type = "import" };
        public static Cmd Overtime(int fault) => new Cmd { Type = "overtime", Fault = fault };
        public static Cmd ScriptFault(string corridor) => new Cmd { Type = "script_fault", Corridor = corridor };

        public Cmd Copy() => (Cmd)MemberwiseClone();

        public JObj ToJson()
        {
            var o = new JObj { ["type"] = Type };
            switch (Type)
            {
                case "ramp": o["plant"] = Plant; o["dir"] = (long)Dir; o["n"] = (long)N; break;
                case "power": o["plant"] = Plant; o["on"] = On; break;
                case "battery": o["mode"] = Mode; break;
                case "crew": o["fault"] = (long)Fault; o["crew"] = (long)Crew; break;
                case "shed": case "restore": o["district"] = District; break;
                case "overtime": o["fault"] = (long)Fault; break;
                case "script_fault": o["corridor"] = Corridor; break;
            }
            return o;
        }

        public static Cmd FromJson(object o)
        {
            if (!(o is JObj)) return null;
            var c = new Cmd
            {
                Type = J.S(o, "type"), Plant = J.S(o, "plant"), Mode = J.S(o, "mode"), District = J.S(o, "district"), Corridor = J.S(o, "corridor"),
                Dir = J.I(o, "dir", 1), N = J.I(o, "n", 1), Fault = J.I(o, "fault", -1), Crew = J.I(o, "crew", -1), On = J.B(o, "on", true)
            };
            return System.Array.IndexOf(Types, c.Type) >= 0 ? c : null;
        }
    }

    /// <summary>Something the UI / analytics / tests can react to (drained every frame).</summary>
    public sealed class SimEvent
    {
        public string Type;
        public int Step;
        public JObj Data = new JObj();

        public SimEvent(string type, int step = 0) { Type = type; Step = step; }

        public SimEvent With(string key, object value)
        {
            Data[key] = value is int i ? (long)i : value;
            return this;
        }

        public string Str(string key) => J.S(Data, key);
        public long Num(string key) => J.L(Data, key);
        public override string ToString() => Type + " " + Json.Write(Data);
    }

    /// <summary>State a shift hands to the next (setpoints, gas on/off, battery charge + mode, reservoir).</summary>
    public sealed class Carry
    {
        public Dictionary<string, int> Steps = new Dictionary<string, int>();
        public int GasOn, BatMode = 3;
        public long BatSoc, ResWh;

        public Carry Copy()
        {
            var c = (Carry)MemberwiseClone();
            c.Steps = new Dictionary<string, int>(Steps);
            return c;
        }

        public JObj ToJson()
        {
            var st = new JObj();
            foreach (var p in GameData.Plants) if (Steps.TryGetValue(p, out int v)) st[p] = (long)v;
            return new JObj { ["steps"] = st, ["gas_on"] = (long)GasOn, ["bat_soc"] = BatSoc, ["bat_mode"] = (long)BatMode, ["res_wh"] = ResWh };
        }

        public static Carry FromJson(object o)
        {
            var c = new Carry { GasOn = J.I(o, "gas_on"), BatMode = J.I(o, "bat_mode", 3), BatSoc = J.L(o, "bat_soc"), ResWh = J.L(o, "res_wh") };
            var st = J.Obj(o, "steps");
            if (st != null) foreach (var k in st.Keys) c.Steps[k] = J.I(st[k]);
            return c;
        }
    }

    /// <summary>The static part of a live shift (what the career owned when it started).</summary>
    public sealed class ShiftContext
    {
        public Dictionary<string, int> HookedGrowthPm = new Dictionary<string, int>();
        public Dictionary<string, int> Plants = new Dictionary<string, int>();
        public Dictionary<string, int> Lines = new Dictionary<string, int>();
        public List<string> Ties = new List<string>();
        public int Crews = 1, CrewSpeed, Dr;
        public Carry Carry = new Carry();
    }

    public sealed class Fault
    {
        public int Id, Corridor = -1, District = -1, Step, Crew = -1, Repair, Done = -1, Part;
        public string Kind = "line", Site = "", Cause = "";
        public bool Open => Done < 0;

        public Fault Copy() => (Fault)MemberwiseClone();

        public JObj ToJson() => new JObj
        {
            ["id"] = (long)Id, ["kind"] = Kind, ["corridor"] = (long)Corridor, ["district"] = (long)District, ["site"] = Site, ["step"] = (long)Step,
            ["crew"] = (long)Crew, ["repair"] = (long)Repair, ["done"] = (long)Done, ["part"] = (long)Part, ["cause"] = Cause
        };

        public static Fault FromJson(object o) => new Fault
        {
            Id = J.I(o, "id"), Kind = J.S(o, "kind"), Corridor = J.I(o, "corridor", -1), District = J.I(o, "district", -1), Site = J.S(o, "site"),
            Step = J.I(o, "step"), Crew = J.I(o, "crew", -1), Repair = J.I(o, "repair"), Done = J.I(o, "done", -1), Part = J.I(o, "part"), Cause = J.S(o, "cause")
        };
    }

    public sealed class CrewTruck
    {
        public const int Idle = 0, Travel = 1, Repair = 2, Return = 3;
        public int Id, State, Until, Job = -1;
        public string Node = "";

        public CrewTruck Copy() => (CrewTruck)MemberwiseClone();

        public JObj ToJson() => new JObj { ["id"] = (long)Id, ["node"] = Node, ["state"] = (long)State, ["until"] = (long)Until, ["job"] = (long)Job };

        public static CrewTruck FromJson(object o) => new CrewTruck
        {
            Id = J.I(o, "id"), Node = J.S(o, "node"), State = J.I(o, "state"), Until = J.I(o, "until"), Job = J.I(o, "job", -1)
        };
    }
}
