// CoddyKit (Unity) — no analytics event leaves the game before consent mode v2 has been applied (Patron 2026-10-30).
// The Firebase bridge wraps its sink in this gate: events are held (bounded) until CoddyServices.ApplyConsentMode has
// handed the UMP answer to Firebase (SetConsent), then flushed in order. Firebase's own automatic events are covered by
// the manifest defaults (all denied, CoddyConsentDefaults.androidlib).
using System.Collections.Generic;

namespace CoddyKit
{
    public sealed class ConsentGatedAnalytics : IAnalytics
    {
        public const int MaxHeld = 64;
        readonly IAnalytics inner;
        readonly Queue<(string, Dictionary<string, object>)> held = new Queue<(string, Dictionary<string, object>)>();
        public int Held => held.Count;
        public int Dropped { get; private set; }

        public ConsentGatedAnalytics(IAnalytics inner)
        {
            this.inner = inner;
            CoddyServices.ConsentApplied += Flush;
        }

        public void Event(string name, Dictionary<string, object> parameters)
        {
            if (!CoddyServices.ConsentModeApplied)
            {
                if (held.Count < MaxHeld) held.Enqueue((name, parameters));
                else Dropped++;
                return;
            }
            Flush();
            inner.Event(name, parameters);
        }

        void Flush()
        {
            if (!CoddyServices.ConsentModeApplied) return;
            while (held.Count > 0) { var e = held.Dequeue(); inner.Event(e.Item1, e.Item2); }
        }
    }
}
