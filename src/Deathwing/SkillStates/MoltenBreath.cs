using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Secondary variant: a sustained cone of dragonfire. Held down, it ticks damage into everything in
    /// front of Deathwing and ignites it, but roots him to a crawl while he breathes.
    /// </summary>
    public class MoltenBreath : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 0.4f;
        public static float baseMaxDuration = 3.5f;
        public static float tickInterval = 0.2f;
        public static float range = 26f;
        public static float coneHalfAngle = 22f;
        public static float moveSpeedMultiplier = 0.35f;
        public static float force = 250f;

        private float windupDuration;
        private float maxDuration;
        private float tickStopwatch;
        private bool windupFinished;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;
            maxDuration = baseMaxDuration;

            StartAimMode(maxDuration + 1f);
            characterBody.SetAimTimer(maxDuration + 1f);
            Util.PlaySound(Sounds.breathStart, gameObject);
            PlayCrossfade("Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate", windupDuration, 0.1f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterBody)
            {
                characterBody.isSprinting = false;
            }

            if (characterMotor && characterMotor.isGrounded)
            {
                // Breathing fire is a commitment: he can reposition, but only barely.
                characterMotor.moveDirection *= moveSpeedMultiplier;
            }

            if (fixedAge < windupDuration)
            {
                return;
            }

            if (!windupFinished)
            {
                windupFinished = true;
                Util.PlaySound(Sounds.breathLoop, gameObject);
            }

            tickStopwatch += GetDeltaTime();
            if (tickStopwatch >= tickInterval)
            {
                tickStopwatch = 0f;
                Breathe();
            }

            if (!isAuthority)
            {
                return;
            }

            bool released = !IsKeyDownAuthority();
            if (released || fixedAge >= windupDuration + maxDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        /// <summary>
        /// One tick of the cone. Each tick is a short-range blast walked along the aim ray, which keeps
        /// the flame shaped like a cone without needing a custom collider or projectile.
        /// </summary>
        private void Breathe()
        {
            Ray aimRay = GetAimRay();
            Vector3 mouth = characterBody.corePosition + Vector3.up * (1.2f * characterScale);

            DeathwingAssets.SpawnEffect(
                DeathwingAssets.fireImpactEffect,
                mouth + aimRay.direction * (range * 0.35f),
                1.6f * characterScale,
                gameObject);

            if (!isAuthority)
            {
                return;
            }

            float tickCoefficient = Tuning.breathDamageCoefficient.Value * tickInterval;

            // Three overlapping blasts walked along the aim ray approximate a cone: each is placed
            // further out and widened by the cone's angle, which avoids needing a custom collider.
            for (int i = 1; i <= 3; i++)
            {
                float distance = range * i / 3f;
                Vector3 position = mouth + aimRay.direction * distance;
                float radius = Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * distance;

                BlastAttack blast = CreateFireBlast(position, radius, tickCoefficient / 3f, force);
                blast.procCoefficient = tickInterval;
                blast.losType = BlastAttack.LoSType.NearestHit;
                blast.Fire();
            }
        }

        public override void OnExit()
        {
            Util.PlaySound(Sounds.breathStop, gameObject);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
