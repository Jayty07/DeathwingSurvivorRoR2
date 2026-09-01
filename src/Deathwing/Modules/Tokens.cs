using BepInEx.Configuration;
using Deathwing.SkillStates;
using R2API;
using UnityEngine;

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
        internal const string incinerateName = prefix + "INCINERATE_NAME";
        internal const string incinerateDescription = prefix + "INCINERATE_DESCRIPTION";
        internal const string lavaBurstName = prefix + "LAVABURST_NAME";
        internal const string lavaBurstDescription = prefix + "LAVABURST_DESCRIPTION";
        internal const string onslaughtName = prefix + "ONSLAUGHT_NAME";
        internal const string onslaughtDescription = prefix + "ONSLAUGHT_DESCRIPTION";
        internal const string earthShatterName = prefix + "EARTHSHATTER_NAME";
        internal const string earthShatterDescription = prefix + "EARTHSHATTER_DESCRIPTION";
        internal const string boulderName = prefix + "BOULDER_NAME";
        internal const string boulderDescription = prefix + "BOULDER_DESCRIPTION";
        internal const string chargeName = prefix + "CHARGE_NAME";
        internal const string chargeDescription = prefix + "CHARGE_DESCRIPTION";
        internal const string dragonflightName = prefix + "DRAGONFLIGHT_NAME";
        internal const string dragonflightDescription = prefix + "DRAGONFLIGHT_DESCRIPTION";
        internal const string formSwitchName = prefix + "FORMSWITCH_NAME";
        internal const string formSwitchDescription = prefix + "FORMSWITCH_DESCRIPTION";
        internal const string cataclysmName = prefix + "CATACLYSM_NAME";
        internal const string cataclysmDescription = prefix + "CATACLYSM_DESCRIPTION";
        internal const string bellowingRoarName = prefix + "BELLOWINGROAR_NAME";
        internal const string bellowingRoarDescription = prefix + "BELLOWINGROAR_DESCRIPTION";
        internal const string buffElementiumName = prefix + "BUFF_ELEMENTIUM_NAME";

        internal static void Init()
        {
            LanguageAPI.Add(bodyName, "Deathwing");
            LanguageAPI.Add(bodySubtitle, "The Destroyer");
            LanguageAPI.Add(bodyDescription,
                "Deathwing is a walking siege engine: he trades mobility for armor and raw force." +
                "<style=cIsUtility>He cannot be stunned, and every skill sets the ground on fire.</style>" +
                "<style=cSub>\n\n< ! > His armour is four elementium plates, and he sheds one every quarter of his health. Only Dragonflight rebuilds them." +
                $"\n\n< ! > <style=cIsUtility>{FormatKey(Tuning.formSwitchKey)}</style> switches form. Destroyer gives you Incinerate and Onslaught up close; World Breaker gives you Lava Burst and Earth Shatter at range." +
                $"\n\n< ! > <style=cIsUtility>{FormatKey(Tuning.dragonflightKey)}</style> is Dragonflight: three seconds to take off, then you are untouchable, healing, and free to go anywhere. It will not cast while you are being hurt." +
                $"\n\n< ! > <style=cIsUtility>{FormatKey(Tuning.heroicCataclysmKey)}</style> is Cataclysm and <style=cIsUtility>{FormatKey(Tuning.bellowingRoarKey)}</style> is Bellowing Roar, his two heroics. They root him, so open with them." +
                "\n\n< ! > Molten Flame roots you to a crawl while it burns. Use it on what cannot get away.</style>");
            LanguageAPI.Add(bodyOutro, "..and so he left, the world still burning behind him.");
            LanguageAPI.Add(bodyFailure, "..and so he vanished, his flame finally spent.");

            LanguageAPI.Add(passiveName, "Aspect of Death");
            LanguageAPI.Add(passiveDescription,
                $"Deathwing wears <style=cIsHealing>{AspectOfDeath.maxPlates} elementium plates</style>, each granting " +
                $"<style=cIsHealing>{Tuning.platingArmorPerPlate.Value:0} armor</style>. He <style=cIsHealth>sheds one for every " +
                $"{100f / AspectOfDeath.maxPlates:0}% of his health he loses</style>, and <style=cIsUtility>only Dragonflight rebuilds them</style>. " +
                "He also <style=cIsUtility>cannot be stunned or frozen</style>.");

            LanguageAPI.Add(primaryName, "Claw and Bite");
            LanguageAPI.Add(primaryDescription,
                $"<style=cIsDamage>Ignite.</style> A three-hit chain: two claw swipes for <style=cIsDamage>{Tuning.clawDamageCoefficient.Value * 100f:0}% damage</style> each, " +
                $"then a bite for <style=cIsDamage>{Tuning.clawDamageCoefficient.Value * MoltenClaw.finisherDamageMultiplier * 100f:0}% damage</style> that erupts around the impact and launches what it hits.");

            LanguageAPI.Add(secondaryName, "Molten Flame");
            LanguageAPI.Add(secondaryDescription,
                $"<style=cIsDamage>Ignite.</style> Breathe a jet of dragonfire for <style=cIsDamage>{Tuning.moltenFlameDamageCoefficient.Value * 100f:0}% damage per second</style>, " +
                $"for as long as you hold it up to <style=cIsUtility>{MoltenFlame.baseMaxDuration:0.#}s</style>. Takes " +
                $"<style=cIsUtility>{MoltenFlame.baseWindupDuration:0.#}s</style> to catch, and roots Deathwing to a crawl while it burns.");

            LanguageAPI.Add(incinerateName, "Incinerate");
            LanguageAPI.Add(incinerateDescription,
                $"<style=cIsUtility>Destroyer form.</style> <style=cIsDamage>Ignite.</style> Set the air alight around you after a brief windup, dealing " +
                $"<style=cIsDamage>{Tuning.incinerateDamageCoefficient.Value * 100f:0}% damage</style> to everything within " +
                $"<style=cIsDamage>{Incinerate.radius:0}m</style> and launching it.");

            LanguageAPI.Add(lavaBurstName, "Lava Burst");
            LanguageAPI.Add(lavaBurstDescription,
                $"<style=cIsUtility>World Breaker form.</style> <style=cIsDamage>Ignite.</style> A second after you aim it, the ground bursts for " +
                $"<style=cIsDamage>{Tuning.lavaBurstDamageCoefficient.Value * 100f:0}% damage</style>, <style=cIsUtility>slowing</style> what it hits and leaving a pool of lava behind.");

            LanguageAPI.Add(onslaughtName, "Onslaught");
            LanguageAPI.Add(onslaughtDescription,
                $"<style=cIsUtility>Destroyer form.</style> <style=cIsDamage>Ignite.</style> Lunge forward, trampling for " +
                $"<style=cIsDamage>{Tuning.onslaughtDamageCoefficient.Value * 100f:0}% damage</style> and finishing with a bite for " +
                $"<style=cIsDamage>{Tuning.onslaughtBiteDamageCoefficient.Value * 100f:0}% damage</style> that <style=cIsUtility>slows</style>.");

            LanguageAPI.Add(earthShatterName, "Earth Shatter");
            LanguageAPI.Add(earthShatterDescription,
                $"<style=cIsUtility>World Breaker form.</style> <style=cIsDamage>Ignite.</style> Split the ground in two fissures for " +
                $"<style=cIsDamage>{Tuning.earthShatterDamageCoefficient.Value * 100f:0}% damage</style>, <style=cIsDamage>stunning</style> everything they run under.");

            LanguageAPI.Add(boulderName, "Molten Boulder");
            LanguageAPI.Add(boulderDescription,
                $"Hurl a chunk of the earth's crust for <style=cIsDamage>{Tuning.boulderDamageCoefficient.Value * 100f:0}% damage</style>. The impact <style=cIsDamage>ignites</style> and leaves a pool of lava.");

            LanguageAPI.Add(chargeName, "Elementium Charge");
            LanguageAPI.Add(chargeDescription,
                $"<style=cIsUtility>Armored.</style> Charge forward, trampling enemies for <style=cIsDamage>{Tuning.chargeDamageCoefficient.Value * 100f:0}% damage</style> and launching them away.");

            LanguageAPI.Add(dragonflightName, "Dragonflight");
            LanguageAPI.Add(dragonflightDescription,
                $"Take <style=cIsUtility>{Dragonflight.takeoffDuration:0.#}s</style> to take off, then fly freely, " +
                "<style=cIsHealing>immune to all damage</style>, <style=cIsHealing>healing</style> and " +
                "<style=cIsHealing>rebuilding your plates</style> as you go. Airborne, your primary " +
                $"<style=cIsDamage>rains fireballs for {Tuning.dragonFireDamageCoefficient.Value * 100f:0}% damage</style> " +
                "and your secondary dives on them. <style=cIsHealth>Cannot be cast shortly after taking damage.</style>");

            LanguageAPI.Add(formSwitchName, "Change Form");
            LanguageAPI.Add(formSwitchDescription,
                "Switch between <style=cIsUtility>Destroyer</style> - Incinerate and Onslaught, up close - and " +
                "<style=cIsUtility>World Breaker</style> - Lava Burst and Earth Shatter, at range.");

            LanguageAPI.Add(cataclysmName, "Cataclysm");
            LanguageAPI.Add(cataclysmDescription,
                $"<style=cIsDamage>Heroic. Ignite.</style> Tear the ground open around you in rings, each dealing " +
                $"<style=cIsDamage>{Tuning.heroicCataclysmDamageCoefficient.Value * 100f:0}% damage</style>. Roots Deathwing while it erupts.");

            LanguageAPI.Add(bellowingRoarName, "Bellowing Roar");
            LanguageAPI.Add(bellowingRoarDescription,
                "<style=cIsDamage>Heroic.</style> Bellow, <style=cIsUtility>routing</style> everything around you and " +
                "<style=cIsDamage>igniting</style> it. Roots Deathwing while he roars.");

            LanguageAPI.Add(buffElementiumName, "Elementium Plating");
        }

        /// <summary>
        /// The key a rebindable skill is bound to, read from the config so the description matches what
        /// the player actually has to press rather than the default.
        /// </summary>
        private static string FormatKey(ConfigEntry<KeyboardShortcut> key)
        {
            return key.Value.MainKey.ToString();
        }
    }
}
