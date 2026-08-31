using BepInEx.Configuration;
using UnityEngine;

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
        internal static ConfigEntry<float> extraRowGap;
        internal static ConfigEntry<float> cameraDistance;
        internal static ConfigEntry<float> cameraPivotHeight;
        internal static ConfigEntry<bool> dropPod;
        internal static ConfigEntry<bool> realModelVoice;
        internal static ConfigEntry<float> realModelVoiceVolume;

        internal static ConfigEntry<float> platingArmorPerPlate;

        internal static ConfigEntry<float> clawDamageCoefficient;
        internal static ConfigEntry<float> boulderDamageCoefficient;
        internal static ConfigEntry<float> boulderCooldown;
        internal static ConfigEntry<float> breathWidth;
        internal static ConfigEntry<float> chargeDamageCoefficient;
        internal static ConfigEntry<float> chargeCooldown;

        internal static ConfigEntry<float> moltenFlameDamageCoefficient;
        internal static ConfigEntry<float> moltenFlameCooldown;
        internal static ConfigEntry<float> incinerateDamageCoefficient;
        internal static ConfigEntry<float> incinerateCooldown;
        internal static ConfigEntry<float> lavaBurstDamageCoefficient;
        internal static ConfigEntry<float> lavaBurstCooldown;
        internal static ConfigEntry<float> onslaughtDamageCoefficient;
        internal static ConfigEntry<float> onslaughtBiteDamageCoefficient;
        internal static ConfigEntry<float> onslaughtCooldown;
        internal static ConfigEntry<float> earthShatterDamageCoefficient;
        internal static ConfigEntry<float> earthShatterCooldown;
        internal static ConfigEntry<float> dragonflightCooldown;
        internal static ConfigEntry<float> dragonflightDuration;
        internal static ConfigEntry<float> dragonflightHealFraction;
        internal static ConfigEntry<float> dragonflightCombatLockout;
        internal static ConfigEntry<float> dragonFireDamageCoefficient;
        internal static ConfigEntry<float> dragonFireInterval;
        internal static ConfigEntry<float> heroicCataclysmDamageCoefficient;
        internal static ConfigEntry<float> heroicCataclysmCooldown;
        internal static ConfigEntry<float> bellowingRoarDamageCoefficient;
        internal static ConfigEntry<float> bellowingRoarCooldown;
        internal static ConfigEntry<float> formSwitchCooldown;

        internal static ConfigEntry<KeyboardShortcut> dragonflightKey;
        internal static ConfigEntry<KeyboardShortcut> formSwitchKey;
        internal static ConfigEntry<KeyboardShortcut> heroicCataclysmKey;
        internal static ConfigEntry<KeyboardShortcut> bellowingRoarKey;

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
            dropPod = config.Bind("Model", "Drop Pod", false,
                "Arrive in the survivor drop pod. Off by default: the pod holds him inside its own "
                + "geometry while it lands, which is where his footing is worked out from, so he can end "
                + "up stood at the pod's height for the rest of the stage.");
            extraRowGap = config.Bind("Model", "Extra Skill Row Gap", 0.5f,
                "How far above the skill bar his Z/X/C/V icons sit, in icon heights. Raise it if their key "
                + "labels crowd the row below.");
            cameraDistance = config.Bind("Model", "Camera Distance", 0f,
                "Metres the camera sits behind him. 0 works it out from Real Model Height, which is what "
                + "keeps him framed when you resize him.");
            cameraPivotHeight = config.Bind("Model", "Camera Pivot Height", 0f,
                "Metres up his body the camera looks at and orbits. 0 puts it at the middle of his body.");
            walkCycleSpeed = config.Bind("Model", "Walk Cycle Speed", 1f,
                "Multiplier on how fast his walk animation plays. The stride he covers is worked out from "
                + "his size, so 1 keeps his feet in step with the ground; raise it for a faster gait.");
            realModelVoice = config.Bind("Audio", "Custom Voice", true,
                "Play Deathwing's own roars, flame breath and taunt over the borrowed survivor sounds.");
            realModelVoiceVolume = config.Bind("Audio", "Custom Voice Volume", 1f,
                "Volume of those voice lines, from 0 to 1.");

            clawDamageCoefficient = config.Bind("Skills", "Molten Claw Damage", 3.2f, "Damage coefficient per claw swipe.");
            boulderDamageCoefficient = config.Bind("Skills", "Molten Boulder Damage", 6f, "Damage coefficient of the boulder impact.");
            boulderCooldown = config.Bind("Skills", "Molten Boulder Cooldown", 5f, "Cooldown in seconds.");
            breathWidth = config.Bind("Skills", "Molten Breath Width", 3.2f,
                "How much wider the flame is drawn than the flamethrower drone sprays it. Visual only.");
            chargeDamageCoefficient = config.Bind("Skills", "Elementium Charge Damage", 5f, "Damage coefficient per enemy trampled.");
            chargeCooldown = config.Bind("Skills", "Elementium Charge Cooldown", 8f, "Cooldown in seconds.");

            platingArmorPerPlate = config.Bind("Passive", "Armor Per Plate", 15f,
                "Armor each of his four elementium plates grants. One plate is shed for every 25% of his "
                + "health he has lost, and they are only rebuilt by Dragonflight.");

            // His Heroes of the Storm kit. Cooldowns are his own; damage is his own figures converted to
            // Risk of Rain coefficients at the same ratio his flame breath was converted at, so the kit
            // keeps its internal balance rather than each skill being guessed at separately.
            moltenFlameDamageCoefficient = config.Bind("HotS Skills", "Molten Flame Damage", 4.5f,
                "Damage coefficient per second of the flame channel.");
            moltenFlameCooldown = config.Bind("HotS Skills", "Molten Flame Cooldown", 3f, "Cooldown in seconds.");
            incinerateDamageCoefficient = config.Bind("HotS Skills", "Incinerate Damage", 3f,
                "Damage coefficient of the wing beat around him.");
            incinerateCooldown = config.Bind("HotS Skills", "Incinerate Cooldown", 7f, "Cooldown in seconds.");
            lavaBurstDamageCoefficient = config.Bind("HotS Skills", "Lava Burst Damage", 1f,
                "Damage coefficient of the impact. The pool it leaves burns for a fraction of this per second.");
            lavaBurstCooldown = config.Bind("HotS Skills", "Lava Burst Cooldown", 9f, "Cooldown in seconds.");
            onslaughtDamageCoefficient = config.Bind("HotS Skills", "Onslaught Damage", 1.8f,
                "Damage coefficient of the lunge itself.");
            onslaughtBiteDamageCoefficient = config.Bind("HotS Skills", "Onslaught Bite Damage", 3.5f,
                "Damage coefficient of the bite at the end of the lunge, where it hits hardest.");
            onslaughtCooldown = config.Bind("HotS Skills", "Onslaught Cooldown", 6f, "Cooldown in seconds.");
            earthShatterDamageCoefficient = config.Bind("HotS Skills", "Earth Shatter Damage", 2.7f,
                "Damage coefficient per fissure.");
            earthShatterCooldown = config.Bind("HotS Skills", "Earth Shatter Cooldown", 12f, "Cooldown in seconds.");
            dragonflightCooldown = config.Bind("HotS Skills", "Dragonflight Cooldown", 20f, "Cooldown in seconds.");
            dragonflightDuration = config.Bind("HotS Skills", "Dragonflight Duration", 8f,
                "Maximum seconds he stays in the sky before landing on his own.");
            dragonflightHealFraction = config.Bind("HotS Skills", "Dragonflight Heal Per Second", 0.025f,
                "Fraction of his maximum health healed per second while flying.");
            dragonflightCombatLockout = config.Bind("HotS Skills", "Dragonflight Combat Lockout", 6f,
                "Seconds after taking damage or using a skill before Dragonflight can be cast.");
            dragonFireDamageCoefficient = config.Bind("HotS Skills", "Dragon Fire Damage", 2.2f,
                "Damage coefficient of each fireball he spits down while flying.");
            dragonFireInterval = config.Bind("HotS Skills", "Dragon Fire Interval", 0.45f,
                "Seconds between those fireballs.");
            heroicCataclysmDamageCoefficient = config.Bind("HotS Skills", "Heroic Cataclysm Damage", 2.7f,
                "Damage coefficient of the impact. The scorched ground burns for a fraction of this per second.");
            heroicCataclysmCooldown = config.Bind("HotS Skills", "Heroic Cataclysm Cooldown", 30f,
                "Cooldown in seconds. His own is 90, which is a Heroes of the Storm match rather than a run.");
            bellowingRoarDamageCoefficient = config.Bind("HotS Skills", "Bellowing Roar Damage", 0.75f,
                "Damage coefficient of the roar. Its point is the rout, not the damage.");
            bellowingRoarCooldown = config.Bind("HotS Skills", "Bellowing Roar Cooldown", 25f, "Cooldown in seconds.");
            formSwitchCooldown = config.Bind("HotS Skills", "Form Switch Cooldown", 1f,
                "Cooldown in seconds on changing form. Only there to stop it being held down.");

            // His last three abilities have no slot to live in: Risk of Rain gives a survivor four, and
            // his kit has seven things in it.
            dragonflightKey = config.Bind("HotS Keys", "Dragonflight", new KeyboardShortcut(KeyCode.Z),
                "Takes off, heals him and rebuilds his plates.");
            formSwitchKey = config.Bind("HotS Keys", "Form Switch", new KeyboardShortcut(KeyCode.X),
                "Swaps between Destroyer form (Incinerate and Onslaught) and World Breaker form "
                + "(Lava Burst and Earth Shatter).");
            heroicCataclysmKey = config.Bind("HotS Keys", "Cataclysm", new KeyboardShortcut(KeyCode.C),
                "His heroic: flies across the battlefield leaving it burning.");
            bellowingRoarKey = config.Bind("HotS Keys", "Bellowing Roar", new KeyboardShortcut(KeyCode.V),
                "His second heroic: routs everything around him.");
        }
    }
}
