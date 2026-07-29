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
        public static float range = 55f;
        public static float coneHalfAngle = 18f;
        public static float moveSpeedMultiplier = 0.35f;
        public static float force = 250f;
        public static int visualSteps = 4;
        public static int damageSteps = 4;

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

            // The flame is drawn as a row of blasts down the aim ray so its visual reaches as far as its
            // damage does; a single effect at one distance read as a short puff.
            for (int i = 1; i <= visualSteps; i++)
            {
                float distance = range * i / visualSteps;
                DeathwingAssets.SpawnEffect(
                    DeathwingAssets.fireImpactEffect,
                    mouth + aimRay.direction * distance,
                    Mathf.Lerp(1.2f, 3f, (float)i / visualSteps) * characterScale,
                    gameObject);
            }

            if (!isAuthority)
            {
                return;
            }

            float tickCoefficient = Tuning.breathDamageCoefficient.Value * tickInterval;

            // Overlapping blasts walked along the aim ray approximate a cone: each is placed further
            // out and widened by the cone's angle, which avoids needing a custom collider.
            for (int i = 1; i <= damageSteps; i++)
            {
                float distance = range * i / damageSteps;
                Vector3 position = mouth + aimRay.direction * distance;
                float radius = Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * distance;

                BlastAttack blast = CreateFireBlast(position, radius, tickCoefficient / damageSteps, force);
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
