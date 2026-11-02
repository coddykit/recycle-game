// Volt City EditMode tests: the project policy (ProjectValidator), the game's kit numbers against the fleet rules, and
// the house-ad host row. UNTESTED until the Unity editor is installed (they need UnityEditor).
using System.Linq;
using CoddyKit;
using CoddyKit.Ads;
using CoddyKit.EditorTools;
using NUnit.Framework;
using VoltCity.App;
using VoltCity.EditorTools;

namespace VoltCity.App.Tests
{
    public class VoltProjectTests
    {
        [Test]
        public void ProjectFollowsTheFleetPolicy()
        {
            var problems = ProjectValidator.Problems(VoltProjectSetup.Spec);
            Assert.IsEmpty(problems, "run Coddy/Volt City/Apply Project Setup:\n- " + string.Join("\n- ", problems));
        }

        [Test]
        public void RewardedNumbersRespectTheCaps()
        {
            Assert.AreEqual("", RewardedRules.CheckBudget(VoltGameConfig.RewardSingleMax, VoltGameConfig.RewardDailyMax, VoltGameConfig.CheapestPackUnits));
            var limits = VoltGameConfig.RewardedLimits();
            CollectionAssert.AreEquivalent(new[] { "offline_x2", "shift_bonus", "emergency_import", "crew_overtime" }, limits.Keys.ToArray());
            // docs/IAP.md §3: cash per day = 1 × 100 + 3 × 75 = 325 milli-days <= 500
            Assert.LessOrEqual(limits["offline_x2"].Daily * 100 + limits["shift_bonus"].Daily * 75, VoltGameConfig.RewardDailyMax);
        }

        [Test]
        public void ReleaseVersionCodeIsBuildTimeSecondsNeverOne()
        {
            const long now = 1_793_200_000;
            Assert.AreEqual(1_793_200_000, VoltAndroidBuild.ResolveVersionCode("", "internal", now), "missing env = build-time seconds");
            Assert.AreEqual(1_793_200_000, VoltAndroidBuild.ResolveVersionCode(null, "dev", now));
            Assert.AreEqual(1_793_200_123, VoltAndroidBuild.ResolveVersionCode("1793200123", "production", now));
            Assert.AreEqual(1_793_200_000, VoltAndroidBuild.ResolveVersionCode("abc", "internal", now), "junk env = build-time seconds");
            Assert.AreEqual(0, VoltAndroidBuild.ResolveVersionCode("1", "internal", now), "a release with vc 1 fails closed");
            Assert.AreEqual(0, VoltAndroidBuild.ResolveVersionCode("2200000000", "production", now), "above Play's ceiling");
            Assert.AreEqual(5, VoltAndroidBuild.ResolveVersionCode("5", "dev", now), "dev builds may pin a small code");
        }

        [Test]
        public void AdFreeIsOnlyTheNoadsEntitlement()
        {
            CollectionAssert.AreEqual(new[] { "noads" }, VoltGameConfig.AdFreeEntitlements);
        }

        [Test]
        public void HouseAdsKnowThisHost()
        {
            Assert.IsTrue(HouseRelated.Related.ContainsKey(VoltGameConfig.AppId), "regenerate Tools/gen_house_related.py");
            Assert.IsFalse(HouseRelated.Related[VoltGameConfig.AppId].Contains(VoltGameConfig.AppId), "never self-promotion");
        }

        [Test]
        public void TenLanguagesWithHindi()
        {
            Assert.AreEqual(10, VoltGameConfig.Languages.Length);
            CollectionAssert.Contains(VoltGameConfig.Languages, "hi");
        }
    }
}
