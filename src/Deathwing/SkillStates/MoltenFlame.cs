using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His Q. A sustained jet of dragonfire that ticks eight times a second into everything in front of
    /// him and ignites it, at the cost of rooting him to a crawl while it burns. In Heroes of the Storm
    /// the flame is paid for out of Energy; here it is a channel with a duration and a short cooldown,
    /// since a resource bar would need a custom HUD.
    /// </summary>
    public class MoltenFlame : BaseDeathwingSkillState
    {
        public static float baseWindupDuration = 0.6f;
        public static float baseMaxDuration = 4f;
        /// <summary>His own cadence: damage every eighth of a second.</summary>
        public static float tickInterval = 0.125f;
        public static float range = 55f;
        public static float coneHalfAngle = 18f;
        public static float moveSpeedMultiplier = 0.15f;
        public static float force = 250f;
        public static int visualSteps = 7;
        public static float visualStartDistance = 1.5f;
        public static int damageSteps = 5;
        public static float nearDamageDistance = 5f;
        /// <summary>How long the jet is left burning after the breath ends, so it dies down.</summary>
        public static float jetFadeDuration = 0.4f;

        /// <summary>
        /// Model transforms the jet is emitted from, in order of preference. The dragon's own jaw comes
        /// first; the rest are the chassis' muzzles, used while the placeholder model is in play.
        /// </summary>
        private static readonly string[] muzzleNames =
        {
            DeathwingBody.mouthChildName, "MuzzleCenter", "MuzzleGun", "MuzzleLeft", "HeadCenter", "Head"
        };

        private float windupDuration;
        private float maxDuration;
        private float tickStopwatch;
        private bool windupFinished;
        private Transform mouthTransform;
        private bool mouthSearched;
        private GameObject jet;
        private bool jetSpawned;
        private DeathwingVoice.Voice voice;

        public override void OnEnter()
        {
            base.OnEnter();
            windupDuration = baseWindupDuration / attackSpeedStat;
            maxDuration = baseMaxDuration;

            StartAimMode(maxDuration + 1f);
            characterBody.SetAimTimer(maxDuration + 1f);
            Util.PlaySound(Sounds.breathStart, gameObject);
            DeathwingVoice.Play(DeathwingVoice.flameBreath, gameObject);
            PlayDragonAnimation(DeathwingClips.breathStart, windupDuration,
                "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
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
                voice = DeathwingVoice.Play(DeathwingVoice.flameLoop, gameObject, 0.85f, true);
                StartJet();

                // Jaw held open for as long as the flame lasts.
                if (dragon)
                {
                    dragon.PlayHeld(DeathwingClips.breathLoop, 0.15f);
                }
            }

            AimJet();

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

            // Only drawn as a row of puffs when the borrowed jet could not be resolved: the jet is a single
            // continuous effect, so puffing impact effects along it as well just muddies it. Keyed off having
            // spawned it rather than off the live instance, so losing the instance does not bring them back.
            if (!jetSpawned)
            {
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
            }

            if (!isAuthority)
            {
                return;
            }

            float tickCoefficient = Tuning.moltenFlameDamageCoefficient.Value * tickInterval;

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

        /// <summary>
        /// Borrows the flamethrower drone's jet rather than drawing the flame out of impact effects. It is a
        /// persistent effect rather than a one-shot, so it is instantiated once and steered every step; it is
        /// left unparented because the model's muzzle does not point where Deathwing is aiming.
        /// </summary>
        private void StartJet()
        {
            GameObject prefab = DeathwingAssets.FlamethrowerEffect(out float authoredDistance);
            if (!prefab)
            {
                return;
            }

            jetSpawned = true;
            jet = UnityEngine.Object.Instantiate(prefab, Mouth(), Quaternion.identity);

            // The drone only sprays in short bursts, so its effect carries timers that tore the jet down
            // partway through the breath; the fallback puffs then reappeared for the rest of it.
            foreach (DestroyOnTimer timer in jet.GetComponentsInChildren<DestroyOnTimer>(true))
            {
                UnityEngine.Object.Destroy(timer);
            }

            // Sized by the particle systems rather than by the transform: they simulate in world space, where
            // scaling the transform moves the emitter about but leaves the particles the size a drone sprays
            // them. Speed carries them further, which is what makes the jet reach.
            float length = range / authoredDistance;
            float width = characterScale * Tuning.breathWidth.Value;
            foreach (ParticleSystem system in jet.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startSizeMultiplier *= width;
                main.startSpeedMultiplier *= length;
                // Held for as long as Deathwing breathes, where the drone's burst runs out on its own.
                main.loop = true;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.radius *= width;
                shape.scale *= width;

                system.Play();
            }

            AimJet();
        }

        private void AimJet()
        {
            if (!jet)
            {
                return;
            }

            jet.transform.position = Mouth();
            jet.transform.rotation = Quaternion.LookRotation(GetAimRay().direction);
        }

        public override void OnExit()
        {
            Util.PlaySound(Sounds.breathStop, gameObject);
            DeathwingVoice.Stop(voice);

            if (dragon)
            {
                dragon.Release(DeathwingClips.breathEnd, 0.4f);
            }

            if (jet)
            {
                // Stops spraying but is given a moment to burn out rather than vanishing mid-frame.
                foreach (ParticleSystem system in jet.GetComponentsInChildren<ParticleSystem>(true))
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                UnityEngine.Object.Destroy(jet, jetFadeDuration);
            }

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
