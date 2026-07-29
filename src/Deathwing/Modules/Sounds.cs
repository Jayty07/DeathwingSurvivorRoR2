namespace Deathwing.Modules
{
    /// <summary>
    /// Vanilla Wwise event names borrowed for Deathwing's skills. A name that no longer exists is a
    /// silent no-op in game, so these are safe to swap for custom banks later.
    /// </summary>
    internal static class Sounds
    {
        internal const string clawSwing = "Play_imp_attack1";
        internal const string boulderWindup = "Play_golem_attack1_start";
        internal const string boulderThrow = "Play_MULT_m2_grenade_throw";
        internal const string chargeStart = "Play_beetleGuard_attack2_start";
        internal const string cataclysmChannel = "Play_titanBoss_attack1_start";
        internal const string cataclysmErupt = "Play_titanBoss_attack1_impact";
        internal const string breathStart = "Play_magmaWorm_spit";
        internal const string breathLoop = "Play_elite_fire_trail_loop";
        internal const string breathStop = "Stop_elite_fire_trail_loop";
        internal const string wingFlap = "Play_gravekeeper_attack2_shoot";
        internal const string diveStart = "Play_vulture_attack2_windUp";
        internal const string diveImpact = "Play_titanBoss_attack2_impact";
    }
}
