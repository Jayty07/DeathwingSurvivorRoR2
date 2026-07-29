using Deathwing.SkillStates;
using R2API;

namespace Deathwing.Modules
{
    internal static class Tokens
    {
        internal const string prefix = "DEATHWING_";

        internal const string bodyName = prefix + "BODY_NAME";
        internal const string bodySubtitle = prefix + "BODY_SUBTITLE";
        internal const string bodyDescription = prefix + "BODY_DESCRIPTION";
        internal const string bodyOutro = prefix + "BODY_OUTRO";
        internal const string bodyFailure = prefix + "BODY_FAILURE";

        internal const string passiveName = prefix + "PASSIVE_NAME";
        internal const string passiveDescription = prefix + "PASSIVE_DESCRIPTION";
        internal const string primaryName = prefix + "PRIMARY_NAME";
        internal const string primaryDescription = prefix + "PRIMARY_DESCRIPTION";
        internal const string secondaryName = prefix + "SECONDARY_NAME";
        internal const string secondaryDescription = prefix + "SECONDARY_DESCRIPTION";
        internal const string breathName = prefix + "BREATH_NAME";
        internal const string breathDescription = prefix + "BREATH_DESCRIPTION";
        internal const string utilityName = prefix + "UTILITY_NAME";
        internal const string utilityDescription = prefix + "UTILITY_DESCRIPTION";
        internal const string flightName = prefix + "FLIGHT_NAME";
        internal const string flightDescription = prefix + "FLIGHT_DESCRIPTION";
        internal const string specialName = prefix + "SPECIAL_NAME";
        internal const string specialDescription = prefix + "SPECIAL_DESCRIPTION";
        internal const string buffElementiumName = prefix + "BUFF_ELEMENTIUM_NAME";

        internal static void Init()
        {
            LanguageAPI.Add(bodyName, "Deathwing");
            LanguageAPI.Add(bodySubtitle, "The Destroyer");
            LanguageAPI.Add(bodyDescription,
                "Deathwing is a walking siege engine: he trades mobility for armor and raw force.<style=cIsUtility>He cannot be slowed easily, and every skill sets the ground on fire.</style>" +
                "<style=cSub>\n\n< ! > Molten Claw is slow but hits like a boulder. Land all three hits of the combo: the slam is where most of its damage is." +
                "\n\n< ! > Molten Boulder arcs over cover and leaves a lava pool. Use it to zone chokepoints, or take Molten Breath to melt whatever closes the distance." +
                "\n\n< ! > Wings of the Destroyer trades your ground dash for flight. Press it to take off, press it again to land, or dive to land as a bomb." +
                "\n\n< ! > Elementium Charge is your only ground mobility. It also makes you nearly unkillable while it lasts, so charge through danger, not away from it." +
                "\n\n< ! > Cataclysm roots you while it channels. Open with it against packed enemies.</style>");
            LanguageAPI.Add(bodyOutro, "..and so he left, the world still burning behind him.");
            LanguageAPI.Add(bodyFailure, "..and so he vanished, his flame finally spent.");

            LanguageAPI.Add(passiveName, "Molten Blood");
            LanguageAPI.Add(passiveDescription,
                $"The more wounded Deathwing becomes, the hotter he burns. Gain up to <style=cIsHealing>{Tuning.moltenBloodMaxArmor.Value:0} armor</style> " +
                $"and <style=cIsDamage>{Tuning.moltenBloodMaxDamageMult.Value * 100f:0}% damage</style>, scaling with missing health.");

            LanguageAPI.Add(primaryName, "Molten Claw");
            LanguageAPI.Add(primaryDescription,
                $"<style=cIsDamage>Ignite.</style> A three-hit combo: two claw swipes for <style=cIsDamage>{Tuning.clawDamageCoefficient.Value * 100f:0}% damage</style> each, " +
                $"then a slam for <style=cIsDamage>{Tuning.clawDamageCoefficient.Value * MoltenClaw.finisherDamageMultiplier * 100f:0}% damage</style> that erupts around the impact and launches what it hits.");

            LanguageAPI.Add(secondaryName, "Molten Boulder");
            LanguageAPI.Add(secondaryDescription,
                $"Hurl a chunk of the earth's crust for <style=cIsDamage>{Tuning.boulderDamageCoefficient.Value * 100f:0}% damage</style>. The impact <style=cIsDamage>ignites</style> and leaves a pool of lava.");

            LanguageAPI.Add(breathName, "Molten Breath");
            LanguageAPI.Add(breathDescription,
                $"<style=cIsDamage>Ignite.</style> Breathe dragonfire in a cone for <style=cIsDamage>{Tuning.breathDamageCoefficient.Value * 100f:0}% damage per second</style> for up to " +
                $"<style=cIsUtility>{MoltenBreath.baseMaxDuration:0.#}s</style>. Deathwing is slowed to a crawl while breathing.");

            LanguageAPI.Add(utilityName, "Elementium Charge");
            LanguageAPI.Add(utilityDescription,
                $"<style=cIsUtility>Armored.</style> Charge forward, trampling enemies for <style=cIsDamage>{Tuning.chargeDamageCoefficient.Value * 100f:0}% damage</style> and launching them away.");

            LanguageAPI.Add(flightName, "Wings of the Destroyer");
            LanguageAPI.Add(flightDescription,
                $"<style=cIsUtility>Flight.</style> Toggle flight for up to <style=cIsUtility>{Tuning.flightDuration.Value:0.#}s</style>, steering where you look. " +
                $"Press <style=cIsUtility>your primary</style> while airborne to dive, dealing <style=cIsDamage>{DiveSlam.damageCoefficient * 100f:0}% damage</style> on impact in a radius that grows with the height of the dive.");

            LanguageAPI.Add(specialName, "Cataclysm");
            LanguageAPI.Add(specialDescription,
                $"Split the earth open. Rings of erupting fissures deal <style=cIsDamage>{Tuning.cataclysmDamageCoefficient.Value * 100f:0}% damage</style> each and <style=cIsDamage>ignite</style> everything caught in them.");

            LanguageAPI.Add(buffElementiumName, "Elementium Plating");
        }
    }
}
