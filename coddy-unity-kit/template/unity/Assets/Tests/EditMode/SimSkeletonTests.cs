// @TITLE@ - P1 sim tests live here (EditMode, engine-free). U0: the assembly is wired.
using NUnit.Framework;
using @NS@.Sim;

namespace @NS@.Sim.Tests
{
    public class SimSkeletonTests
    {
        [Test]
        public void SimAssemblyIsWired() => Assert.GreaterOrEqual(SimInfo.SchemaVersion, 1);
    }
}
