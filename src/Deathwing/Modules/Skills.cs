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

            SteppedSkillDef claw = CreateSteppedSkill<MoltenClaw>(
                skillName: "DeathwingMoltenClaw",
                nameToken: Tokens.primaryName,
                descriptionToken: Tokens.primaryDescription,
                iconKeys: new[] { "RoR2/Base/Loader/texLoaderIconSwingFist.tif", "RoR2/Base/Commando/texCommandoIconM1.tif" },
                stepCount: 2);
            claw.baseRechargeInterval = 0f;
            claw.baseMaxStock = 0;
            claw.mustKeyPress = false;
            claw.activationStateMachineName = "Weapon";
            claw.interruptPriority = InterruptPriority.Any;

            SkillDef boulder = CreateSkill<HurlMoltenBoulder>(
                skillName: "DeathwingMoltenBoulder",
                nameToken: Tokens.secondaryName,
                descriptionToken: Tokens.secondaryDescription,
                iconKeys: new[] { "RoR2/Base/Commando/texCommandoIconM2.tif" });
            boulder.baseRechargeInterval = Tuning.boulderCooldown.Value;
            boulder.baseMaxStock = 1;
            boulder.activationStateMachineName = "Weapon";
            boulder.interruptPriority = InterruptPriority.Skill;

            SkillDef charge = CreateSkill<ElementiumCharge>(
                skillName: "DeathwingElementiumCharge",
                nameToken: Tokens.utilityName,
                descriptionToken: Tokens.utilityDescription,
                iconKeys: new[] { "RoR2/Base/Commando/texCommandoIconUtility.tif" });
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
                iconKeys: new[] { "RoR2/Base/Croco/texCrocoIconUtility.tif", "RoR2/Base/Commando/texCommandoIconUtility.tif" });
            flight.baseRechargeInterval = Tuning.flightCooldown.Value;
            flight.baseMaxStock = 1;
            flight.activationStateMachineName = "Body";
            flight.interruptPriority = InterruptPriority.PrioritySkill;
            flight.isCombatSkill = false;
            flight.cancelSprintingOnActivation = false;
            // Held down to keep flying, so the skill must not re-trigger every frame the key is held.
            flight.mustKeyPress = true;
            flight.beginSkillCooldownOnSkillEnd = true;

            SkillDef cataclysm = CreateSkill<Cataclysm>(
                skillName: "DeathwingCataclysm",
                nameToken: Tokens.specialName,
                descriptionToken: Tokens.specialDescription,
                iconKeys: new[] { "RoR2/Base/Commando/texCommandoIconSpecial.tif" });
            cataclysm.baseRechargeInterval = Tuning.cataclysmCooldown.Value;
            cataclysm.baseMaxStock = 1;
            cataclysm.activationStateMachineName = "Body";
            cataclysm.interruptPriority = InterruptPriority.PrioritySkill;

            // DiveSlam is entered from flight rather than from a slot, but still needs registering.
            ContentAddition.AddEntityState<DiveSlam>(out _);

            AssignFamily(skillLocator, SkillSlot.Primary, claw);
            AssignFamily(skillLocator, SkillSlot.Secondary, boulder);
            AssignFamily(skillLocator, SkillSlot.Utility, charge, flight);
            AssignFamily(skillLocator, SkillSlot.Special, cataclysm);
        }

        private static void SetupPassive(SkillLocator skillLocator)
        {
            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = Tokens.passiveName;
            skillLocator.passiveSkill.skillDescriptionToken = Tokens.passiveDescription;
            skillLocator.passiveSkill.icon = DeathwingAssets.Load<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif");
        }

        private static SkillDef CreateSkill<TState>(string skillName, string nameToken, string descriptionToken, string[] iconKeys)
            where TState : EntityState
        {
            SkillDef skillDef = ScriptableObject.CreateInstance<SkillDef>();
            Configure<TState>(skillDef, skillName, nameToken, descriptionToken, iconKeys);
            return skillDef;
        }

        private static SteppedSkillDef CreateSteppedSkill<TState>(string skillName, string nameToken, string descriptionToken, string[] iconKeys, int stepCount)
            where TState : EntityState
        {
            SteppedSkillDef skillDef = ScriptableObject.CreateInstance<SteppedSkillDef>();
            Configure<TState>(skillDef, skillName, nameToken, descriptionToken, iconKeys);
            skillDef.stepCount = stepCount;
            skillDef.stepGraceDuration = 0.6f;
            return skillDef;
        }

        private static void Configure<TState>(SkillDef skillDef, string skillName, string nameToken, string descriptionToken, string[] iconKeys)
            where TState : EntityState
        {
            skillDef.skillName = skillName;
            skillDef.skillNameToken = nameToken;
            skillDef.skillDescriptionToken = descriptionToken;
            skillDef.icon = DeathwingAssets.Load<Sprite>(iconKeys);
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
