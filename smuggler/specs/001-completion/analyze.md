# Consistency analysis
Pass 1: baseline simulation has 76 passing checks. Existing onboarding and asset
pipeline exist, so extend them instead of replacing them. Current language helpers
are EN/TR-only and the legacy writer would drop added locales. Fix tooling first.
Pass 2: increasing button sizes requires scrollable layouts and screenshot checks;
translation coverage alone cannot prove layout quality. The test runner currently
deletes the normal save: isolation precedes additional QA. Valid JSON with malformed
sections needs validation before backup rotation. CJK fonts must ship in the bundle.
All requirements map to tasks and verification. No production approval is assumed.
