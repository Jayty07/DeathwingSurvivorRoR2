using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His W in World Breaker form. A second after he calls it, the ground where he aimed bursts open,
    /// hitting for the impact and then leaving a pool of lava that burns and slows whatever stands in it.
    /// The delay is the trade: it is his zoning tool, not his panic button.
    /// </summary>
    public class LavaBurst : BaseDeathwingSkillState
    {
        public static float baseDelayDuration = 1f;
        public static float recoveryDuration = 0.4f;
        public static float range = 70f;
        public static float impactRadius = 9f;
        public static float force = 1600f;

        private float delayDuration;
        private Vector3 target;
        private bool erupted;

        public override void OnEnter()
        {
            base.OnEnter();
            delayDuration = baseDelayDuration / attackSpeedStat;
            target = AimedGroundPoint();

            characterBody.SetAimTimer(delayDuration + recoveryDuration);
            Util.PlaySound(Sounds.boulderWindup, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.7f);
            PlayDragonAnimation(DeathwingClips.boulder, delayDuration, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");

            // The ground cracks for the second before it opens, so it can be stepped out of.
            DeathwingAssets.SpawnEffect(DeathwingAssets.emberEffect, target, 3f, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!erupted && fixedAge >= delayDuration)
            {
                erupted = true;
                Erupt();
            }

            if (isAuthority && fixedAge >= delayDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        /// <summary>Where he is looking, dropped onto the ground; his own reach if he aims at the sky.</summary>
        private Vector3 AimedGroundPoint()
        {
            Ray aimRay = GetAimRay();
            if (Physics.Raycast(aimRay, out RaycastHit hit, range, LayerIndex.world.mask))
            {
                return hit.point;
            }

            return GroundPosition(aimRay.GetPoint(range), 200f);
        }

        private void Erupt()
        {
            Util.PlaySound(Sounds.cataclysmErupt, gameObject);
            DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, target, 4f, gameObject);
            ShakeCamera(target, 6f, 0.4f, impactRadius + 40f);

            if (!isAuthority)
            {
                return;
            }

            BlastAttack blast = CreateFireBlast(
                target, impactRadius * characterScale, Tuning.lavaBurstDamageCoefficient.Value, force);
            // Whatever the burst catches leaves it wading, which is the point of the pool it lands in.
            blast.damageType = DamageType.IgniteOnHit | DamageType.SlowOnHit;
            blast.bonusForce = Vector3.up * (force * 0.5f);
            blast.Fire();

            if (Projectiles.lavaPool)
            {
                ProjectileManager.instance.FireProjectile(
                    Projectiles.lavaPool,
                    target,
                    Quaternion.identity,
                    gameObject,
                    damageStat * Tuning.lavaBurstDamageCoefficient.Value * Projectiles.lavaPoolDamageCoefficient,
                    0f,
                    RollCrit(),
                    DamageColorIndex.Item);
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(target);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            target = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
