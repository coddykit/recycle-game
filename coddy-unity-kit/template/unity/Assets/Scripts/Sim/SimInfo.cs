// @TITLE@ - engine-free simulation (P1). noEngineReferences: no UnityEngine here, ever (headless balance runner + dotnet).
// U0 skeleton placeholder so the assembly compiles; P1 replaces it with the real fixed-step, seeded sim.
namespace @NS@.Sim
{
    public static class SimInfo
    {
        /// <summary>Save / data schema version of the sim (P1 bumps it with every save-format change).</summary>
        public const int SchemaVersion = 1;
    }
}
