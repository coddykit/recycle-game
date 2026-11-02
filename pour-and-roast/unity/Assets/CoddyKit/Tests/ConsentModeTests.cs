using CoddyKit;
using NUnit.Framework;

namespace CoddyKit.Tests
{
    public class ConsentModeTests
    {
        [Test] public void BeforeUmpEverythingDenied()
        {
            var s = ConsentMode.FromTcf(false, 0, "1111111111");
            Assert.IsFalse(s.AnalyticsStorage || s.AdStorage || s.AdUserData || s.AdPersonalization);
        }

        [Test] public void DeclinedInUmpEverythingDenied()
        {
            var s = ConsentMode.FromTcf(true, 1, "0000000000");
            Assert.IsFalse(s.AnalyticsStorage || s.AdStorage || s.AdUserData || s.AdPersonalization);
            Assert.IsFalse(ConsentMode.FromTcf(true, 1, "").AdStorage);
            Assert.IsFalse(ConsentMode.FromTcf(true, 1, null).AdUserData);
        }

        [Test] public void AcceptedInUmpEverythingGranted()
        {
            var s = ConsentMode.FromTcf(true, 1, "11111111111");
            Assert.IsTrue(s.AnalyticsStorage && s.AdStorage && s.AdUserData && s.AdPersonalization);
        }

        [Test] public void OnlyWhatThePurposesAllow()
        {
            // storage yes, measurement no, personalisation no
            var s = ConsentMode.FromTcf(true, 1, "1000000000");
            Assert.IsTrue(s.AdStorage);
            Assert.IsTrue(s.AnalyticsStorage);
            Assert.IsFalse(s.AdUserData);
            Assert.IsFalse(s.AdPersonalization);
            // personalisation needs 3 AND 4
            Assert.IsFalse(ConsentMode.FromTcf(true, 1, "1010001").AdPersonalization);
            Assert.IsTrue(ConsentMode.FromTcf(true, 1, "1011001").AdPersonalization);
        }

        [Test] public void OutsideGdprGranted()
        {
            Assert.IsTrue(ConsentMode.FromTcf(true, 0, "").AdPersonalization);
            Assert.IsTrue(ConsentMode.FromTcf(true, -1, null).AdStorage);
        }
    }
}
