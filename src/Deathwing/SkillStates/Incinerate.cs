using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His W in Destroyer form. He rears up and beats the air alight around himself: a short telegraph,
    /// then one heavy burst of flame on everything in reach. It is his answer to being surrounded, which
    /// is where a slow dragon usually ends up.
    /// </summary>
    public class Incinerate : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 0.75f;
        public static float recoveryDuration = 0.35f;
        public static float radius = 13f;
        public static float force = 2200f;

        private float windupDuration;
        private bool fired;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;

            characterBody.SetAimTimer(windupDuration + recoveryDuration);
            Util.PlaySound(Sounds.chargeStart, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.8f);
            PlayDragonAnimation(DeathwingClips.slam, (windupDuration + recoveryDuration) * 1.1f, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");
            dragonModel?.Silhouette(windupDuration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!fired && fixedAge >= windupDuration)
            {
                fired = true;
                Burn();
            }

            if (isAuthority && fixedAge >= windupDuration + recoveryDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Burn()
        {
            Vector3 center = characterBody ? characterBody.corePosition : transform.position;
            float scaledRadius = radius * characterScale;

            Util.PlaySound(Sounds.cataclysmErupt, gameObject);
            DeathwingAssets.SpawnEffect(DeathwingAssets.explosionEffect, center, 2.5f * characterScale, gameObject);
            Util.PlaySound(Sounds.breathStart, gameObject);

            if (!isAuthority)
            {
                return;
            }

            // As in Heroes: a wall of flame racing out from him to the edge of the area, and the ground
            // inside it left burning. The shockwave carries the camera kick for anyone nearby.
            Vector3 ground = GroundPosition(center);
            DeathwingEffects.SpawnShockwave(ground, scaledRadius, 5f, gameObject, true);
            DeathwingEffects.SpawnGroundFire(ground, scaledRadius * 0.85f, 2.5f, gameObject);

            BlastAttack blast = CreateFireBlast(
                center, scaledRadius, Tuning.incinerateDamageCoefficient.Value, force);
            blast.bonusForce = Vector3.up * (force * 0.4f);
            blast.Fire();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
