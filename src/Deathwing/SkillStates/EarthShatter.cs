using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His E in World Breaker form. He slams the ground and two fissures tear away from him, stunning
    /// everything they run under. Where Onslaught closes a gap, this one holds a line open.
    /// </summary>
    public class EarthShatter : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 0.75f;
        public static float travelDuration = 0.4f;
        public static float recoveryDuration = 0.4f;
        /// <summary>Angle either side of his aim that the two fissures run along.</summary>
        public static float fissureSpread = 14f;
        public static int segmentsPerFissure = 5;
        public static float fissureLength = 45f;
        public static float segmentRadius = 6f;
        public static float force = 1200f;

        private float windupDuration;
        private Vector3 aimDirection;
        private int segmentsFired;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;

            aimDirection = GetAimRay().direction;
            aimDirection.y = 0f;
            aimDirection = aimDirection.normalized;

            if (characterDirection)
            {
                characterDirection.forward = aimDirection;
            }

            characterBody.SetAimTimer(windupDuration + travelDuration);
            Util.PlaySound(Sounds.cataclysmChannel, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.9f);
            PlayDragonAnimation(DeathwingClips.cataclysm, windupDuration, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor && fixedAge < windupDuration)
            {
                characterMotor.velocity = new Vector3(0f, characterMotor.velocity.y, 0f);
                characterMotor.moveDirection = Vector3.zero;
            }

            if (fixedAge >= windupDuration)
            {
                // The fissures run outwards over time rather than landing all at once, so they read as
                // cracks racing away from him.
                float progress = Mathf.Clamp01((fixedAge - windupDuration) / travelDuration);
                int wanted = Mathf.Min(segmentsPerFissure, Mathf.FloorToInt(progress * segmentsPerFissure) + 1);
                while (segmentsFired < wanted)
                {
                    FireSegment(segmentsFired);
                    segmentsFired++;
                }
            }

            if (isAuthority && fixedAge >= windupDuration + travelDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void FireSegment(int index)
        {
            float distance = fissureLength * (index + 1) / segmentsPerFissure;
            Vector3 origin = characterBody ? characterBody.footPosition : transform.position;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 direction = Quaternion.Euler(0f, fissureSpread * side, 0f) * aimDirection;
                Vector3 point = GroundPosition(origin + direction * distance, 60f);

                DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, point, 2.6f, gameObject);

                if (!isAuthority)
                {
                    continue;
                }

                BlastAttack blast = CreateFireBlast(
                    point,
                    segmentRadius * characterScale,
                    Tuning.earthShatterDamageCoefficient.Value / segmentsPerFissure,
                    force);
                // Stun stands in for his 0.75s stun; Risk of Rain 2's own is a flat second.
                blast.damageType = DamageType.IgniteOnHit | DamageType.Stun1s;
                blast.Fire();
            }

            if (index == 0)
            {
                Util.PlaySound(Sounds.cataclysmErupt, gameObject);
                DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 1f, false, 100f);
                ShakeCamera(origin, 7f, 0.5f, fissureLength + 30f);
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(aimDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            aimDirection = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
