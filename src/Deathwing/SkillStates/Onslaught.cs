using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His E in Destroyer form. He gathers himself for a moment, lunges, and finishes with a bite at the
    /// end of the lunge that hits far harder than the charge through it and leaves what survives slowed.
    /// Distance is the reward: the bite is where the damage is, so it wants to be aimed, not mashed.
    /// </summary>
    public class Onslaught : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 0.5f;
        public static float baseLungeDuration = 0.55f;
        public static float recoveryDuration = 0.35f;
        public static float speedMultiplier = 5.5f;
        public static float trampleForce = 2400f;
        public static float biteRadius = 8f;
        public static float biteForce = 3000f;

        private float windupDuration;
        private float lungeDuration;
        private Vector3 lungeDirection;
        private OverlapAttack attack;
        private bool bitten;

        private bool IsLunging => fixedAge >= windupDuration && fixedAge < windupDuration + lungeDuration;
        private ParticleSystem trail;
        private ParticleSystem trailSmoke;
        private float nextScorch;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;
            lungeDuration = baseLungeDuration;

            lungeDirection = GetAimRay().direction;
            lungeDirection.y = 0f;
            lungeDirection = lungeDirection.normalized;

            if (characterDirection)
            {
                characterDirection.forward = lungeDirection;
                characterDirection.moveVector = lungeDirection;
            }

            attack = new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                damage = Tuning.onslaughtDamageCoefficient.Value * damageStat,
                procCoefficient = 1f,
                hitEffectPrefab = DeathwingAssets.fireImpactEffect,
                pushAwayForce = trampleForce,
                hitBoxGroup = FindHitBoxGroup(DeathwingBody.chargeHitBoxGroupName),
                isCrit = RollCrit(),
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.IgniteOnHit
            };

            Util.PlaySound(Sounds.chargeStart, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.85f);
            PlayDragonAnimation(DeathwingClips.chargeStart, windupDuration, "Body", "Sprint");
            dragonModel?.Silhouette(windupDuration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (fixedAge < windupDuration)
            {
                // Coiled: he does not slide about while he is winding up.
                if (characterMotor)
                {
                    characterMotor.velocity = new Vector3(0f, characterMotor.velocity.y, 0f);
                    characterMotor.moveDirection = Vector3.zero;
                }

                return;
            }

            if (IsLunging)
            {
                if (characterMotor)
                {
                    characterMotor.velocity = lungeDirection * (moveSpeedStat * speedMultiplier);
                }

                if (characterDirection)
                {
                    characterDirection.forward = lungeDirection;
                }

                Trail();

                if (isAuthority)
                {
                    attack?.Fire();

                    // Burning ground left behind the lunge, a patch every half body length.
                    if (fixedAge >= nextScorch)
                    {
                        nextScorch = fixedAge + 0.12f;
                        DeathwingEffects.SpawnGroundFire(
                            GroundPosition(characterBody.footPosition), 2.2f * characterScale, 1.8f, gameObject);
                    }
                }
            }
            else if (!bitten)
            {
                bitten = true;
                Bite();
            }

            if (isAuthority && fixedAge >= windupDuration + lungeDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Bite()
        {
            Vector3 mouth = (characterBody ? characterBody.corePosition : transform.position)
                + lungeDirection * (2f * characterScale);

            Util.PlaySound(Sounds.clawSlam, gameObject);
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 0.7f, false, 90f);
            DeathwingAssets.SpawnEffect(DeathwingAssets.explosionEffect, mouth, 2f * characterScale, gameObject);
            EndTrail();

            if (isAuthority)
            {
                // The bite lands like a hammer: dust and stone thrown out in front of him.
                DeathwingEffects.SpawnShockwave(GroundPosition(mouth), biteRadius * characterScale * 1.4f, 6f, gameObject);
            }

            if (characterMotor)
            {
                characterMotor.velocity *= 0.25f;
            }

            if (!isAuthority)
            {
                return;
            }

            BlastAttack blast = CreateFireBlast(
                mouth, biteRadius * characterScale, Tuning.onslaughtBiteDamageCoefficient.Value, biteForce);
            blast.damageType = DamageType.IgniteOnHit | DamageType.SlowOnHit;
            blast.Fire();
        }

        /// <summary>Fire and smoke streaming off him for the length of the lunge, on every client.</summary>
        private void Trail()
        {
            if (trail || !characterBody)
            {
                return;
            }

            float size = characterBody.radius * 2f;
            trail = DeathwingEffects.AttachFlames(characterBody.transform, size, 0f, 1.6f);
            trailSmoke = DeathwingEffects.AttachSmoke(characterBody.transform, size, 0f, 24f);
        }

        private void EndTrail()
        {
            if (trail)
            {
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Object.Destroy(trail.gameObject, 2f);
                trail = null;
            }

            if (trailSmoke)
            {
                trailSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Object.Destroy(trailSmoke.gameObject, 4f);
                trailSmoke = null;
            }
        }

        public override void OnExit()
        {
            EndTrail();
            base.OnExit();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(lungeDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            lungeDirection = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
