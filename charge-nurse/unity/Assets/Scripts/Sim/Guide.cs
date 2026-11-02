// Charge Nurse sim core — the guide's scripted moments (plan §9). The guide runs the real sim in tutorial mode (nobody
// worsens or leaves, no crises, no rewards); "guided" patients and beds wait for the player's tap instead of a team, so
// every step is the player's own action. Walk-ins start once the X-ray room is open (step 7).
namespace ChargeNurse.Sim
{
    public sealed partial class Simulation
    {
        /// <summary>Step 6: the first ambulance call (a red card), waiting for Accept.</summary>
        public void GuideAmbulance()
        {
            if (S.Ambulance != null) return;
            S.Ambulance = new AmbulanceCall { Sev = Sev.Red, Left = Gd.AmbulanceAnswerMin, Guided = true };
            Emit("ambulance_call", -1, Sev.Red);
        }

        /// <summary>Step 9: two observation patients are ready to go home but nobody signed the discharge — the beds stay
        /// blocked (the plan's hidden bottleneck: usually discharge, not the ER).</summary>
        public void GuideDischargeSetup()
        {
            if (S.Patients.Exists(p => p.Guided && p.Stage == Stage.WardReady)) return;
            for (int k = 0; k < 2; k++)
            {
                int bed = FreeBedFor("obs");
                if (bed < 0) break;
                var p = new Patient { Id = S.NextPatient++, Sev = Sev.Yellow, Tag = Sev.Yellow, Admit = true, Ward = "obs", Seen = true, Guided = true, Stage = Stage.WardReady, Bed = bed };
                S.Beds[bed].State = BedState.Busy;
                S.Beds[bed].Patient = p.Id;
                S.Patients.Add(p);
            }
        }

        /// <summary>Guided patients / beds go back to the teams (after the lens step, and when the guide ends).</summary>
        public void ReleaseGuided()
        {
            foreach (var p in S.Patients) p.Guided = false;
            foreach (var b in S.Beds) b.Guided = false;
        }

        /// <summary>Walk-ins on (the guide's step 7, or the end of the guide).</summary>
        public void StartArrivals() { S.ArrivalsOn = true; }

        /// <summary>The guide finished or was skipped: walk-ins, crises (after the free days) and outcomes may start.</summary>
        public void EndTutorial()
        {
            S.Tutorial = false;
            S.ArrivalsOn = true;
            ReleaseGuided();
            if (S.Ambulance != null && S.Ambulance.Guided) S.Ambulance.Guided = false;
        }
    }
}
