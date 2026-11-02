# Implementation plan
1. Baseline import and deterministic gameplay simulation; inspect mobile UI.
2. Isolate QA saves; fix recovery and save-result reporting.
3. Introduce a shared locale catalog, scalable language selector, bundled font
   fallback, incremental API translation and placeholder/coverage validation.
4. Enforce touch-size floors, improve the cinematic intro with explicit controls,
   add tutorial progress and persist supported appearance settings.
5. Run Blender asset generation, then import and inspect the in-game presentation.
6. Add regression checks, capture multiple locales and panels at phone sizes,
   fix failures and record evidence plus outstanding release prerequisites.
