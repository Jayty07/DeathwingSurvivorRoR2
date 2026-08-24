using BepInEx.Configuration;

namespace Deathwing.Modules
{
    /// <summary>Every gameplay number the survivor exposes to the config file.</summary>
    internal static class Tuning
    {
        internal static ConfigEntry<float> baseHealth;
        internal static ConfigEntry<float> levelHealth;
        internal static ConfigEntry<float> baseArmor;
        internal static ConfigEntry<float> baseRegen;
        internal static ConfigEntry<float> baseMoveSpeed;
        internal static ConfigEntry<float> baseDamage;
        internal static ConfigEntry<float> baseAttackSpeed;
        internal static ConfigEntry<float> modelScale;
        internal static ConfigEntry<bool> tintModel;
        internal static ConfigEntry<bool> useRealModel;
        internal static ConfigEntry<float> realModelScale;
        internal static ConfigEntry<float> modelEmission;
        internal static ConfigEntry<bool> registerModel;
        internal static ConfigEntry<float> realModelHeight;
        internal static ConfigEntry<float> realModelLift;
        internal static ConfigEntry<float> walkCycleSpeed;
        internal static ConfigEntry<bool> realModelVoice;
        internal static ConfigEntry<float> realModelVoiceVolume;

        internal static ConfigEntry<float> moltenBloodMaxArmor;
        internal static ConfigEntry<float> moltenBloodMaxDamageMult;

        internal static ConfigEntry<float> clawDamageCoefficient;
        internal static ConfigEntry<float> boulderDamageCoefficient;
        internal static ConfigEntry<float> boulderCooldown;
        internal static ConfigEntry<float> breathDamageCoefficient;
        internal static ConfigEntry<float> breathCooldown;
        internal static ConfigEntry<float> breathWidth;
        internal static ConfigEntry<float> chargeDamageCoefficient;
        internal static ConfigEntry<float> chargeCooldown;
        internal static ConfigEntry<float> flightCooldown;
        internal static ConfigEntry<float> flightDuration;
        internal static ConfigEntry<float> cataclysmDamageCoefficient;
        internal static ConfigEntry<float> cataclysmCooldown;

        internal static void Init(ConfigFile config)
        {
            baseHealth = config.Bind("Stats", "Base Health", 380f, "Health at level 1. Vanilla survivors sit between 110 and 160.");
            levelHealth = config.Bind("Stats", "Health Per Level", 114f, "Health gained per level.");
            baseArmor = config.Bind("Stats", "Base Armor", 40f, "Flat armor granted by Deathwing's elementium plating.");
            baseRegen = config.Bind("Stats", "Base Health Regen", 1.8f, "Health per second at level 1. Vanilla survivors regenerate about 1.");
            baseMoveSpeed = config.Bind("Stats", "Base Move Speed", 6f, "Vanilla survivors move at 7.");
            baseDamage = config.Bind("Stats", "Base Damage", 16f, "Vanilla survivors deal 12 base damage.");
            baseAttackSpeed = config.Bind("Stats", "Base Attack Speed", 0.8f, "Multiplier on all skill durations. Below 1 means slower, heavier swings.");
            modelScale = config.Bind("Stats", "Model Scale", 1.9f, "Uniform scale applied to the model, hitboxes and character capsule.");
            tintModel = config.Bind("Stats", "Tint Model", true, "Recolour the placeholder chassis model molten black-and-orange. Ignored when the real model is in use.");
            useRealModel = config.Bind("Model", "Use Real Model", true,
                "Draw the real Deathwing model when its art payload is available. Disable to fall back to the placeholder chassis mesh.");
            realModelScale = config.Bind("Model", "Real Model Scale", 0.0095f,
                "Scale the real model is built at. In game it is resized to Real Model Height, so this only affects the character select model.");
            realModelHeight = config.Bind("Model", "Real Model Height", 11f,
                "Metres from his claws to the top of his standing pose. He is measured after spawning and "
                + "scaled to match, so this is the one dial for his size. 0 leaves him at Real Model Scale.");
            modelEmission = config.Bind("Model", "Real Model Emission", 2.2f,
                "How hot the model's glowing cracks and eyes burn.");
            registerModel = config.Bind("Model", "Register Model With Character Model", false,
                "Hand the real model to the game's character model system, which draws elite, burn and "
                + "cloak overlays on it and dissolves it on death - at the cost of also subjecting it to "
                + "the spawn print effect, which can leave it clipped away entirely.");
            realModelLift = config.Bind("Model", "Real Model Lift", 0f,
                "Extra metres to raise the real model after its feet have been placed on the ground. "
                + "Negative values sink him.");
            walkCycleSpeed = config.Bind("Model", "Walk Cycle Speed", 1f,
                "Multiplier on how fast his walk animation plays. The stride he covers is worked out from "
                + "his size, so 1 keeps his feet in step with the ground; raise it for a faster gait.");
            realModelVoice = config.Bind("Audio", "Custom Voice", true,
                "Play Deathwing's own roars, flame breath and taunt over the borrowed survivor sounds.");
            realModelVoiceVolume = config.Bind("Audio", "Custom Voice Volume", 1f,
                "Volume of those voice lines, from 0 to 1.");

            moltenBloodMaxArmor = config.Bind("Passive", "Molten Blood Max Armor", 60f, "Bonus armor at 0% health, scaled linearly by missing health.");
            moltenBloodMaxDamageMult = config.Bind("Passive", "Molten Blood Max Damage Bonus", 0.4f, "Bonus damage multiplier at 0% health, scaled linearly by missing health.");

            clawDamageCoefficient = config.Bind("Skills", "Molten Claw Damage", 3.2f, "Damage coefficient per claw swipe.");
            boulderDamageCoefficient = config.Bind("Skills", "Molten Boulder Damage", 6f, "Damage coefficient of the boulder impact.");
            boulderCooldown = config.Bind("Skills", "Molten Boulder Cooldown", 5f, "Cooldown in seconds.");
            breathDamageCoefficient = config.Bind("Skills", "Molten Breath Damage", 4.5f, "Damage coefficient per second of fire breath.");
            breathCooldown = config.Bind("Skills", "Molten Breath Cooldown", 7f, "Cooldown in seconds.");
            breathWidth = config.Bind("Skills", "Molten Breath Width", 3.2f,
                "How much wider the flame is drawn than the flamethrower drone sprays it. Visual only.");
            chargeDamageCoefficient = config.Bind("Skills", "Elementium Charge Damage", 5f, "Damage coefficient per enemy trampled.");
            chargeCooldown = config.Bind("Skills", "Elementium Charge Cooldown", 8f, "Cooldown in seconds.");
            flightCooldown = config.Bind("Skills", "Wings Of The Destroyer Cooldown", 12f, "Cooldown in seconds. Unused flight time is partially refunded.");
            flightDuration = config.Bind("Skills", "Wings Of The Destroyer Duration", 6f, "Maximum seconds airborne before the wings give out.");
            cataclysmDamageCoefficient = config.Bind("Skills", "Cataclysm Damage", 4f, "Damage coefficient per fissure ring.");
            cataclysmCooldown = config.Bind("Skills", "Cataclysm Cooldown", 14f, "Cooldown in seconds.");
        }
    }
}
