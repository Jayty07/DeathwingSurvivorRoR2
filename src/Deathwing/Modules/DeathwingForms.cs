using Deathwing.SkillStates;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Everything in his Heroes of the Storm kit that Risk of Rain 2 has nowhere to put. He has seven
    /// abilities and a form toggle; a survivor has four slots. His two forms therefore swap what Utility
    /// and Special do, and the rest - Dragonflight, the form toggle and his two heroics - live on skills
    /// of their own outside those slots, which this component fires from rebindable keys.
    /// </summary>
    public class DeathwingForms : MonoBehaviour
    {
        internal enum Form
        {
            Destroyer,
            WorldBreaker
        }

        private CharacterBody body;
        private SkillLocator skills;
        private AspectOfDeath aspect;

        private Form form = Form.Destroyer;

        /// <summary>Which form he is in, read by the skills that behave differently in each.</summary>
        internal Form currentForm => form;

        /// <summary>His off-slot skills, in the order the extra HUD row draws them.</summary>
        internal GenericSkill dragonflightSkill { get; private set; }

        internal GenericSkill formSwitchSkill { get; private set; }

        internal GenericSkill cataclysmSkill { get; private set; }

        internal GenericSkill bellowingRoarSkill { get; private set; }

        /// <summary>Seconds left of the post-damage bar on Dragonflight, 0 when he is free to fly.</summary>
        internal float flightLockoutRemaining =>
            body ? Mathf.Max(0f, Tuning.dragonflightCombatLockout.Value - body.outOfDangerStopwatch) : 0f;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            skills = GetComponent<SkillLocator>();
            aspect = GetComponent<AspectOfDeath>();

            foreach (GenericSkill skill in GetComponents<GenericSkill>())
            {
                switch (skill.skillName)
                {
                    case Skills.dragonflightSkillName:
                        dragonflightSkill = skill;
                        break;
                    case Skills.formSwitchSkillName:
                        formSwitchSkill = skill;
                        break;
                    case Skills.cataclysmSkillName:
                        cataclysmSkill = skill;
                        break;
                    case Skills.bellowingRoarSkillName:
                        bellowingRoarSkill = skill;
                        break;
                }
            }
        }

        /// <summary>
        /// The game only hands its cooldown reduction to the four slots it knows about, so his off-slot
        /// skills are given the same figures his Special is running with. Without this, Alien Head and
        /// friends would quietly do nothing for his heroics.
        /// </summary>
        private void FixedUpdate()
        {
            GenericSkill reference = skills ? skills.special : null;
            if (!reference)
            {
                return;
            }

            MatchCooldownStats(dragonflightSkill, reference);
            MatchCooldownStats(formSwitchSkill, reference);
            MatchCooldownStats(cataclysmSkill, reference);
            MatchCooldownStats(bellowingRoarSkill, reference);
        }

        private static void MatchCooldownStats(GenericSkill skill, GenericSkill reference)
        {
            if (!skill)
            {
                return;
            }

            skill.cooldownScale = reference.cooldownScale;
            skill.flatCooldownReduction = reference.flatCooldownReduction;
        }

        private void Update()
        {
            // Raw key reads, so only the player who owns this body may act on them.
            if (!body || !body.hasEffectiveAuthority || !body.isPlayerControlled)
            {
                return;
            }

            if (Tuning.formSwitchKey.Value.IsDown() && formSwitchSkill)
            {
                formSwitchSkill.ExecuteIfReady();
            }

            if (Tuning.dragonflightKey.Value.IsDown() && dragonflightSkill && CanFly())
            {
                dragonflightSkill.ExecuteIfReady();
            }

            if (Tuning.heroicCataclysmKey.Value.IsDown() && cataclysmSkill)
            {
                cataclysmSkill.ExecuteIfReady();
            }

            if (Tuning.bellowingRoarKey.Value.IsDown() && bellowingRoarSkill)
            {
                bellowingRoarSkill.ExecuteIfReady();
            }
        }

        /// <summary>
        /// Dragonflight is barred for a few seconds after he is hurt, as it is in Heroes of the Storm:
        /// it is an escape he has to earn a moment of peace for, not one he can press under fire.
        /// </summary>
        internal bool CanFly()
        {
            return body && body.outOfDangerStopwatch >= Tuning.dragonflightCombatLockout.Value;
        }

        /// <summary>Flips to his other form. Driven by the form switch skill so it shares its cooldown.</summary>
        internal void Toggle()
        {
            SetForm(form == Form.Destroyer ? Form.WorldBreaker : Form.Destroyer);
        }

        /// <summary>
        /// Swaps the form and, with it, his W and E. Overrides are used rather than reassigning the slot
        /// so his loadout choice underneath is left intact.
        /// </summary>
        internal void SetForm(Form target)
        {
            if (target == form)
            {
                return;
            }

            form = target;
            ApplyOverrides();

            Util.PlaySound(target == Form.WorldBreaker ? Sounds.chargeStart : Sounds.wingFlap, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.7f);
            DeathwingAssets.SpawnEffect(
                DeathwingAssets.roarEffect, body.corePosition, 2f * Tuning.modelScale.Value, gameObject);
        }

        /// <summary>Reforges his plates and picks the form he lands in, the way Dragonflight does.</summary>
        internal void OnDragonflightLanded(Form landingForm)
        {
            SetForm(landingForm);

            if (aspect)
            {
                aspect.Reforge();
            }
        }

        private void ApplyOverrides()
        {
            Override(skills ? skills.utility : null, Skills.incinerate, Skills.lavaBurst);
            Override(skills ? skills.special : null, Skills.onslaught, Skills.earthShatter);
        }

        private void Override(GenericSkill slot, SkillDef destroyerSkill, SkillDef worldBreakerSkill)
        {
            if (!slot || !destroyerSkill || !worldBreakerSkill)
            {
                return;
            }

            // Only his own skills are swapped: a slot holding one of the legacy loadout variants is left
            // alone rather than being silently replaced by a form skill the player did not pick.
            if (slot.baseSkill != destroyerSkill && slot.baseSkill != worldBreakerSkill)
            {
                return;
            }

            SkillDef wanted = form == Form.WorldBreaker ? worldBreakerSkill : destroyerSkill;
            SkillDef unwanted = form == Form.WorldBreaker ? destroyerSkill : worldBreakerSkill;

            slot.UnsetSkillOverride(this, unwanted, GenericSkill.SkillOverridePriority.Contextual);
            if (wanted != slot.baseSkill)
            {
                slot.SetSkillOverride(this, wanted, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
    }
}
