using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Utility: Deathwing's only mobility. A committed forward charge that trades steering for heavy
    /// armor, trampling anything in the way once each.
    /// </summary>
    public class ElementiumCharge : BaseDeathwingSkillState
    {
        public static float baseDuration = 1.3f;
        public static float speedMultiplier = 4.2f;
        public static float trampleForce = 3500f;
        public static float scorchInterval = 0.18f;

        private float duration;
        private Vector3 chargeDirection;
        private OverlapAttack attack;
        private float scorchStopwatch;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration;

            chargeDirection = (inputBank && inputBank.moveVector.sqrMagnitude > 0.01f)
                ? inputBank.moveVector.normalized
                : GetAimRay().direction;
            chargeDirection.y = 0f;
            chargeDirection = chargeDirection.normalized;

            if (characterDirection)
            {
                characterDirection.forward = chargeDirection;
                // Steering during the charge would defeat the point of a committed dash.
                characterDirection.moveVector = chargeDirection;
            }

            attack = new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                damage = Tuning.chargeDamageCoefficient.Value * damageStat,
                procCoefficient = 1f,
                hitEffectPrefab = DeathwingAssets.fireImpactEffect,
                pushAwayForce = trampleForce,
                hitBoxGroup = FindHitBoxGroup(DeathwingBody.chargeHitBoxGroupName),
                isCrit = RollCrit(),
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.IgniteOnHit
            };

            characterBody.AddTimedBuff(Buffs.elementiumPlating, duration + 0.5f);
            Util.PlaySound(Sounds.chargeStart, gameObject);
            ShakeCamera(transform.position, 3f, 0.3f, 30f);
            PlayDragonAnimation(DeathwingClips.charge, duration, "Body", "Sprint");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor)
            {
                characterMotor.velocity = chargeDirection * (moveSpeedStat * speedMultiplier);
            }

            if (characterDirection)
            {
                characterDirection.forward = chargeDirection;
            }

            scorchStopwatch += GetDeltaTime();
            if (scorchStopwatch >= scorchInterval)
            {
                scorchStopwatch = 0f;
                DeathwingAssets.SpawnEffect(DeathwingAssets.fireImpactEffect, GroundPosition(transform.position), 1.6f * characterScale, gameObject);
                ShakeCamera(transform.position, 1.2f, scorchInterval, 25f);
            }

            if (isAuthority)
            {
                attack?.Fire();

                if (fixedAge >= duration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }

        public override void OnExit()
        {
            if (characterMotor)
            {
                // Bleed off the charge so it does not launch Deathwing across the arena.
                characterMotor.velocity *= 0.35f;
            }

            base.OnExit();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(chargeDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            chargeDirection = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
