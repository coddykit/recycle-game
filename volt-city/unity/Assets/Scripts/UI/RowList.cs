// Volt City UI — renders a TabView's rows into a finger-scrolling kit list and keeps it in sync WITHOUT rebuilding:
// rows are matched by Row.Id (UiReconcile pattern): new ids are added, gone ids removed, order fixed by moving elements,
// an unchanged row (same Sig) is not touched, a changed row updates its texts / badge / bar / pill states in place, and
// only a changed pill SHAPE rebuilds that card's side control. While a finger is down on the list (UiKit.Held) nothing
// is touched (W62/W63: never rebuild under a finger). Every pill / card body registers its coach target id.
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections.Generic;
using CoddyKit.Ui;
using UnityEngine.UIElements;
using VoltCity.Game;
using TabView = VoltCity.Game.TabView; // Unity 6 UI Toolkit also has a TabView control

namespace VoltCity.UI
{
    public sealed class RowList
    {
        sealed class Entry
        {
            public VisualElement Root;
            public UiKit.ListCard Card;
            public Label Badge;
            public VisualElement Bar, BarFill;
            public SideControl Side;
            public string Sig = "", Shape = "", Open = "", Target = "";
            public bool IsSection;
        }

        public readonly ScrollView View;
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly List<string> order = new List<string>();
        readonly Func<RowAction, bool> run;
        readonly Action<string, string> open;
        VisualElement empty;
        /// <summary>Coach targets in this list: target id -> element (pill or card).</summary>
        public readonly Dictionary<string, VisualElement> Targets = new Dictionary<string, VisualElement>();

        /// <param name="run">a pill was pressed (the screen gates it through the coach and runs the action)</param>
        /// <param name="open">a card body was tapped: (open id, target id)</param>
        public RowList(ScrollView view, Func<RowAction, bool> run, Action<string, string> open)
        {
            View = view ?? UiKit.List();
            this.run = run;
            this.open = open;
        }

        public bool Held => UiKit.Held(View);

        public void Clear()
        {
            View.Clear();
            entries.Clear();
            order.Clear();
            Targets.Clear();
            empty = null;
        }

        /// <summary>Reconciles the list with tv.Rows. Returns false when it waited for a finger to lift.</summary>
        public bool Render(TabView tv, string emptyTitle = null, string emptyBody = null)
        {
            if (Held) return false;
            var want = new List<string>();
            var rows = new Dictionary<string, Row>();
            string section = null;
            foreach (var r in tv.Rows)
            {
                if (!string.IsNullOrEmpty(r.Section) && r.Section != section)
                {
                    section = r.Section;
                    want.Add("§" + section);
                }
                string id = r.Id;
                int dup = 1;
                while (rows.ContainsKey(id)) id = r.Id + "#" + (++dup);
                rows[id] = r;
                want.Add(id);
            }
            // empty state
            if (tv.Rows.Count == 0 && !string.IsNullOrEmpty(emptyTitle))
            {
                if (empty == null) { empty = UiKit.EmptyCard(emptyTitle, emptyBody ?? ""); View.Add(empty); }
            }
            else if (empty != null) { empty.RemoveFromHierarchy(); empty = null; }
            // remove
            var gone = new List<string>();
            foreach (var k in entries.Keys) if (!want.Contains(k)) gone.Add(k);
            foreach (var k in gone)
            {
                entries[k].Root.RemoveFromHierarchy();
                entries.Remove(k);
            }
            // add / update
            foreach (var k in want)
            {
                if (k.StartsWith("§"))
                {
                    if (!entries.ContainsKey(k)) entries[k] = new Entry { Root = UiKit.SectionHeader(k.Substring(1)), IsSection = true };
                    continue;
                }
                var r = rows[k];
                if (!entries.TryGetValue(k, out var e)) { e = Build(r); entries[k] = e; }
                else if (e.Sig != r.Sig()) Update(e, r);
            }
            // order (move only when it differs)
            bool same = order.Count == want.Count;
            for (int i = 0; same && i < want.Count; i++) same = order[i] == want[i];
            if (!same)
            {
                var content = View.contentContainer;
                for (int i = 0; i < want.Count; i++)
                {
                    var el = entries[want[i]].Root;
                    if (el.parent != content || content.IndexOf(el) != i + (empty != null ? 1 : 0)) content.Insert(Math.Min(i + (empty != null ? 1 : 0), content.childCount), el);
                }
                order.Clear();
                order.AddRange(want);
            }
            // coach targets
            Targets.Clear();
            foreach (var k in want)
            {
                var e = entries[k];
                if (e.IsSection) continue;
                if (!string.IsNullOrEmpty(e.Target)) Targets[e.Target] = e.Root;
                if (e.Side != null) foreach (var b in e.Side.Buttons) if (!string.IsNullOrEmpty(b.name)) Targets[b.name] = b;
            }
            return true;
        }

        Entry Build(Row r)
        {
            var e = new Entry();
            e.Card = UiKit.Card(r.Title, r.Sub, Icons.Get(r.Icon), r.Locked, r.Selected, "");
            e.Root = e.Card.Root;
            e.Badge = UiKit.Text("", "coddy-badge");
            e.Badge.style.display = DisplayStyle.None;
            e.Card.Head.Insert(Math.Max(0, e.Card.Head.IndexOf(e.Card.Side)), e.Badge);
            e.Bar = new VisualElement { pickingMode = PickingMode.Ignore };
            e.Bar.AddToClassList("vc-bar");
            e.BarFill = new VisualElement { pickingMode = PickingMode.Ignore };
            e.BarFill.AddToClassList("vc-bar__fill");
            e.Bar.Add(e.BarFill);
            e.Root.Add(e.Bar);
            TapGesture.Attach(e.Root, () => { if (!string.IsNullOrEmpty(e.Open)) open?.Invoke(e.Open, e.Target); });
            Update(e, r);
            return e;
        }

        void Update(Entry e, Row r)
        {
            UiKit.SetText(e.Card.Title, r.Title);
            e.Card.SetSub(r.Sub);
            bool badge = !string.IsNullOrEmpty(r.Badge);
            e.Badge.style.display = badge ? DisplayStyle.Flex : DisplayStyle.None;
            if (badge) UiKit.SetText(e.Badge, r.Badge);
            e.Bar.style.display = r.Progress >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (r.Progress >= 0) e.BarFill.style.width = Length.Percent(Math.Max(0f, Math.Min(1f, r.Progress)) * 100f);
            e.Root.EnableInClassList("coddy-list-card--locked", r.Locked);
            e.Root.EnableInClassList("coddy-list-card--selected", r.Selected);
            e.Root.EnableInClassList("vc-card--tappable", !string.IsNullOrEmpty(r.Open));
            e.Root.EnableInClassList("vc-card--wrap", r.Wrap);   // P4: an opened info strip shows its whole text
            e.Open = r.Open;
            e.Target = r.Target;
            string shape = r.SideShape();
            if (shape != e.Shape)
            {
                e.Shape = shape;
                e.Side = r.Actions.Count > 0 ? new SideControl(r.Actions, run) : null;
                e.Card.SetSide(e.Side?.Root);
            }
            else e.Side?.Update(r.Actions);
            e.Sig = r.Sig();
        }
    }
}
