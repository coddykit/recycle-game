// CoddyKit (Unity) — Google consent mode v2 for Firebase Analytics (Patron 2026-10-29, fleet rule from the Godot kit).
// Before UMP has answered every signal is DENIED (manifest defaults in Bridges/Firebase/CoddyConsentDefaults.androidlib
// + an explicit SetConsent at registration). After UMP: only what the player allowed, read from the IAB TCF v2 strings
// UMP writes (IABTCF_gdprApplies, IABTCF_PurposeConsents):
//   outside the GDPR (gdprApplies != 1)   -> all granted
//   ad_storage          = purpose 1 (store / access information on a device)
//   ad_user_data        = purposes 1 + 7 (measure ad performance)
//   ad_personalization  = purposes 3 + 4 (personalised ads profile + selection)
//   analytics_storage   = purpose 1 (the fleet grants analytics, but never storage the player refused)
// A player who declines in UMP therefore sends all four as denied.
namespace CoddyKit
{
    public struct ConsentSignals
    {
        public bool AnalyticsStorage, AdStorage, AdUserData, AdPersonalization;

        public static readonly ConsentSignals Denied = default;
        public static readonly ConsentSignals Granted = new ConsentSignals { AnalyticsStorage = true, AdStorage = true, AdUserData = true, AdPersonalization = true };

        public override string ToString() =>
            $"analytics_storage={S(AnalyticsStorage)} ad_storage={S(AdStorage)} ad_user_data={S(AdUserData)} ad_personalization={S(AdPersonalization)}";
        static string S(bool b) => b ? "granted" : "denied";
    }

    public static class ConsentMode
    {
        /// <param name="umpAnswered">UMP finished (form shown or not required); false = still unknown -> all denied</param>
        /// <param name="gdprApplies">IABTCF_gdprApplies (1 = applies, 0 = not, -1 = missing)</param>
        /// <param name="purposeConsents">IABTCF_PurposeConsents ('1'/'0' per purpose, purpose 1 first)</param>
        public static ConsentSignals FromTcf(bool umpAnswered, int gdprApplies, string purposeConsents)
        {
            if (!umpAnswered) return ConsentSignals.Denied;
            if (gdprApplies != 1) return ConsentSignals.Granted;
            bool P(int n) => purposeConsents != null && purposeConsents.Length >= n && purposeConsents[n - 1] == '1';
            return new ConsentSignals
            {
                AnalyticsStorage = P(1),
                AdStorage = P(1),
                AdUserData = P(1) && P(7),
                AdPersonalization = P(3) && P(4),
            };
        }
    }
}
