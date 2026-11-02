using System.Collections.Generic;
using CoddyKit;
using NUnit.Framework;

namespace CoddyKit.Tests
{
    public class ConsentGatedAnalyticsTests
    {
        sealed class Recorder : IAnalytics
        {
            public readonly List<string> Log = new List<string>();
            public void Event(string name, Dictionary<string, object> p) => Log.Add("event:" + name);
        }

        [SetUp] public void SetUp() => CoddyServices.ResetToFakes();
        [TearDown] public void TearDown() => CoddyServices.ResetToFakes();

        [Test]
        public void NoEventBeforeConsentMode_ConsentAppliedFirst_ThenFlushedInOrder()
        {
            var rec = new Recorder();
            CoddyServices.ConsentModeSink = s => rec.Log.Add("consent:" + s);   // what the Firebase bridge does (SetConsent)
            var gate = new ConsentGatedAnalytics(rec);
            gate.Event("legacy_import", null);
            gate.Event("se_case_open", new Dictionary<string, object> { ["case"] = "c1" });
            Assert.IsEmpty(rec.Log, "nothing may leave before UMP has answered");
            Assert.AreEqual(2, gate.Held);

            CoddyServices.ApplyConsentMode(ConsentMode.FromTcf(true, 1, "0000000000"));   // declined in UMP
            Assert.AreEqual(3, rec.Log.Count);
            StringAssert.StartsWith("consent:", rec.Log[0], "consent mode reaches Firebase BEFORE the first event");
            StringAssert.Contains("ad_storage=denied", rec.Log[0]);
            StringAssert.Contains("analytics_storage=denied", rec.Log[0]);
            Assert.AreEqual("event:legacy_import", rec.Log[1]);
            Assert.AreEqual("event:se_case_open", rec.Log[2]);

            gate.Event("purchase_success", null);
            Assert.AreEqual("event:purchase_success", rec.Log[3], "after consent: straight through");
        }

        [Test]
        public void HeldQueueIsBounded()
        {
            var gate = new ConsentGatedAnalytics(new Recorder());
            for (int i = 0; i < ConsentGatedAnalytics.MaxHeld + 5; i++) gate.Event("e", null);
            Assert.AreEqual(ConsentGatedAnalytics.MaxHeld, gate.Held);
            Assert.AreEqual(5, gate.Dropped);
        }
    }
}
