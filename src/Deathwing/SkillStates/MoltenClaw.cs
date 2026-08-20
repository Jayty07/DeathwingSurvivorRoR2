using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Primary: a heavy three-hit combo. Two claw swipes, then an overhead slam that adds a blast
    /// around the impact and launches what it hits. Slow to start, no cancel window until the swing
    /// has landed, and every hit ignites.
    /// </summary>
    public class MoltenClaw : BaseDeathwingSkillState, SteppedSkillDef.IStepSetter
    {
        public static float baseDuration = 1.15f;
        public static float attackStartFraction = 0.32f;
        public static float attackEndFraction = 0.55f;
        public static float hitboxRadius = 5.5f;
        public static float hitForce = 900f;
        public static float selfForwardImpulse = 4f;

        /// <summary>The slam is slower and hits harder than the two swipes that set it up.</summary>
        public static int comboLength = 3;
        public static float finisherDurationMultiplier = 1.35f;
        public static float finisherDamageMultiplier = 1.6f;
        public static float finisherBlastRadius = 9f;
        public static float finisherBlastForce = 2400f;
        public static float swipeShakeMagnitude = 1.6f;
        public static float finisherShakeMagnitude = 5f;

        private int step;
        private float duration;
        private OverlapAttack attack;
        private bool hasHit;
        private bool hasFired;

        void SteppedSkillDef.IStepSetter.SetStep(int i) => step = i;

        /// <summary>The last step of the combo, which lands as a slam rather than a swipe.</summary>
        private bool IsFinisher => step % comboLength == comboLength - 1;

        private float DamageCoefficient =>
            Tuning.clawDamageCoefficient.Value * (IsFinisher ? finisherDamageMultiplier : 1f);

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            if (IsFinisher)
            {
                duration *= finisherDurationMultiplier;
            }

            attack = new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                damage = DamageCoefficient * damageStat,
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
            Util.PlaySound(IsFinisher ? Sounds.clawSlam : Sounds.clawSwing, gameObject);

            // The real model has a claw swipe per step of the combo. Without it the chassis has no claw
            // animations at all, so the combo is telegraphed through the gestures it does have: the
            // swipes reuse the firing gesture, the slam the heavier throw.
            if (IsFinisher)
            {
                PlayDragonAnimation(DeathwingClips.clawC, duration,
                    "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
            }
            else
            {
                PlayDragonAnimation(step % comboLength == 0 ? DeathwingClips.clawA : DeathwingClips.clawB, duration,
                    "Gesture, Override", "FireGun", "FireGun.playbackRate");
            }
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
                    ShakeCamera(
                        transform.position,
                        IsFinisher ? finisherShakeMagnitude : swipeShakeMagnitude,
                        IsFinisher ? 0.4f : 0.2f,
                        IsFinisher ? 40f : 20f);

                    if (IsFinisher)
                    {
                        Slam();
                    }
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
            SpawnFireEffect(origin, (IsFinisher ? 2.4f : 1.3f) * characterScale);
        }

        /// <summary>
        /// The finisher adds a blast in front of Deathwing on top of the melee hitbox, so the combo ends
        /// on something that clears a crowd rather than only what fits inside the claw.
        /// </summary>
        private void Slam()
        {
            if (!isAuthority || !characterDirection)
            {
                return;
            }

            Vector3 impact = GroundPosition(transform.position + characterDirection.forward * (2.4f * characterScale));
            BlastAttack blast = CreateFireBlast(impact, finisherBlastRadius * characterScale, DamageCoefficient * 0.5f, finisherBlastForce);
            blast.bonusForce = Vector3.up * (finisherBlastForce * 0.35f);
            blast.Fire();
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
