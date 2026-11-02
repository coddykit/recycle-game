// Volt City — plain view models the UI Toolkit layer renders 1:1 (engine-free, unit-tested). A tab/card is a TabView:
// header title + ONE summary line + rows. A Row is one kit list card: title (whole words), one grey line, optional badge,
// optional progress bar, ONE side control (a pill, a +/− stepper pair, or a segmented control) — UI.md section 2.
// Row.Id is stable (reconcile key: rows are added / removed / moved, never rebuilt on a money tick); Row.Sig() changes
// only when something visible changed, so the view touches a label only then (LESSONS A10).
using System.Collections.Generic;
using System.Text;

namespace VoltCity.Game
{
    public sealed class RowAction
    {
        /// <summary>Action id executed by GameActions (e.g. "ramp:steam:+1", "send:3", "buy:gas_1").</summary>
        public string Id = "";
        public string Label = "";
        public bool Enabled = true;
        /// <summary>primary | tonal | danger | segment (one of a segmented control) | ghost.</summary>
        public string Kind = "primary";
        /// <summary>Segment currently selected (Kind = segment).</summary>
        public bool Selected;
        /// <summary>Coach / test target id of this control ("grid.steam.plus" …).</summary>
        public string Target = "";
    }

    public sealed class Row
    {
        public string Id = "";
        public string Section = "";
        public string Title = "";
        public string Sub = "";
        public string Badge = "";
        /// <summary>Icon name under Assets/Resources/ui/icons (null = no tile).</summary>
        public string Icon;
        /// <summary>0..1 output bar, -1 = none.</summary>
        public float Progress = -1f;
        public bool Locked;
        public bool Selected;
        /// <summary>Tapping the card body (not the pill) opens this card ("card:district:riverside"); "" = nothing.</summary>
        public string Open = "";
        /// <summary>Coach / test target of the card body.</summary>
        public string Target = "";
        /// <summary>The grey line may wrap onto several lines (an opened info strip, P4 shop policy).</summary>
        public bool Wrap;
        public readonly List<RowAction> Actions = new List<RowAction>();

        public Row Act(string id, string label, bool enabled = true, string kind = "primary", string target = "", bool selected = false)
        {
            Actions.Add(new RowAction { Id = id, Label = label, Enabled = enabled, Kind = kind, Target = target, Selected = selected });
            return this;
        }

        /// <summary>Everything the user can see on the card, as one string (cheap compare before touching the view).</summary>
        public string Sig()
        {
            var sb = new StringBuilder();
            sb.Append(Section).Append('|').Append(Title).Append('|').Append(Sub).Append('|').Append(Badge).Append('|').Append(Icon)
                .Append('|').Append(Progress < 0 ? -1 : (int)(Progress * 100)).Append('|').Append(Locked).Append(Selected).Append(Wrap).Append('|').Append(Open);
            foreach (var a in Actions) sb.Append('|').Append(a.Id).Append(a.Label).Append(a.Enabled).Append(a.Kind).Append(a.Selected);
            return sb.ToString();
        }

        /// <summary>The side control's shape only (pill set + kinds): when it changes the view rebuilds that side, else it
        /// only updates texts / enabled states.</summary>
        public string SideShape()
        {
            var sb = new StringBuilder();
            foreach (var a in Actions) sb.Append(a.Id).Append(':').Append(a.Kind).Append(';');
            return sb.ToString();
        }
    }

    public sealed class TabView
    {
        public string Id = "";
        public string Title = "";
        public string Sub = "";
        public string Icon;
        public readonly List<Row> Rows = new List<Row>();
        /// <summary>Shown when Rows is empty.</summary>
        public string EmptyTitle = "", EmptyBody = "";
        /// <summary>Optional big action under the header (sheet action row): e.g. "Start shift 4", "Collect $930".</summary>
        public RowAction Primary;

        public Row Add(Row r) { Rows.Add(r); return r; }
    }

    public enum NeedleBand { Green = 0, Amber = 1, Red = 2 }

    public sealed class HudView
    {
        public string Cash = "", Rating = "", Clock = "", Phase = "", Speed = "", Goal = "", NeedleLabel = "";
        public int NeedlePm;
        /// <summary>Needle angle in degrees, -60 (deficit) .. +60 (surplus), quantised to 0.5 deg (rotate only on change).</summary>
        public float NeedleDeg;
        public NeedleBand Band;
        public bool Live, Paused;
        /// <summary>0..1 of the shift elapsed (thin progress under the clock).</summary>
        public float ShiftProgress;
        public string NeedleIcon = "check";
    }
}
