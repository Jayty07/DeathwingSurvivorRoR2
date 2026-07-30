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
        public static int visualSteps = 7;
        public static float visualStartDistance = 1.5f;
        public static int damageSteps = 5;
        public static float nearDamageDistance = 5f;

        /// <summary>Model transforms the jet is emitted from, in order of preference.</summary>
        private static readonly string[] muzzleNames = { "MuzzleCenter", "MuzzleGun", "MuzzleLeft", "HeadCenter", "Head" };

        private float windupDuration;
        private float maxDuration;
        private float tickStopwatch;
        private bool windupFinished;
        private Transform mouthTransform;
        private bool mouthSearched;

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
        /// The point the flame leaves. The chassis' muzzle transforms sit on the model, so the jet
        /// follows it wherever the model actually is instead of floating out in front of the capsule.
        /// </summary>
        private Vector3 Mouth()
        {
            if (mouthTransform)
            {
                return mouthTransform.position;
            }

            // Resolved once: the lookup walks the model's child locator by name, and the breath asks for
            // the mouth several times a second for as long as it is held.
            if (!mouthSearched)
            {
                mouthSearched = true;
                foreach (string childName in muzzleNames)
                {
                    mouthTransform = FindModelChild(childName);
                    if (mouthTransform)
                    {
                        return mouthTransform.position;
                    }
                }
            }

            return characterBody.corePosition + Vector3.up * (0.6f * characterScale);
        }

        /// <summary>
        /// One tick of the cone. Each tick is a row of short-range blasts walked along the aim ray, which
        /// keeps the flame shaped like a cone without needing a custom collider or projectile.
        /// </summary>
        private void Breathe()
        {
            Ray aimRay = GetAimRay();
            Vector3 mouth = Mouth();

            // The flame is drawn as a row of blasts from the mouth outwards, so it reads as a continuous
            // jet: stepping evenly across the full range left a gap between Deathwing and the first puff
            // that made the fire look like it started in mid-air.
            for (int i = 0; i < visualSteps; i++)
            {
                float t = (float)i / (visualSteps - 1);
                float distance = Mathf.Lerp(visualStartDistance * characterScale, range, t * t);
                DeathwingAssets.SpawnEffect(
                    DeathwingAssets.fireImpactEffect,
                    mouth + aimRay.direction * distance,
                    Mathf.Lerp(0.8f, 3f, t) * characterScale,
                    gameObject);
            }

            if (!isAuthority)
            {
                return;
            }

            float tickCoefficient = Tuning.breathDamageCoefficient.Value * tickInterval;

            // Overlapping blasts walked along the aim ray approximate a cone: each is placed further
            // out and widened by the cone's angle, which avoids needing a custom collider.
            // The first blast sits close to the mouth so a target in his face is still burned; spacing
            // them evenly across a 55m range left everything nearby untouched.
            for (int i = 0; i < damageSteps; i++)
            {
                float distance = Mathf.Lerp(nearDamageDistance, range, (float)i / (damageSteps - 1));
                Vector3 position = mouth + aimRay.direction * distance;
                float radius = Mathf.Max(3f, Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * distance);

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
