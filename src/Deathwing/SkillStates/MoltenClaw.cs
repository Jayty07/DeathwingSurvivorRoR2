using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Primary: a heavy two-step claw swipe. Slow to start, no cancel window until the swing has
    /// landed, and it ignites everything it touches.
    /// </summary>
    public class MoltenClaw : BaseDeathwingSkillState, SteppedSkillDef.IStepSetter
    {
        public static float baseDuration = 1.15f;
        public static float attackStartFraction = 0.32f;
        public static float attackEndFraction = 0.55f;
        public static float hitboxRadius = 5.5f;
        public static float hitForce = 900f;
        public static float selfForwardImpulse = 4f;

        private int step;
        private float duration;
        private OverlapAttack attack;
        private bool hasHit;
        private bool hasFired;

        void SteppedSkillDef.IStepSetter.SetStep(int i) => step = i;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;

            attack = new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                damage = Tuning.clawDamageCoefficient.Value * damageStat,
                procCoefficient = 1f,
                hitEffectPrefab = DeathwingAssets.fireImpactEffect,
                forceVector = Vector3.zero,
                pushAwayForce = hitForce,
                hitBoxGroup = FindHitBoxGroup(DeathwingBody.clawHitBoxGroupName),
                isCrit = RollCrit(),
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.IgniteOnHit
            };

            StartAimMode(2f);
            characterBody.SetAimTimer(duration + 0.5f);
            Util.PlaySound(Sounds.clawSwing, gameObject);
            PlayCrossfade("Gesture, Override", "FireGun", "FireGun.playbackRate", duration, 0.1f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            float progress = fixedAge / duration;
            if (progress >= attackStartFraction && progress <= attackEndFraction)
            {
                if (!hasFired)
                {
                    hasFired = true;
                    if (isAuthority && characterMotor && characterDirection)
                    {
                        // Deathwing leans into the swing rather than sliding around freely.
                        characterMotor.rootMotion += characterDirection.forward * (selfForwardImpulse * GetDeltaTime());
                    }

                    SpawnClawEffect();
                }

                if (isAuthority && attack != null && attack.Fire() && !hasHit)
                {
                    hasHit = true;
                    characterBody.AddTimedBuff(Buffs.elementiumPlating, 0.6f);
                }
            }

            if (fixedAge >= duration && isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }

        private void SpawnClawEffect()
        {
            if (!characterDirection)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * (1.2f * characterScale) + characterDirection.forward * (2.4f * characterScale);
            SpawnFireEffect(origin, 1.2f * characterScale);
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write((byte)step);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            step = reader.ReadByte();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
