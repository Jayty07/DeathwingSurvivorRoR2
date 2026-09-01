using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// The end of a flight he chose to end himself. He is drawn again and dropped back to the ground
    /// with no say in where he goes, and the moment he touches down he is rooted for as long as his
    /// landing beat runs: a dragon of his weight does not land and walk off in the same instant.
    /// </summary>
    public class DragonflightLanding : BaseDeathwingSkillState
    {
        public static float descentSpeed = 45f;
        /// <summary>Ceiling on the drop, so a flight that ends over a hole cannot strand the state.</summary>
        public static float maxDescentDuration = 8f;
        public static float minLandingDuration = 0.4f;

        private float landingDuration = minLandingDuration;
        private float landedAt;
        private bool landed;

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

                if ((characterMotor && characterMotor.isGrounded) || fixedAge >= maxDescentDuration)
                {
                    Land();
                }

                return;
            }

            // Rooted until the landing beat has played out.
            if (characterMotor)
            {
                characterMotor.velocity = Vector3.zero;
                characterMotor.moveDirection = Vector3.zero;
            }

            if (isAuthority && fixedAge - landedAt >= landingDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Land()
        {
            landed = true;
            landedAt = fixedAge;

            if (characterMotor)
            {
                characterMotor.useGravity = true;
                characterMotor.velocity = Vector3.zero;
            }

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
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 0.7f, false, 110f);
            ShakeCamera(transform.position, 6f, 0.4f, 40f);
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
