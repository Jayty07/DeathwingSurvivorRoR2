using System;
using Deathwing.SkillStates;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Deathwing.Modules
{
    internal static class Skills
    {
        /// <summary>His form skills, held so the form component can swap Utility and Special between them.</summary>
        internal static SkillDef incinerate { get; private set; }

        internal static SkillDef lavaBurst { get; private set; }

        internal static SkillDef onslaught { get; private set; }

        internal static SkillDef earthShatter { get; private set; }

        /// <summary>
        /// The names his off-slot skills are found by at runtime. He has more abilities than Risk of
        /// Rain 2 has slots, so these four live on <see cref="GenericSkill"/> components of their own,
        /// outside the four the HUD draws, and are fired from keys by <see cref="DeathwingForms"/>.
        /// </summary>
        internal const string dragonflightSkillName = "DeathwingDragonflight";

        internal const string formSwitchSkillName = "DeathwingFormSwitch";

        internal const string cataclysmSkillName = "DeathwingCataclysm";

        internal const string bellowingRoarSkillName = "DeathwingBellowingRoar";

        internal static void Init(GameObject bodyPrefab)
        {
            SkillLocator skillLocator = bodyPrefab.GetComponent<SkillLocator>();
            if (!skillLocator)
            {
                Log.Error("Body has no SkillLocator; skills will not be assigned.");
                return;
            }

            SetupPassive(skillLocator);

            // Deathwing's own icons, each falling back to the chassis' icon for that slot: skill icon
            // addresses are not stable between game versions, and a null icon leaves an unreadable
            // blank slot in the HUD.
            Sprite primaryIcon = ChassisIcon(skillLocator.primary);
            Sprite secondaryIcon = ChassisIcon(skillLocator.secondary);
            Sprite utilityIcon = ChassisIcon(skillLocator.utility);
            Sprite specialIcon = ChassisIcon(skillLocator.special);

            SteppedSkillDef claw = CreateSteppedSkill<MoltenClaw>(
                skillName: "DeathwingClawAndBite",
                nameToken: Tokens.primaryName,
                descriptionToken: Tokens.primaryDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.destroyer, primaryIcon),
                stepCount: MoltenClaw.comboLength);
            // A primary needs a stock to spend: with a max of zero the slot is permanently greyed out.
            // A zero recharge interval restocks it instantly, which is how vanilla primaries work.
            claw.baseRechargeInterval = 0f;
            claw.baseMaxStock = 1;
            claw.mustKeyPress = false;
            claw.activationStateMachineName = "Weapon";
            claw.interruptPriority = InterruptPriority.Any;

            SkillDef flame = CreateSkill<MoltenFlame>(
                skillName: "DeathwingMoltenFlame",
                nameToken: Tokens.secondaryName,
                descriptionToken: Tokens.secondaryDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.moltenFlame, secondaryIcon));
            flame.baseRechargeInterval = Tuning.moltenFlameCooldown.Value;
            flame.activationStateMachineName = "Weapon";
            flame.interruptPriority = InterruptPriority.Skill;
            // Held down for as long as the flame should last, and the cooldown only starts once it stops.
            flame.mustKeyPress = true;
            flame.beginSkillCooldownOnSkillEnd = true;
            flame.canceledFromSprinting = true;

            SkillDef boulder = CreateSkill<HurlMoltenBoulder>(
                skillName: "DeathwingMoltenBoulder",
                nameToken: Tokens.boulderName,
                descriptionToken: Tokens.boulderDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.lavaBurst, secondaryIcon));
            boulder.baseRechargeInterval = Tuning.boulderCooldown.Value;
            boulder.activationStateMachineName = "Weapon";
            boulder.interruptPriority = InterruptPriority.Skill;

            incinerate = CreateSkill<Incinerate>(
                skillName: "DeathwingIncinerate",
                nameToken: Tokens.incinerateName,
                descriptionToken: Tokens.incinerateDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.incinerate, utilityIcon));
            incinerate.baseRechargeInterval = Tuning.incinerateCooldown.Value;
            incinerate.activationStateMachineName = "Weapon";
            incinerate.interruptPriority = InterruptPriority.Skill;

            lavaBurst = CreateSkill<LavaBurst>(
                skillName: "DeathwingLavaBurst",
                nameToken: Tokens.lavaBurstName,
                descriptionToken: Tokens.lavaBurstDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.lavaBurst, utilityIcon));
            lavaBurst.baseRechargeInterval = Tuning.lavaBurstCooldown.Value;
            lavaBurst.activationStateMachineName = "Weapon";
            lavaBurst.interruptPriority = InterruptPriority.Skill;

            onslaught = CreateSkill<Onslaught>(
                skillName: "DeathwingOnslaught",
                nameToken: Tokens.onslaughtName,
                descriptionToken: Tokens.onslaughtDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.onslaught, specialIcon));
            onslaught.baseRechargeInterval = Tuning.onslaughtCooldown.Value;
            onslaught.activationStateMachineName = "Body";
            onslaught.interruptPriority = InterruptPriority.PrioritySkill;
            onslaught.cancelSprintingOnActivation = false;

            earthShatter = CreateSkill<EarthShatter>(
                skillName: "DeathwingEarthShatter",
                nameToken: Tokens.earthShatterName,
                descriptionToken: Tokens.earthShatterDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.earthShatter, specialIcon));
            earthShatter.baseRechargeInterval = Tuning.earthShatterCooldown.Value;
            earthShatter.activationStateMachineName = "Body";
            earthShatter.interruptPriority = InterruptPriority.PrioritySkill;

            SkillDef charge = CreateSkill<ElementiumCharge>(
                skillName: "DeathwingElementiumCharge",
                nameToken: Tokens.chargeName,
                descriptionToken: Tokens.chargeDescription,
                icon: utilityIcon);
            charge.baseRechargeInterval = Tuning.chargeCooldown.Value;
            charge.activationStateMachineName = "Body";
            charge.interruptPriority = InterruptPriority.PrioritySkill;
            charge.cancelSprintingOnActivation = false;

            // The dive is entered from flight rather than pressed, so it alone needs no skill def.
            ContentAddition.AddEntityState<DiveSlam>(out _);

            SetupExtraSkills(bodyPrefab, specialIcon);

            // His Heroes of the Storm kit is the default in every slot; the original Deathwing skills are
            // kept as the second variant so the old kit is still playable from the loadout screen.
            AssignFamily(skillLocator, SkillSlot.Primary, claw);
            AssignFamily(skillLocator, SkillSlot.Secondary, flame, boulder);
            AssignFamily(skillLocator, SkillSlot.Utility, incinerate, charge);
            AssignFamily(skillLocator, SkillSlot.Special, onslaught);
        }

        /// <summary>
        /// Builds the four abilities that have no slot to sit in. They are real skills on real
        /// <see cref="GenericSkill"/> components rather than keys with a stopwatch behind them, so their
        /// cooldowns are the game's - which means cooldown items reach them, a client sees the same
        /// cooldown the host does, and the HUD has something to draw.
        /// </summary>
        private static void SetupExtraSkills(GameObject bodyPrefab, Sprite fallbackIcon)
        {
            SkillDef flight = CreateSkill<Dragonflight>(
                skillName: dragonflightSkillName,
                nameToken: Tokens.dragonflightName,
                descriptionToken: Tokens.dragonflightDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.dragonflight, fallbackIcon));
            flight.baseRechargeInterval = Tuning.dragonflightCooldown.Value;
            flight.activationStateMachineName = "Body";
            flight.interruptPriority = InterruptPriority.PrioritySkill;
            flight.cancelSprintingOnActivation = false;

            SkillDef form = CreateSkill<SwitchForm>(
                skillName: formSwitchSkillName,
                nameToken: Tokens.formSwitchName,
                descriptionToken: Tokens.formSwitchDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.formSwitch, fallbackIcon));
            form.baseRechargeInterval = Tuning.formSwitchCooldown.Value;
            form.activationStateMachineName = "Weapon";
            form.interruptPriority = InterruptPriority.Any;
            form.isCombatSkill = false;
            form.cancelSprintingOnActivation = false;

            SkillDef cataclysm = CreateSkill<Cataclysm>(
                skillName: cataclysmSkillName,
                nameToken: Tokens.cataclysmName,
                descriptionToken: Tokens.cataclysmDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.cataclysm, fallbackIcon));
            cataclysm.baseRechargeInterval = Tuning.heroicCataclysmCooldown.Value;
            cataclysm.activationStateMachineName = "Body";
            cataclysm.interruptPriority = InterruptPriority.PrioritySkill;

            SkillDef roar = CreateSkill<BellowingRoar>(
                skillName: bellowingRoarSkillName,
                nameToken: Tokens.bellowingRoarName,
                descriptionToken: Tokens.bellowingRoarDescription,
                icon: DeathwingIcons.Get(DeathwingIcons.aspectOfDeath, fallbackIcon));
            roar.baseRechargeInterval = Tuning.bellowingRoarCooldown.Value;
            roar.activationStateMachineName = "Body";
            roar.interruptPriority = InterruptPriority.PrioritySkill;

            AddExtraSkill(bodyPrefab, flight);
            AddExtraSkill(bodyPrefab, form);
            AddExtraSkill(bodyPrefab, cataclysm);
            AddExtraSkill(bodyPrefab, roar);
        }

        private static void AddExtraSkill(GameObject bodyPrefab, SkillDef skillDef)
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = $"{skillDef.skillName}Family";
            family.variants = new[]
            {
                new SkillFamily.Variant
                {
                    skillDef = skillDef,
                    unlockableDef = null,
                    viewableNode = new ViewablesCatalog.Node(skillDef.skillNameToken, false, null)
                }
            };

            ContentAddition.AddSkillFamily(family);

            GenericSkill genericSkill = bodyPrefab.AddComponent<GenericSkill>();
            genericSkill._skillFamily = family;
            genericSkill.skillName = skillDef.skillName;

            // Kept out of both selection screens: these are not a choice, and a loadout row with one
            // variant is a row the player can only stare at.
            genericSkill.hideInCharacterSelect = true;
            genericSkill.hideInLoadoutSelect = true;
        }

        private static void SetupPassive(SkillLocator skillLocator)
        {
            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = Tokens.passiveName;
            skillLocator.passiveSkill.skillDescriptionToken = Tokens.passiveDescription;
            skillLocator.passiveSkill.icon =
                DeathwingIcons.Get(DeathwingIcons.aspectOfDeath, ChassisIcon(skillLocator.special));
        }

        /// <summary>The icon the chassis already used for a slot; always resolved, unlike an address.</summary>
        private static Sprite ChassisIcon(GenericSkill genericSkill)
        {
            SkillFamily family = genericSkill ? genericSkill.skillFamily : null;
            if (family == null || family.variants == null || family.variants.Length == 0)
            {
                return null;
            }

            return family.variants[0].skillDef ? family.variants[0].skillDef.icon : null;
        }

        private static SkillDef CreateSkill<TState>(string skillName, string nameToken, string descriptionToken, Sprite icon)
            where TState : EntityState
        {
            SkillDef skillDef = ScriptableObject.CreateInstance<SkillDef>();
            Configure<TState>(skillDef, skillName, nameToken, descriptionToken, icon);
            return skillDef;
        }

        private static SteppedSkillDef CreateSteppedSkill<TState>(string skillName, string nameToken, string descriptionToken, Sprite icon, int stepCount)
            where TState : EntityState
        {
            SteppedSkillDef skillDef = ScriptableObject.CreateInstance<SteppedSkillDef>();
            Configure<TState>(skillDef, skillName, nameToken, descriptionToken, icon);
            skillDef.stepCount = stepCount;
            skillDef.stepGraceDuration = 0.6f;
            return skillDef;
        }

        private static void Configure<TState>(SkillDef skillDef, string skillName, string nameToken, string descriptionToken, Sprite icon)
            where TState : EntityState
        {
            skillDef.skillName = skillName;
            skillDef.skillNameToken = nameToken;
            skillDef.skillDescriptionToken = descriptionToken;
            skillDef.icon = icon;
            skillDef.activationState = ContentAddition.AddEntityState<TState>(out _);
            skillDef.activationStateMachineName = "Weapon";
            skillDef.interruptPriority = InterruptPriority.Skill;
            skillDef.baseMaxStock = 1;
            skillDef.rechargeStock = 1;
            skillDef.requiredStock = 1;
            skillDef.stockToConsume = 1;
            skillDef.beginSkillCooldownOnSkillEnd = false;
            skillDef.canceledFromSprinting = false;
            skillDef.cancelSprintingOnActivation = true;
            skillDef.forceSprintDuringState = false;
            skillDef.fullRestockOnAssign = true;
            skillDef.resetCooldownTimerOnUse = false;
            skillDef.isCombatSkill = true;
            skillDef.mustKeyPress = true;
            skillDef.attackSpeedBuffsRestockSpeed = false;

            ContentAddition.AddSkillDef(skillDef);
        }

        /// <summary>
        /// Replaces the borrowed chassis' skill family for a slot so Deathwing's loadout is his own
        /// rather than a mutation of the base survivor's shared assets.
        /// </summary>
        private static void AssignFamily(SkillLocator skillLocator, SkillSlot slot, params SkillDef[] skillDefs)
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            family.variants = new SkillFamily.Variant[skillDefs.Length];
            ((ScriptableObject)family).name = $"Deathwing{slot}Family";

            for (int i = 0; i < skillDefs.Length; i++)
            {
                family.variants[i] = new SkillFamily.Variant
                {
                    skillDef = skillDefs[i],
                    unlockableDef = null,
                    viewableNode = new ViewablesCatalog.Node(skillDefs[i].skillNameToken, false, null)
                };
            }

            ContentAddition.AddSkillFamily(family);

            GenericSkill genericSkill = SlotToSkill(skillLocator, slot);
            if (!genericSkill)
            {
                Log.Error($"Body is missing a GenericSkill for slot {slot}.");
                return;
            }

            genericSkill._skillFamily = family;
            genericSkill.skillName = skillDefs[0].skillName;
        }

        private static GenericSkill SlotToSkill(SkillLocator skillLocator, SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Primary: return skillLocator.primary;
                case SkillSlot.Secondary: return skillLocator.secondary;
                case SkillSlot.Utility: return skillLocator.utility;
                case SkillSlot.Special: return skillLocator.special;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
