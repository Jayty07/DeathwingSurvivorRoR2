using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Flight. Deathwing beats his wings and takes off: the skill is a toggle, so he keeps flying until
    /// the key is pressed again, the primary key ends the flight in a dive, or the wings give out. Steer
    /// with the camera. Flight time is a fuel budget rather than a cooldown, so the skill only refunds
    /// what is left over when it ends.
    /// </summary>
    public class WingsOfTheDestroyer : BaseDeathwingSkillState
    {
        public static float takeoffDuration = 0.45f;
        /// <summary>Grace period before a second press counts, so one tap cannot toggle flight off.</summary>
        public static float toggleOffDelay = 0.35f;
        public static float takeoffVerticalSpeed = 18f;
        public static float horizontalSpeedMultiplier = 2.1f;
        public static float verticalSpeed = 11f;
        public static float hoverDrift = -0.6f;

        private bool wantsToDive;
        private bool wantsToLand;
        private bool beatingWings;
        private float maxFlightDuration;

        public override void OnEnter()
        {
            base.OnEnter();
            maxFlightDuration = Tuning.flightDuration.Value;

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.disableAirControlUntilCollision = false;
                characterMotor.velocity = new Vector3(characterMotor.velocity.x, takeoffVerticalSpeed, characterMotor.velocity.z);

                // Taking off from the ground needs the motor unstuck from it, otherwise ground snapping
                // cancels the upward velocity every step and the skill only appears to work mid-air.
                if (characterMotor.Motor)
                {
                    characterMotor.Motor.ForceUnground();
                }
            }

            characterBody.AddTimedBuff(Buffs.elementiumPlating, takeoffDuration);
            characterBody.SetAimTimer(maxFlightDuration);
            Util.PlaySound(Sounds.wingFlap, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.85f);
            PlayDragonAnimation(DeathwingClips.flightStart, takeoffDuration, "Body", "Jump");
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, transform.position, 1.4f * characterScale, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor)
            {
                characterMotor.velocity = CalculateFlightVelocity();
            }

            if (dragon && !beatingWings && fixedAge >= takeoffDuration)
            {
                // Wings held out and gliding for as long as the flight lasts. Animation runs on every
                // client, so it is chosen before the authority-only input handling below.
                beatingWings = true;
                dragon.PlayHeld(DeathwingClips.flightLoop, 0.2f);
            }

            if (!isAuthority)
            {
                return;
            }

            if (inputBank)
            {
                if (inputBank.skill1.justPressed)
                {
                    wantsToDive = true;
                }
                else if (fixedAge > toggleOffDelay && FlightKeyJustPressed())
                {
                    wantsToLand = true;
                }
            }

            if (wantsToDive)
            {
                outer.SetNextState(new DiveSlam());
                return;
            }

            if (wantsToLand || fixedAge >= maxFlightDuration)
            {
                outer.SetNextStateToMain();
            }
        }

        /// <summary>
        /// True when the key that started the flight is pressed again. The slot the skill was activated
        /// from decides which input button to watch, so a rebound key still toggles flight off.
        /// </summary>
        private bool FlightKeyJustPressed()
        {
            SkillSlot slot = skillLocator ? skillLocator.FindSkillSlot(activatorSkillSlot) : SkillSlot.None;
            switch (slot)
            {
                case SkillSlot.Primary: return inputBank.skill1.justPressed;
                case SkillSlot.Secondary: return inputBank.skill2.justPressed;
                case SkillSlot.Utility: return inputBank.skill3.justPressed;
                case SkillSlot.Special: return inputBank.skill4.justPressed;
                default: return false;
            }
        }

        /// <summary>Steer with the camera while holding a movement key; hold still to hover.</summary>
        private Vector3 CalculateFlightVelocity()
        {
            if (fixedAge < takeoffDuration)
            {
                return new Vector3(characterMotor.velocity.x * 0.9f, takeoffVerticalSpeed, characterMotor.velocity.z * 0.9f);
            }

            Vector3 velocity = Vector3.up * hoverDrift;

            if (inputBank)
            {
                Vector3 moveInput = inputBank.moveVector;
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    Vector3 aim = GetAimRay().direction;
                    Vector3 flatAim = new Vector3(aim.x, 0f, aim.z).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, flatAim);

                    // Forward input follows the full aim direction so looking up climbs and looking
                    // down descends; strafing stays horizontal.
                    Vector3 direction = aim * Vector3.Dot(moveInput.normalized, flatAim)
                        + right * Vector3.Dot(moveInput.normalized, right);

                    velocity += direction.normalized * (moveSpeedStat * horizontalSpeedMultiplier);
                }

                if (inputBank.jump.down)
                {
                    velocity += Vector3.up * verticalSpeed;
                }
            }

            if (characterDirection)
            {
                Vector3 facing = new Vector3(velocity.x, 0f, velocity.z);
                if (facing.sqrMagnitude > 1f)
                {
                    characterDirection.forward = facing.normalized;
                }
            }

            return velocity;
        }

        public override void OnExit()
        {
            if (characterMotor)
            {
                characterMotor.useGravity = true;
            }

            // A dive has its own animation to play, so the landing beat is only for flights that end
            // with him settling back down.
            if (dragon)
            {
                dragon.Release(wantsToDive ? null : DeathwingClips.flightLand, 0.5f);
            }

            // Only the unused portion of the flight budget is refunded, so short hops come back fast.
            if (isAuthority && !wantsToDive && activatorSkillSlot != null)
            {
                float unusedFraction = Mathf.Clamp01(1f - fixedAge / maxFlightDuration);
                activatorSkillSlot.rechargeStopwatch += activatorSkillSlot.CalculateFinalRechargeInterval() * unusedFraction * 0.5f;
            }

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
