// Docket Day — the 2.5D diorama of the firm's street in Calderport (plan §8): the office tower cutaway on the left (one
// open floor per chapter: the Garage Front, then Main Street Suite, then Courthouse Row; the front wall is cut away so the
// desks, the people and the rooms you build are visible), the street, and the courthouse across it on the right.
//   * built ONCE from the sim (floors, desks, rooms), rebuilt only when something structural changes (chapter, hire / fire,
//     a room finished, a cosmetic); people update from the sim at 30 Hz (Low: 15 Hz, ≤ 30 people, half the walkers);
//   * one palette material for everything (MeshKit / Blender kit) + instanced people; the Meshy office cat is the one
//     textured hero prop (sitting on the front step; LOD1 / 512 texture on Low);
//   * Low tier: no shadows (QualityTiers), only the active floor's people animated, no street props beyond lamps;
//   * picking: a tap ray-casts the ground plane (no physics module) and picks reception / the courthouse / the office.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using DocketDay.Sim;

namespace DocketDay.World
{
    public struct WorldHit
    {
        public string Kind;   // reception | courthouse | office | cat | none
        public int Index;
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class RegionWorld : MonoBehaviour
    {
        public const float FloorH = 6f;
        public static readonly Vector3 ReceptionPos = new Vector3(-12f, 0f, 9f);
        public static readonly Vector3 CourtDoor = new Vector3(22f, 0f, 9f);
        public static readonly Vector3 TowerCenter = new Vector3(-20f, 0f, 16f);
        public static readonly Vector3 CourtCenter = new Vector3(22f, 0f, 16f);
        public static readonly Vector3 StreetWest = new Vector3(-48f, 0f, 2f), StreetEast = new Vector3(52f, 0f, 2f);

        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }

        Material mat, matInst, matCat;
        GameObject towerObj, courtObj, propsObj, catObj;
        readonly List<GameObject> roomObjs = new List<GameObject>();
        Crowd crowd;
        int builtChapter = -1, builtStaff = -1, builtRooms = -1, builtIntake = -1, builtSession = -1;
        string builtTheme = "~", builtCourt = "~";
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;
        public string OfficeTheme = "";
        public bool CourtTheme, Outfits;

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            BuildStreet();
            crowd = new Crowd(matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            if (rig != null) rig.Fit(ViewBounds(), true);
            MarkDirty();
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            builtChapter = -1;
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        /// <summary>Cosmetics (office style, courtroom theme, suits & gowns) — owned + equipped, from the screen.</summary>
        public void SetLooks(string officeTheme, bool courtTheme, bool outfits)
        {
            OfficeTheme = officeTheme ?? "";
            CourtTheme = courtTheme;
            Outfits = outfits;
            MarkDirty();
        }

        // ------------------------------------------------------------------ static scene

        void BuildStreet()
        {
            var k = new MeshKit();
            k.Quad(new Vector3(-70, 0, -40), new Vector3(-70, 0, 60), new Vector3(80, 0, 60), new Vector3(80, 0, -40), Pal.Grass);
            k.Strip(new Vector3(-70, 0, 0), new Vector3(80, 0, 0), 10f, 0.05f, Pal.Road);
            for (float x = -66; x < 78; x += 8) k.Strip(new Vector3(x, 0, 0), new Vector3(x + 3.5f, 0, 0), 0.35f, 0.07f, Pal.RoadLine);
            k.Strip(new Vector3(-70, 0, 6.5f), new Vector3(80, 0, 6.5f), 3f, 0.08f, Pal.Concrete);
            k.Strip(new Vector3(-70, 0, -6.5f), new Vector3(80, 0, -6.5f), 3f, 0.08f, Pal.Concrete);
            k.Strip(new Vector3(4, 0, -4.5f), new Vector3(4, 0, 4.5f), 3.2f, 0.09f, Pal.RoadLine);   // the crossing between the firm and the court
            // a row of harbour houses behind (scenery, one mesh)
            var rng = new System.Random(7);
            int[] walls = { Pal.WallCream, Pal.WallBrick, Pal.WallBlue, Pal.WallCream };
            for (float x = -64; x < 76; x += 10)
            {
                if (x > -36 && x < 40) continue;   // the firm + the courthouse plots
                float h = 7 + (float)rng.NextDouble() * 9;
                k.Box(new Vector3(x, 0, 16), new Vector3(8.5f, h, 12), 0, walls[rng.Next(walls.Length)], rng.NextDouble() < 0.5 ? Pal.RoofRed : Pal.RoofGrey);
                k.Box(new Vector3(x, h * 0.55f, 9.95f), new Vector3(5.5f, 1.4f, 0.1f), 0, Pal.Window);
            }
            for (float x = -60; x < 76; x += 14) k.Tree(new Vector3(x, 0, -11f), 1.3f, x * 7);
            Spawn("Street", k.Build("street"), Vector3.zero, transform);
            propsObj = new GameObject("StreetProps");
            propsObj.transform.SetParent(transform, false);
            for (float x = -40; x <= 44; x += 14) Prop("lamp", MeshKit.Lamp, new Vector3(x, 0, 5.4f), 0, propsObj.transform);
            Prop("bench", MeshKit.Bench, new Vector3(-30, 0, -6.8f), 180, propsObj.transform);
            Prop("bench", MeshKit.Bench, new Vector3(10, 0, 6.8f), 0, propsObj.transform);
        }

        GameObject Spawn(string name, Mesh mesh, Vector3 pos, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = !Low;
            return go;
        }

        /// <summary>Blender-kit model when present (Resources/kit/&lt;name&gt;), else the procedural fallback.</summary>
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parent);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
                go.transform.localPosition = pos;
            }
            else go = Spawn(kitName, fallback(), pos, parent);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        int Floors => Mathf.Clamp(Sim.Chapter, 1, 3);

