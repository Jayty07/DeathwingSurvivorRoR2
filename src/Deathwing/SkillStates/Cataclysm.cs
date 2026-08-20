using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Special: Deathwing roots himself, then splits the ground open in expanding rings of fissures.
    /// Each ring is its own blast, so enemies caught close take every ring.
    /// </summary>
    public class Cataclysm : BaseDeathwingSkillState
    {
        public static float baseChannelDuration = 1.4f;
        public static float baseEruptionDuration = 1.2f;
        public static float recoveryDuration = 0.6f;
        public static int ringCount = 3;
        public static float firstRingRadius = 9f;
        public static float ringRadiusStep = 8f;
        public static int fissuresPerRing = 6;
        public static float ringForce = 2600f;
        public static float ringShakeMagnitude = 8f;

        private float channelDuration;
        private float eruptionDuration;
        private int ringsFired;

        private bool IsChannelling => fixedAge < channelDuration;

        public override void OnEnter()
        {
            base.OnEnter();
            channelDuration = baseChannelDuration / attackSpeedStat;
            eruptionDuration = baseEruptionDuration / attackSpeedStat;

            characterBody.AddTimedBuff(Buffs.elementiumPlating, channelDuration + eruptionDuration);
            Util.PlaySound(Sounds.cataclysmChannel, gameObject);
            PlayDragonAnimation(DeathwingClips.cataclysm, channelDuration,
                "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, transform.position, 4f * characterScale, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor && IsChannelling)
            {
                // Rooted while channelling: this is the payoff for a very telegraphed skill.
                characterMotor.velocity = new Vector3(0f, characterMotor.velocity.y, 0f);
                characterMotor.moveDirection = Vector3.zero;
            }

            if (!IsChannelling)
            {
                float eruptionProgress = Mathf.Clamp01((fixedAge - channelDuration) / eruptionDuration);
                int ringsWanted = Mathf.Min(ringCount, Mathf.FloorToInt(eruptionProgress * ringCount) + 1);
                while (ringsFired < ringsWanted)
                {
                    FireRing(ringsFired);
                    ringsFired++;
                }
            }

            if (isAuthority && fixedAge >= channelDuration + eruptionDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void FireRing(int ringIndex)
        {
            float radius = firstRingRadius + ringRadiusStep * ringIndex;
            Vector3 center = GroundPosition(transform.position);

            Util.PlaySound(Sounds.cataclysmErupt, gameObject);
            ShakeCamera(center, ringShakeMagnitude, 0.6f, radius + 40f);

            for (int i = 0; i < fissuresPerRing; i++)
            {
                float angle = (360f / fissuresPerRing) * i + ringIndex * 18f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius);
                Vector3 fissure = GroundPosition(center + offset);
                DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, fissure, 3.5f, gameObject);

                // Every fissure leaves burning ground, so the whole area stays denied after the rings
                // have gone off rather than only the outermost one.
                if (isAuthority && Projectiles.lavaPool)
                {
                    ProjectileManager.instance.FireProjectile(
                        Projectiles.lavaPool,
                        fissure,
                        Quaternion.identity,
                        gameObject,
                        damageStat * Projectiles.lavaPoolDamageCoefficient,
                        0f,
                        RollCrit(),
                        DamageColorIndex.Item);
                }
            }

            if (!isAuthority)
            {
                return;
            }

            // Ring damage is modelled as a filled blast at the ring radius; the inner rings already
            // covered the ground closer to Deathwing.
            BlastAttack blast = CreateFireBlast(center, radius, Tuning.cataclysmDamageCoefficient.Value, ringForce);
            blast.bonusForce = Vector3.up * (ringForce * 0.5f);
            blast.Fire();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
