using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His heroic. Deathwing rears up and calls the ground open in a long lane ahead of him, the way he
    /// scorches a path down the battlefield in Heroes of the Storm. The lane is shown for the whole
    /// two-second channel, then erupts away from him in a run of fissures and stays burning behind them.
    /// </summary>
    public class Cataclysm : BaseDeathwingSkillState
    {
        /// <summary>His own two seconds between the call and the ground opening.</summary>
        public static float baseChannelDuration = 2f;
        public static float baseEruptionDuration = 1.4f;
        public static float recoveryDuration = 0.6f;
        public static float laneLength = 60f;
        public static float laneWidth = 16f;
        public static int segments = 8;
        public static float segmentForce = 2600f;
        public static float shakeMagnitude = 8f;

        private float channelDuration;
        private float eruptionDuration;
        private Vector3 aimDirection;
        private Vector3 origin;
        private int segmentsFired;

        private bool IsChannelling => fixedAge < channelDuration;

        private float HalfWidth => laneWidth * 0.5f * Mathf.Max(1f, characterScale * 0.6f);

        public override void OnEnter()
        {
            base.OnEnter();
            // Flat, not scaled by attack speed: his figure, and the telegraph is the point.
            channelDuration = baseChannelDuration;
            eruptionDuration = baseEruptionDuration / attackSpeedStat;

            aimDirection = GetAimRay().direction;
            aimDirection.y = 0f;
            aimDirection = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : transform.forward;
            if (characterDirection)
            {
                characterDirection.forward = aimDirection;
            }

            origin = GroundPosition(characterBody ? characterBody.footPosition : transform.position);

            characterBody.AddTimedBuff(Buffs.elementiumPlating, channelDuration + eruptionDuration);
            characterBody.SetAimTimer(channelDuration + eruptionDuration);
            Util.PlaySound(Sounds.cataclysmChannel, gameObject);
            DeathwingVoice.Play(DeathwingVoice.bellowingRoar, gameObject, 1f, false, 120f);
            PlayDragonAnimation(DeathwingClips.cataclysmChannel, channelDuration,
                "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
            dragonModel?.Silhouette(channelDuration);
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, transform.position, 4f * characterScale, gameObject);

            DeathwingEffects.Telegraph(GroundPosition(origin + aimDirection * 3f), HalfWidth, channelDuration,
                aimDirection, laneLength);
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
                float progress = Mathf.Clamp01((fixedAge - channelDuration) / eruptionDuration);
                int wanted = Mathf.Min(segments, Mathf.FloorToInt(progress * segments) + 1);
                while (segmentsFired < wanted)
                {
                    FireSegment(segmentsFired);
                    segmentsFired++;
                }
            }

            if (isAuthority && fixedAge >= channelDuration + eruptionDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void FireSegment(int index)
        {
            float step = laneLength / segments;
            float distance = step * (index + 0.5f);
            Vector3 center = GroundPosition(origin + aimDirection * distance, 60f);
            float halfWidth = HalfWidth;

            Util.PlaySound(Sounds.cataclysmErupt, gameObject);
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 1f, false, 140f);
            DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, center, 4f, gameObject);

            // Two side fissures per segment so the lane reads as a torn strip rather than a line of pits.
            Vector3 across = Vector3.Cross(Vector3.up, aimDirection);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 edge = GroundPosition(center + across * (halfWidth * 0.6f * side), 60f);
                DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, edge, 2.5f, gameObject);
            }

            if (index == 0)
            {
                ShakeCamera(center, shakeMagnitude, 0.6f, laneLength + 40f);
            }

            if (!isAuthority)
            {
                return;
            }

            DeathwingEffects.SpawnShockwave(center, halfWidth * 1.2f, 3f, gameObject, true);
            DeathwingEffects.SpawnGroundFire(center, halfWidth, 6f, gameObject, aimDirection, step);

            if (Projectiles.lavaPool)
            {
                ProjectileManager.instance.FireProjectile(
                    Projectiles.lavaPool,
                    center,
                    Quaternion.identity,
                    gameObject,
                    damageStat * Projectiles.lavaPoolDamageCoefficient,
                    0f,
                    RollCrit(),
                    DamageColorIndex.Item);
            }

            // Each segment is its own blast covering its stretch of the lane; a segment radius a little
            // over the half-width leaves no seams between neighbours.
            BlastAttack blast = CreateFireBlast(center, Mathf.Max(halfWidth, step * 0.75f),
                Tuning.heroicCataclysmDamageCoefficient.Value, segmentForce);
            blast.bonusForce = Vector3.up * (segmentForce * 0.5f);
            blast.Fire();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
