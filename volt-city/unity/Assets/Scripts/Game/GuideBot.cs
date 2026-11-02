// Volt City — the "newbie" bot (DESIGN section 9 audit: a player who only taps the highlighted control): maps the guide's
// spotlight target to the action that control runs. Used by the EditMode guide tests and the PlayMode smoke (the same
// ids the UI gives its controls, so a renamed target fails both).
namespace VoltCity.Game
{
    public static class GuideBot
    {
        /// <summary>Action id the highlighted control runs ("" = nothing to tap now).</summary>
        public static string ActionFor(Coach c)
        {
            if (c == null || !c.Spot) return "";
            if (!c.Showing) return Coach.AssistAction(c.Target);   // a guide assist (balance)
            switch (c.Target)
            {
                case "grid.steam.plus": return "ramp:steam:+1";
                case "hud.needle": return "open:needle";
                case "world.fault": return "select:" + c.FaultId;
                case "card.fault.send": return "send:" + c.FaultId;
                case "report.collect": return "collect";
                case "build.line_riverside_1.buy": return "buy:line_riverside_1";
                case "build.gas_1.buy": return "buy:gas_1";
                case "grid.gas.start": return "power:gas:on";
                case "city.hilltop": return "card:district:hilltop";
            }
            return "";
        }
    }
}
