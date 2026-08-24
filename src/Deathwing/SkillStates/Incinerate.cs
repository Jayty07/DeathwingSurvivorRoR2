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
        public static int flameCount = 8;

        private float windupDuration;
        private bool fired;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;

            characterBody.SetAimTimer(windupDuration + recoveryDuration);
            Util.PlaySound(Sounds.chargeStart, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.8f);
            PlayDragonAnimation(DeathwingClips.chargeStart, windupDuration, "Gesture, Override", "ThrowGrenade",
                "ThrowGrenade.playbackRate");
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
            ShakeCamera(center, 5f, 0.35f, scaledRadius + 30f);

            // A ring of flame at his own feet, so the burst reads as the ground going up rather than as
            // one explosion sitting inside him.
            for (int i = 0; i < flameCount; i++)
            {
                float angle = 360f / flameCount * i;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * (scaledRadius * 0.7f));
                DeathwingAssets.SpawnEffect(
                    DeathwingAssets.fireImpactEffect, GroundPosition(center + offset), 2.4f, gameObject);
            }

            if (!isAuthority)
            {
                return;
            }

            BlastAttack blast = CreateFireBlast(
                center, scaledRadius, Tuning.incinerateDamageCoefficient.Value, force);
            blast.bonusForce = Vector3.up * (force * 0.4f);
            blast.Fire();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