        int WallSwatch()
        {
            switch (OfficeTheme)
            {
                case "theme_classic": return Pal.Classic;
                case "theme_loft": return Pal.Loft;
                case "theme_deco": return Pal.Deco;
                case "gold_nameplate": return Pal.Brand;
            }
            return Pal.WallCream;
        }

        void BuildTower()
        {
            if (towerObj != null) Destroy(towerObj);
            towerObj = new GameObject("Tower");
            towerObj.transform.SetParent(transform, false);
            var k = new MeshKit();
            int wall = WallSwatch();
            int trim = OfficeTheme == "theme_deco" || OfficeTheme == "gold_nameplate" ? Pal.Gold : Pal.Wood;
            float x0 = -34f, x1 = -6f, z0 = 9.5f, z1 = 24f;
            for (int f = 0; f < Floors; f++)
            {
                float y = f * FloorH;
                k.Box(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.4f, z1 - z0), 0, Pal.Carpet, f == 0 ? Pal.Wood : Pal.Carpet);   // floor slab
                k.Box(new Vector3((x0 + x1) * 0.5f, y, z1), new Vector3(x1 - x0, FloorH, 0.6f), 0, wall);                                            // back wall
                k.Box(new Vector3(x0, y, (z0 + z1) * 0.5f), new Vector3(0.6f, FloorH, z1 - z0), 0, wall);                                            // side walls
                k.Box(new Vector3(x1, y, (z0 + z1) * 0.5f), new Vector3(0.6f, FloorH, z1 - z0), 0, wall);
                k.Box(new Vector3((x0 + x1) * 0.5f, y + FloorH * 0.55f, z1 - 0.35f), new Vector3((x1 - x0) * 0.7f, 1.6f, 0.1f), 0, Pal.Window);   // back windows
                k.Box(new Vector3((x0 + x1) * 0.5f, y + FloorH - 0.6f, z0), new Vector3(x1 - x0, 0.6f, 0.4f), 0, trim);                             // cut-away lintel
            }
            k.Box(new Vector3((x0 + x1) * 0.5f, Floors * FloorH, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 1, 0.6f, z1 - z0 + 1), 0, Pal.RoofGrey);   // roof
            k.Box(new Vector3((x0 + x1) * 0.5f, Floors * FloorH + 0.6f, z0 + 0.6f), new Vector3(10f, 1.6f, 0.3f), 0, OfficeTheme == "gold_nameplate" ? Pal.Gold : Pal.Brand);   // sign board (no text)
            k.Box(new Vector3(-12f, 0, 8.4f), new Vector3(4f, 0.25f, 2.2f), 0, Pal.Concrete);   // front step
            Spawn("TowerShell", k.Build("tower"), Vector3.zero, towerObj.transform);
            Prop("reception", MeshKit.Reception, ReceptionPos + new Vector3(0, 0.4f, 2.5f), 180, towerObj.transform);
            for (int i = 0; i < DeskCount; i++) Prop("desk", MeshKit.Desk, DeskPos(i) + new Vector3(0, 0, 1.2f), 180, towerObj.transform);
            if (catObj == null) BuildCat();
            builtTheme = OfficeTheme;
        }

        int DeskCount => Mathf.Min(Floors * 6, Math.Max(Sim.DeskCap, Sim.S.Staff.Count));

        /// <summary>Desk i: six per floor in two rows (front row nearer the camera).</summary>
        public Vector3 DeskPos(int i)
        {
            int f = Mathf.Clamp(i / 6, 0, 2), j = i % 6;
            return new Vector3(-30f + (j % 3) * 7f, f * FloorH + 0.4f, 12.5f + (j / 3) * 5f);
        }

        void BuildRooms()
        {
            foreach (var go in roomObjs) Destroy(go);
            roomObjs.Clear();
            void R(string id, string kit, Func<Mesh> fb, Vector3 pos, float yaw = 0)
            {
                if (!Sim.RoomBuilt(id)) return;
                roomObjs.Add(Prop(kit, fb, pos, yaw, transform));
            }
            float top = (Floors - 1) * FloorH + 0.4f;
            R("library", "bookshelf", MeshKit.Bookshelf, new Vector3(-31f, 0.4f, 22.8f));
            R("coffee", "coffee", MeshKit.Coffee, new Vector3(-8.5f, 0.4f, 22.5f));
            R("docroom", "cabinet", MeshKit.Cabinet, new Vector3(-26f, 0.4f, 22.8f));
            R("evidence", "boxes", MeshKit.Boxes, new Vector3(-9f, Mathf.Min(top, FloorH + 0.4f), 21f));
            R("conference", "conference", MeshKit.Conference, new Vector3(-16f, Mathf.Min(top, FloorH + 0.4f), 21f));
            R("war_room", "board", MeshKit.Board, new Vector3(-20f, top, 23.4f));
            builtRooms = RoomKey();
        }

        int RoomKey() { int n = 0; foreach (var r in Sim.S.Rooms) n = n * 7 + r.Count + 1; return n + Sim.S.Rooms.Count * 1000; }

        void BuildCourt()
        {
            if (courtObj != null) Destroy(courtObj);
            courtObj = Prop("courthouse", MeshKit.Courthouse, new Vector3(22f, 0, 8.2f), 0, transform);
            courtObj.name = "Courthouse";
            // courtroom theme: a warmer, grander stone (MaterialPropertyBlock tint, same material = same batch)
            Color tint = CourtTheme ? new Color(1f, 0.93f, 0.8f) : Color.white;
            foreach (var r in courtObj.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tint);
                r.SetPropertyBlock(mpb);
            }
            builtCourt = CourtTheme ? "on" : "off";
        }

        void BuildCat()
        {
            var cat = Resources.Load<GameObject>("kit/office_cat_lod" + (Low ? "2" : "1"));
            if (cat == null) return;
            catObj = Instantiate(cat, transform);
            catObj.name = "OfficeCat";
            catObj.transform.localPosition = new Vector3(-9.6f, 0.25f, 8.2f);
            catObj.transform.localRotation = Quaternion.Euler(0, 200, 0);
            catObj.transform.localScale = Vector3.one * 1.6f;   // drawn larger than life so it reads at diorama zoom
            matCat = new Material(mat) { name = "office_cat" };
            var tex = Resources.Load<Texture2D>("kit/office_cat_base" + (Low ? "_low" : ""));
            if (tex != null) { matCat.mainTexture = tex; if (matCat.HasProperty("_BaseMap")) matCat.SetTexture("_BaseMap", tex); }
            foreach (var r in catObj.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = matCat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        }

        public List<Bounds> ViewBounds()
        {
            var b = new Bounds(new Vector3(0, 0, 12), new Vector3(80, 10, 34));
            return new List<Bounds> { b };
        }

        // ------------------------------------------------------------------ people

        public Vector3 StaffPos(int index)
        {
            var s = index >= 0 && index < Sim.S.Staff.Count ? Sim.S.Staff[index] : null;
            if (s != null && Sim.InSession(s.Id)) return CourtDoor + new Vector3(-3f + (index % 3) * 3f, 2.2f, 2.5f);
            if (s != null && s.Role == "receptionist") return ReceptionPos + new Vector3(Sim.CountRole("receptionist") > 1 && index % 2 == 1 ? 1.6f : -1.2f, 0.4f, 4f);
            return DeskPos(index) + new Vector3(0, 0, 2.2f);   // behind the desk (the desk faces the camera), facing the street
        }

        void SyncPeople()
        {
            crowd.ClearStanding();
            int lowFloor = Floors - 1;
            for (int i = 0; i < Sim.S.Staff.Count; i++)
            {
                var s = Sim.S.Staff[i];
                if (s.Sick) continue;
                bool busy = Sim.Phase == Phases.Live && s.HoursLeft > 0 && Sim.NextTaskOf(s.Id).m != null;
                if (Low && DeskPos(i).y > lowFloor * FloorH + 1f) busy = false;   // Low: only the active (top) floor animates
                crowd.Stand(StaffPos(i), 180f, Crowd.RoleMesh(s.Role, Outfits), busy);
            }
            int q = 0;
            foreach (var m in Sim.Intakes)
            {
                if (q >= 4) break;
                crowd.Stand(ReceptionPos + new Vector3(-1.5f + q * 1.6f, 0.4f, 0.4f - q * 0.2f), 0f, Crowd.ClientA + m.Id % 3, false);
                q++;
            }
            builtStaff = StaffKey();
            builtIntake = Sim.IntakeCount;
            builtSession = SessionKey();
        }

        int StaffKey() { int k = Sim.S.Staff.Count; foreach (var s in Sim.S.Staff) k = k * 31 + s.Id * 3 + (s.Sick ? 1 : 0); return k; }
        int SessionKey() { int k = 0; foreach (var m in Sim.S.Matters) if (m.Status == MatterStatus.Hearing) k = k * 17 + m.Id; return k; }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Sim == null) return;
            if (dirty)
            {
                dirty = false;
                if (builtChapter != Sim.Chapter || builtTheme != OfficeTheme || builtStaff != StaffKey())
                {
                    bool grew = builtChapter >= 0 && builtChapter != Sim.Chapter;
                    BuildTower();
                    BuildRooms();
                    if (grew && rig != null) rig.Fit(ViewBounds(), false);
                    builtChapter = Sim.Chapter;
                }
                if (builtRooms != RoomKey()) BuildRooms();
                if (courtObj == null || builtCourt != (CourtTheme ? "on" : "off")) BuildCourt();
                SyncPeople();
            }
            float dt = Time.deltaTime;
            slowAcc += dt;
            bool tick = slowAcc >= (Low ? 1f / 15f : 1f / 30f);
            if (tick)
            {
                float step = slowAcc;
                slowAcc = 0f;
                if (builtIntake != Sim.IntakeCount || builtSession != SessionKey() || Sim.Phase == Phases.Live) SyncPeople();
                crowd.Tick(step);
            }
            crowd.Draw();
        }

        /// <summary>Sim events -> walkers (clients arriving / leaving, a lawyer crossing to court) and rebuilds.</summary>
        public void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case "walkin":
                    crowd.Walk(StreetWest + new Vector3(0, 0, 5f), ReceptionPos + new Vector3(0, 0.4f, 0.4f), Crowd.ClientA + Math.Abs(e.A) % 3);
                    break;
                case "walked_out":
                case "declined":
                case "referred":
                    crowd.Walk(ReceptionPos + new Vector3(0, 0.4f, 0.4f), StreetWest + new Vector3(0, 0, 5f), Crowd.ClientB);
                    break;
                case "hearing_start":
                {
                    int idx = e.B >= 0 ? Sim.S.Staff.FindIndex(s => s.Id == e.B) : -1;
                    var from = idx >= 0 ? DeskPos(idx) : ReceptionPos;
                    crowd.Walk(from, CourtDoor + new Vector3(0, 2.2f, 2.5f), idx >= 0 ? Crowd.RoleMesh(Sim.S.Staff[idx].Role, Outfits) : Crowd.Suit);
                    break;
                }
                case "verdict":
                case "settled":
                    crowd.Walk(CourtDoor + new Vector3(0, 0, 0), StreetEast + new Vector3(0, 0, 5f), Crowd.ClientC);
                    break;
                case "hired": case "fired": case "temp_left": case "room_done": case "chapter": case "day_start": case "crisis":
                    MarkDirty();
                    break;
            }
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Reception wins over the office, the court over the street.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Index = -1 };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            // test a few heights: the ground and each floor of the tower (cutaway floors are tapped from above)
            for (int f = Floors - 1; f >= -1; f--)
            {
                float y = f < 0 ? 0f : f * FloorH + 0.4f;
                if (Mathf.Abs(ray.direction.y) < 1e-4f) break;
                float dist = (y - ray.origin.y) / ray.direction.y;
                if (dist < 0) continue;
                var g = ray.origin + ray.direction * dist;
                if (f < 0)
                {
                    if ((g - ReceptionPos).sqrMagnitude < 8f * 8f) return new WorldHit { Kind = "reception", Index = 0 };
                    if (catObj != null && (g - catObj.transform.localPosition).sqrMagnitude < 3f * 3f) return new WorldHit { Kind = "cat", Index = 0 };
                    if (g.x > 8f && g.x < 36f && g.z > 4f && g.z < 28f) return new WorldHit { Kind = "courthouse", Index = 0 };
                    if (g.x > -36f && g.x < -4f && g.z > 8f && g.z < 26f) return new WorldHit { Kind = "office", Index = 0 };
                }
                else if (g.x > -34f && g.x < -6f && g.z > 9.5f && g.z < 24f)
                {
                    if (f == 0 && (g - ReceptionPos).sqrMagnitude < 8f * 8f) return new WorldHit { Kind = "reception", Index = 0 };
                    return new WorldHit { Kind = "office", Index = f };
                }
            }
            return none;
        }

        /// <summary>Screen position (top-left px) of a world point, or null when behind the camera.</summary>
        public Vector2? ScreenOf(Vector3 world)
        {
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            if (s.z <= 0) return null;
            return new Vector2(s.x, Screen.height - s.y);
        }

        public Vector3 BuildingMarkerPos => TowerCenter + new Vector3(0, Floors * FloorH + 4f, -4f);
    }
}
