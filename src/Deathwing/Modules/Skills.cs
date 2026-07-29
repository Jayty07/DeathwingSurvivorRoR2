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
        internal static void Init(GameObject bodyPrefab)
        {
            SkillLocator skillLocator = bodyPrefab.GetComponent<SkillLocator>();
            if (!skillLocator)
            {
                Log.Error("Body has no SkillLocator; skills will not be assigned.");
                return;
            }

            SetupPassive(skillLocator);

            // Icons come off the chassis' own skills: skill icon addresses are not stable between game
            // versions, and a null icon leaves an unreadable blank slot in the HUD.
            Sprite primaryIcon = ChassisIcon(skillLocator.primary);
            Sprite secondaryIcon = ChassisIcon(skillLocator.secondary);
            Sprite utilityIcon = ChassisIcon(skillLocator.utility);
            Sprite specialIcon = ChassisIcon(skillLocator.special);

            SteppedSkillDef claw = CreateSteppedSkill<MoltenClaw>(
                skillName: "DeathwingMoltenClaw",
                nameToken: Tokens.primaryName,
                descriptionToken: Tokens.primaryDescription,
                icon: primaryIcon,
                stepCount: MoltenClaw.comboLength);
            // A primary needs a stock to spend: with a max of zero the slot is permanently greyed out.
            // A zero recharge interval restocks it instantly, which is how vanilla primaries work.
            claw.baseRechargeInterval = 0f;
            claw.baseMaxStock = 1;
            claw.mustKeyPress = false;
            claw.activationStateMachineName = "Weapon";
            claw.interruptPriority = InterruptPriority.Any;

            SkillDef boulder = CreateSkill<HurlMoltenBoulder>(
                skillName: "DeathwingMoltenBoulder",
                nameToken: Tokens.secondaryName,
                descriptionToken: Tokens.secondaryDescription,
                icon: secondaryIcon);
            boulder.baseRechargeInterval = Tuning.boulderCooldown.Value;
            boulder.baseMaxStock = 1;
            boulder.activationStateMachineName = "Weapon";
            boulder.interruptPriority = InterruptPriority.Skill;

            SkillDef breath = CreateSkill<MoltenBreath>(
                skillName: "DeathwingMoltenBreath",
                nameToken: Tokens.breathName,
                descriptionToken: Tokens.breathDescription,
                icon: secondaryIcon);
            breath.baseRechargeInterval = Tuning.breathCooldown.Value;
            breath.baseMaxStock = 1;
            breath.activationStateMachineName = "Weapon";
            breath.interruptPriority = InterruptPriority.Skill;
            // Held down for as long as the flame should last, and the cooldown only starts once it stops.
            breath.mustKeyPress = true;
            breath.beginSkillCooldownOnSkillEnd = true;
            breath.canceledFromSprinting = true;

            SkillDef charge = CreateSkill<ElementiumCharge>(
                skillName: "DeathwingElementiumCharge",
                nameToken: Tokens.utilityName,
                descriptionToken: Tokens.utilityDescription,
                icon: utilityIcon);
            charge.baseRechargeInterval = Tuning.chargeCooldown.Value;
            charge.baseMaxStock = 1;
            charge.activationStateMachineName = "Body";
            charge.interruptPriority = InterruptPriority.PrioritySkill;
            charge.isCombatSkill = true;
            charge.cancelSprintingOnActivation = false;

            SkillDef flight = CreateSkill<WingsOfTheDestroyer>(
                skillName: "DeathwingWings",
                nameToken: Tokens.flightName,
                descriptionToken: Tokens.flightDescription,
                icon: utilityIcon);
            flight.baseRechargeInterval = Tuning.flightCooldown.Value;
            flight.baseMaxStock = 1;
            flight.activationStateMachineName = "Body";
            flight.interruptPriority = InterruptPriority.PrioritySkill;
            flight.isCombatSkill = false;
            flight.cancelSprintingOnActivation = false;
            // A toggle: the state watches for the second press itself, so the slot must not re-activate
            // on every frame the key is held.
            flight.mustKeyPress = true;
            flight.beginSkillCooldownOnSkillEnd = true;

            SkillDef cataclysm = CreateSkill<Cataclysm>(
                skillName: "DeathwingCataclysm",
                nameToken: Tokens.specialName,
                descriptionToken: Tokens.specialDescription,
                icon: specialIcon);
            cataclysm.baseRechargeInterval = Tuning.cataclysmCooldown.Value;
            cataclysm.baseMaxStock = 1;
            cataclysm.activationStateMachineName = "Body";
            cataclysm.interruptPriority = InterruptPriority.PrioritySkill;

            // DiveSlam is entered from flight rather than from a slot, but still needs registering.
            ContentAddition.AddEntityState<DiveSlam>(out _);

            AssignFamily(skillLocator, SkillSlot.Primary, claw);
            AssignFamily(skillLocator, SkillSlot.Secondary, boulder, breath);
            AssignFamily(skillLocator, SkillSlot.Utility, charge, flight);
            AssignFamily(skillLocator, SkillSlot.Special, cataclysm);
        }

        private static void SetupPassive(SkillLocator skillLocator)
        {
            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = Tokens.passiveName;
            skillLocator.passiveSkill.skillDescriptionToken = Tokens.passiveDescription;
            skillLocator.passiveSkill.icon = ChassisIcon(skillLocator.special);
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
