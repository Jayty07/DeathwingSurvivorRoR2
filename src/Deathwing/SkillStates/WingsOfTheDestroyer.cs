using Deathwing.Modules;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Flight. Deathwing beats his wings and takes off: hold the key to stay airborne, steer with the
    /// camera, and press the primary key to end the flight in a dive. Flight time is a fuel budget
    /// rather than a cooldown, so the skill only refunds what is left over when it ends.
    /// </summary>
    public class WingsOfTheDestroyer : BaseDeathwingSkillState
    {
        public static float maxFlightDuration = 6f;
        public static float takeoffDuration = 0.45f;
        public static float takeoffVerticalSpeed = 18f;
        public static float horizontalSpeedMultiplier = 2.1f;
        public static float verticalSpeed = 11f;
        public static float hoverDrift = -0.6f;
        public static float wingBeatInterval = 0.55f;

        private float wingBeatStopwatch;
        private bool wantsToDive;

        public override void OnEnter()
        {
            base.OnEnter();

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.disableAirControlUntilCollision = false;
                characterMotor.velocity = new Vector3(characterMotor.velocity.x, takeoffVerticalSpeed, characterMotor.velocity.z);
            }

            characterBody.AddTimedBuff(Buffs.elementiumPlating, takeoffDuration);
            characterBody.SetAimTimer(maxFlightDuration);
            Util.PlaySound(Sounds.wingFlap, gameObject);
            PlayCrossfade("Body", "Jump", 0.1f);
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, transform.position, 1.4f * characterScale, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor)
            {
                characterMotor.velocity = CalculateFlightVelocity();
            }

            wingBeatStopwatch += GetDeltaTime();
            if (wingBeatStopwatch >= wingBeatInterval)
            {
                wingBeatStopwatch = 0f;
                Util.PlaySound(Sounds.wingFlap, gameObject);
            }

            if (!isAuthority)
            {
                return;
            }

            if (inputBank && inputBank.skill1.justPressed)
            {
                wantsToDive = true;
            }

            if (wantsToDive)
            {
                outer.SetNextState(new DiveSlam());
                return;
            }

            bool outOfFuel = fixedAge >= maxFlightDuration;
            bool released = fixedAge > takeoffDuration && !IsKeyDownAuthority();
            if (outOfFuel || released)
            {
                outer.SetNextStateToMain();
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
