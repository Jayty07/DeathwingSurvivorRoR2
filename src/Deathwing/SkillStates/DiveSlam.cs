using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Ends flight by folding the wings and dropping like a meteor. Landing cracks the ground open for
    /// a blast whose radius grows with the height of the dive.
    /// </summary>
    public class DiveSlam : BaseDeathwingSkillState
    {
        public static float diveSpeed = 55f;
        public static float maxDiveDuration = 3f;
        public static float landingDuration = 0.5f;
        public static float baseBlastRadius = 12f;
        public static float blastRadiusPerMeterFallen = 0.35f;
        public static float maxBlastRadius = 26f;
        public static float damageCoefficient = 7f;
        public static float blastForce = 4000f;
        public static int lavaPoolCount = 3;

        private float startHeight;
        private bool hasLanded;
        private float landingStopwatch;

        public override void OnEnter()
        {
            base.OnEnter();
            startHeight = transform.position.y;

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.velocity = Vector3.down * diveSpeed;
            }

            characterBody.AddTimedBuff(Buffs.elementiumPlating, maxDiveDuration + landingDuration);
            Util.PlaySound(Sounds.diveStart, gameObject);
            PlayCrossfade("Body", "Fall", 0.1f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!hasLanded)
            {
                if (characterMotor)
                {
                    // A steerable dive would make this a movement skill; it is meant to be a commitment.
                    characterMotor.velocity = Vector3.down * diveSpeed;
                }

                bool grounded = characterMotor && characterMotor.isGrounded;
                if (grounded || fixedAge >= maxDiveDuration)
                {
                    Land();
                }

                return;
            }

            landingStopwatch += GetDeltaTime();
            if (characterMotor)
            {
                characterMotor.velocity = Vector3.zero;
            }

            if (isAuthority && landingStopwatch >= landingDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Land()
        {
            hasLanded = true;

            if (characterMotor)
            {
                characterMotor.useGravity = true;
                characterMotor.velocity = Vector3.zero;
            }

            float metersFallen = Mathf.Max(0f, startHeight - transform.position.y);
            float radius = Mathf.Min(maxBlastRadius, baseBlastRadius + metersFallen * blastRadiusPerMeterFallen);
            Vector3 impact = GroundPosition(transform.position);

            Util.PlaySound(Sounds.diveImpact, gameObject);
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, impact, radius * 0.45f, gameObject);
            SpawnFireEffect(impact, radius * 0.4f);
            ShakeCamera(impact, 12f, 0.8f, radius + 60f);

            if (!isAuthority)
            {
                return;
            }

            BlastAttack blast = CreateFireBlast(impact, radius, damageCoefficient, blastForce);
            blast.bonusForce = Vector3.up * (blastForce * 0.4f);
            blast.Fire();

            if (!Projectiles.lavaPool)
            {
                return;
            }

            for (int i = 0; i < lavaPoolCount; i++)
            {
                float angle = 360f / lavaPoolCount * i;
                Vector3 position = GroundPosition(impact + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius * 0.5f));
                ProjectileManager.instance.FireProjectile(
                    Projectiles.lavaPool,
                    position,
                    Quaternion.identity,
                    gameObject,
                    damageStat * Projectiles.lavaPoolDamageCoefficient,
                    0f,
                    RollCrit(),
                    DamageColorIndex.Item);
            }
        }

        public override void OnExit()
        {
            if (characterMotor)
            {
                characterMotor.useGravity = true;
            }

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
