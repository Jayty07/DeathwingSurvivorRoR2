using BepInEx.Configuration;

namespace Deathwing.Modules
{
    /// <summary>Every gameplay number the survivor exposes to the config file.</summary>
    internal static class Tuning
    {
        internal static ConfigEntry<float> baseHealth;
        internal static ConfigEntry<float> levelHealth;
        internal static ConfigEntry<float> baseArmor;
        internal static ConfigEntry<float> baseMoveSpeed;
        internal static ConfigEntry<float> baseDamage;
        internal static ConfigEntry<float> baseAttackSpeed;
        internal static ConfigEntry<float> modelScale;
        internal static ConfigEntry<bool> tintModel;

        internal static ConfigEntry<float> moltenBloodMaxArmor;
        internal static ConfigEntry<float> moltenBloodMaxDamageMult;

        internal static ConfigEntry<float> clawDamageCoefficient;
        internal static ConfigEntry<float> boulderDamageCoefficient;
        internal static ConfigEntry<float> boulderCooldown;
        internal static ConfigEntry<float> breathDamageCoefficient;
        internal static ConfigEntry<float> breathCooldown;
        internal static ConfigEntry<float> chargeDamageCoefficient;
        internal static ConfigEntry<float> chargeCooldown;
        internal static ConfigEntry<float> flightCooldown;
        internal static ConfigEntry<float> flightDuration;
        internal static ConfigEntry<float> cataclysmDamageCoefficient;
        internal static ConfigEntry<float> cataclysmCooldown;

        internal static void Init(ConfigFile config)
        {
            baseHealth = config.Bind("Stats", "Base Health", 260f, "Health at level 1. Vanilla survivors sit between 110 and 160.");
            levelHealth = config.Bind("Stats", "Health Per Level", 78f, "Health gained per level.");
            baseArmor = config.Bind("Stats", "Base Armor", 30f, "Flat armor granted by Deathwing's elementium plating.");
            baseMoveSpeed = config.Bind("Stats", "Base Move Speed", 6f, "Vanilla survivors move at 7.");
            baseDamage = config.Bind("Stats", "Base Damage", 16f, "Vanilla survivors deal 12 base damage.");
            baseAttackSpeed = config.Bind("Stats", "Base Attack Speed", 0.8f, "Multiplier on all skill durations. Below 1 means slower, heavier swings.");
            modelScale = config.Bind("Stats", "Model Scale", 1.9f, "Uniform scale applied to the model, hitboxes and character capsule.");
            tintModel = config.Bind("Stats", "Tint Model", true, "Recolour the model molten black-and-orange. Disable to see the untouched chassis materials.");

            moltenBloodMaxArmor = config.Bind("Passive", "Molten Blood Max Armor", 40f, "Bonus armor at 0% health, scaled linearly by missing health.");
            moltenBloodMaxDamageMult = config.Bind("Passive", "Molten Blood Max Damage Bonus", 0.4f, "Bonus damage multiplier at 0% health, scaled linearly by missing health.");

            clawDamageCoefficient = config.Bind("Skills", "Molten Claw Damage", 3.2f, "Damage coefficient per claw swipe.");
            boulderDamageCoefficient = config.Bind("Skills", "Molten Boulder Damage", 6f, "Damage coefficient of the boulder impact.");
            boulderCooldown = config.Bind("Skills", "Molten Boulder Cooldown", 5f, "Cooldown in seconds.");
            breathDamageCoefficient = config.Bind("Skills", "Molten Breath Damage", 4.5f, "Damage coefficient per second of fire breath.");
            breathCooldown = config.Bind("Skills", "Molten Breath Cooldown", 7f, "Cooldown in seconds.");
            chargeDamageCoefficient = config.Bind("Skills", "Elementium Charge Damage", 5f, "Damage coefficient per enemy trampled.");
            chargeCooldown = config.Bind("Skills", "Elementium Charge Cooldown", 8f, "Cooldown in seconds.");
            flightCooldown = config.Bind("Skills", "Wings Of The Destroyer Cooldown", 12f, "Cooldown in seconds. Unused flight time is partially refunded.");
            flightDuration = config.Bind("Skills", "Wings Of The Destroyer Duration", 6f, "Maximum seconds airborne before the wings give out.");
            cataclysmDamageCoefficient = config.Bind("Skills", "Cataclysm Damage", 4f, "Damage coefficient per fissure ring.");
            cataclysmCooldown = config.Bind("Skills", "Cataclysm Cooldown", 14f, "Cooldown in seconds.");
        }
    }
}
