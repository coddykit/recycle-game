#!/bin/sh
# #183 (owner 10-24 "tab content 3–4 s late"): tap → new tab's first row ≤ 150 ms. The game's tab probe prints
# "TAB_OPEN_PROBE: worst=N ms ..."; this fails the check when N > TAB_BUDGET_MS (150) or no probe line was printed.
# The ≤ 2 frame verdict inside the probe stays; this caps the cost of those frames.
#   sh addons/coddy_ui/tests/tab_budget.sh <probe log>
# check.sh re-runs the probe ONCE on a fail (a loaded host stretches one tab: smuggler fleet 198 ms with 3 parallel checks, 60 alone).
LOG=${1:?probe log}
LIMIT=${TAB_BUDGET_MS:-150}
awk -v lim="$LIMIT" '
  /TAB_OPEN_PROBE:/ { for (i = 1; i <= NF; i++) if ($i ~ /^worst=/) { v = substr($i, 7) + 0; n++; if (v > w) w = v } }
  END {
    if (n == 0) { print "FAIL TAB_BUDGET no TAB_OPEN_PROBE line"; exit 1 }
    if (w > lim) { printf "FAIL TAB_BUDGET worst=%d ms > %d ms\n", w, lim; exit 1 }
    printf "TAB_BUDGET OK worst=%d ms <= %d ms\n", w, lim
  }' "$LOG"
