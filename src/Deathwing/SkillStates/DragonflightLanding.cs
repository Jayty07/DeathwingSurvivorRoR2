using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// The end of a flight he chose to end himself. He is drawn again and dropped back to the ground
    /// with no say in where he goes. The landing clip is authored in place, so the descent it needs
    /// is his: once the ground is close enough it starts, and he is lowered the rest of the way over
    /// its length so its last frame finds him standing on the ground, rooted until it is done.
    /// </summary>
    public class DragonflightLanding : BaseDeathwingSkillState
    {
        public static float descentSpeed = 45f;
        /// <summary>Ceiling on the drop, so a flight that ends over a hole cannot strand the state.</summary>
        public static float maxDescentDuration = 8f;
        public static float minLandingDuration = 0.4f;
        /// <summary>
        /// The drop has to be under way before the ground counts. Stepping out of flight the motor is
        /// still reporting the last step it ran, so an immediate check lands him in the air.
        /// </summary>
        public static float minDescentDuration = 0.15f;
        /// <summary>How far under his feet counts as touching down, pre-scale; a descent step is 0.75m.</summary>
        public static float groundProbe = 0.8f;
        /// <summary>How far above the ground the landing clip begins, pre-scale.</summary>
        public static float approachHeight = 9f;

        private float landingDuration = minLandingDuration;
        private float landedAt;
        private bool landed;
        private bool grounded;

        public override void OnEnter()
        {
            base.OnEnter();

            // Visible again for the drop: he is hidden only while he is actually up there.
            SetModelHidden(false);

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.velocity = Vector3.down * descentSpeed;
            }

            if (dragon)
            {
                dragon.PlayHeld(DeathwingClips.airborne, 0.2f);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!landed)
            {
                if (characterMotor)
                {
                    characterMotor.velocity = Vector3.down * descentSpeed;
                    characterMotor.moveDirection = Vector3.zero;
                }

                if ((fixedAge >= minDescentDuration && HeightAboveGround(out float height)
                        && height <= approachHeight * Mathf.Max(characterScale, 1f))
                    || fixedAge >= maxDescentDuration)
                {
                    Land();
                }

                return;
            }

            float remaining = landingDuration - (fixedAge - landedAt);
            if (!grounded && (FeetOnGround(groundProbe) || remaining <= 0f))
            {
                grounded = true;
                if (characterMotor)
                {
                    characterMotor.useGravity = true;
                }

                Touchdown();
            }

            if (characterMotor)
            {
                characterMotor.moveDirection = Vector3.zero;
                if (grounded)
                {
                    characterMotor.velocity = Vector3.zero;
                }
                else
                {
                    // Lowered over what is left of the clip so its end and the ground arrive together.
                    float height = HeightAboveGround(out float h) ? h : 0f;
                    characterMotor.velocity = Vector3.down * Mathf.Clamp(height / Mathf.Max(remaining, 0.05f), 0f, descentSpeed);
                }
            }

            if (isAuthority && remaining <= 0f)
            {
                outer.SetNextStateToMain();
            }
        }

        private bool HeightAboveGround(out float height)
        {
            Vector3 feet = characterBody ? characterBody.footPosition : transform.position;
            float clearance = 0.5f * Mathf.Max(characterScale, 1f);
            if (Physics.Raycast(feet + Vector3.up * clearance, Vector3.down, out RaycastHit hit,
                clearance + 200f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                height = Mathf.Max(0f, hit.distance - clearance);
                return true;
            }

            height = 0f;
            return false;
        }

        private void Land()
        {
            landed = true;
            landedAt = fixedAge;

            // The clip's own length, so the root lasts exactly as long as the animation the player is
            // watching rather than a figure that happens to look close.
            landingDuration = dragon
                ? Mathf.Max(minLandingDuration, dragon.ClipLength(DeathwingClips.flightLand))
                : minLandingDuration;

            if (dragon)
            {
                dragon.Release(DeathwingClips.flightLand, landingDuration);
            }
            else
            {
                PlayCrossfade("Body", "Land", 0.1f);
            }

            Util.PlaySound(Sounds.wingFlap, gameObject);
        }

        private void Touchdown()
        {
            Util.PlaySound(Sounds.diveImpact, gameObject);
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 0.7f, false, 110f);

            // The landing in Heroes: the ground gives under him, a ring of dust races out and the
            // rock is left cracked and smouldering where he came down.
            if (isAuthority)
            {
                Vector3 ground = characterBody.footPosition;
                DeathwingEffects.SpawnShockwave(ground, 14f * characterScale, 8f, gameObject);
                DeathwingEffects.SpawnGroundFire(ground, 5f * characterScale, 3f, gameObject);
            }

            dragonModel?.Silhouette(0.6f);
        }

        public override void OnExit()
        {
            if (characterMotor)
            {
                characterMotor.useGravity = true;
            }

            SetModelHidden(false);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
