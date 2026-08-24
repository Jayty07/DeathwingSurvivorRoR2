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
    /// and Special do, and the rest - Dragonflight and his two heroics - hang off rebindable keys this
    /// component drives, with their own cooldowns since they are not in a slot the HUD can track.
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
        private EntityStateMachine bodyStateMachine;

        private Form form = Form.Destroyer;
        private float dragonflightCooldown;
        private float cataclysmCooldown;
        private float roarCooldown;

        /// <summary>Which form he is in, read by the skills that behave differently in each.</summary>
        internal Form currentForm => form;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
            skills = GetComponent<SkillLocator>();
            aspect = GetComponent<AspectOfDeath>();
            bodyStateMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
        }

        private void FixedUpdate()
        {
            float delta = Time.fixedDeltaTime;
            dragonflightCooldown = Mathf.Max(0f, dragonflightCooldown - delta);
            cataclysmCooldown = Mathf.Max(0f, cataclysmCooldown - delta);
            roarCooldown = Mathf.Max(0f, roarCooldown - delta);
        }

        private void Update()
        {
            // Raw key reads, so only the player who owns this body may act on them.
            if (!body || !body.hasEffectiveAuthority || !body.isPlayerControlled)
            {
                return;
            }

            if (Tuning.formSwitchKey.Value.IsDown())
            {
                SetForm(form == Form.Destroyer ? Form.WorldBreaker : Form.Destroyer);
            }

            if (Tuning.dragonflightKey.Value.IsDown() && dragonflightCooldown <= 0f && CanFly())
            {
                if (Cast(new Dragonflight()))
                {
                    dragonflightCooldown = Tuning.dragonflightCooldown.Value;
                }
            }

            if (Tuning.heroicCataclysmKey.Value.IsDown() && cataclysmCooldown <= 0f)
            {
                if (Cast(new Cataclysm()))
                {
                    cataclysmCooldown = Tuning.heroicCataclysmCooldown.Value;
                }
            }

            if (Tuning.bellowingRoarKey.Value.IsDown() && roarCooldown <= 0f)
            {
                if (Cast(new BellowingRoar()))
                {
                    roarCooldown = Tuning.bellowingRoarCooldown.Value;
                }
            }
        }

        /// <summary>
        /// Dragonflight is barred for a few seconds after he is hurt, as it is in Heroes of the Storm:
        /// it is an escape he has to earn a moment of peace for, not one he can press under fire.
        /// </summary>
        private bool CanFly()
        {
            return body.outOfDangerStopwatch >= Tuning.dragonflightCombatLockout.Value;
        }

        private bool Cast(EntityState state)
        {
            if (!bodyStateMachine)
            {
                return false;
            }

            return bodyStateMachine.SetInterruptState(state, InterruptPriority.PrioritySkill);
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
