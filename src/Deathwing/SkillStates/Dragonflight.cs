using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His Z, and the spine of his kit. He beats his wings, climbs out of the fight and flies where he
    /// looks: nothing can touch him up there, he closes his wounds as he goes, and he reforges his shed
    /// elementium plates the moment he lands. It is also the only way those plates ever come back, which
    /// is what makes leaving a fight a decision rather than a retreat.
    /// </summary>
    public class Dragonflight : BaseDeathwingSkillState
    {
        /// <summary>His own takeoff: three seconds of beating his wings before he is clear.</summary>
        public static float takeoffDuration = 3f;
        public static float horizontalSpeedMultiplier = 2.4f;
        public static float verticalSpeed = 12f;
        public static float hoverDrift = -0.6f;
        /// <summary>Grace period before the key can end the flight, so one tap cannot cancel it.</summary>
        public static float landDelay = 0.5f;
        public static float fireballSpeed = 90f;

        private float maxDuration;
        private float healStopwatch;
        private float fireStopwatch;
        private bool airborne;
        private bool wantsToDive;
        private bool wantsToLand;

        public override void OnEnter()
        {
            base.OnEnter();
            maxDuration = takeoffDuration + Tuning.dragonflightDuration.Value;

            // He stays on the ground for the whole windup: the three seconds are him beating his wings
            // to get clear, and lifting him through them fights the clip, which is drawn standing.
            if (characterMotor)
            {
                characterMotor.disableAirControlUntilCollision = false;
                characterMotor.velocity = Vector3.zero;
                characterMotor.moveDirection = Vector3.zero;
            }

            characterBody.SetAimTimer(maxDuration);
            Util.PlaySound(Sounds.wingFlap, gameObject);
            DeathwingVoice.Play(DeathwingVoice.roar, gameObject, 0.9f);
            PlayDragonAnimation(DeathwingClips.takeoff, takeoffDuration, "Body", "Jump");
            DeathwingAssets.SpawnEffect(
                DeathwingAssets.roarEffect, transform.position, 1.6f * characterScale, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor)
            {
                characterMotor.velocity = FlightVelocity();
                if (!airborne)
                {
                    characterMotor.moveDirection = Vector3.zero;
                }
            }

            if (!airborne && fixedAge >= takeoffDuration)
            {
                airborne = true;

                if (characterMotor)
                {
                    characterMotor.useGravity = false;

                    // Ground snapping holds him down every step unless the motor is unstuck from it.
                    if (characterMotor.Motor)
                    {
                        characterMotor.Motor.ForceUnground();
                    }
                }

                // Out of the fight and out of sight the moment the takeoff finishes, as his flight does
                // in Heroes of the Storm: he is gone, not a dragon hovering overhead.
                SetModelHidden(true);

                if (dragon)
                {
                    dragon.PlayHeld(DeathwingClips.flightLoop, 0.2f);
                }
            }

            if (airborne)
            {
                // Untouchable and mending: refreshed each step rather than applied once, so nothing that
                // strips buffs can leave him flying without either.
                characterBody.AddTimedBuff(RoR2Content.Buffs.Immune, 0.3f);
                Heal();
            }

            if (!isAuthority)
            {
                return;
            }

            if (airborne && inputBank)
            {
                // M1 rains fire on what he is looking at, as it does in Heroes of the Storm; the dive is
                // on M2 so holding fire cannot slam him into the ground by accident.
                if (inputBank.skill1.down)
                {
                    Breathe();
                }

                if (inputBank.skill2.justPressed)
                {
                    wantsToDive = true;
                }
            }

            if (!wantsToDive && fixedAge > takeoffDuration + landDelay && Tuning.dragonflightKey.Value.IsDown())
            {
                wantsToLand = true;
            }

            if (wantsToDive)
            {
                outer.SetNextState(new DiveSlam());
                return;
            }

            if (wantsToLand || fixedAge >= maxDuration)
            {
                // The descent and the landing beat are their own state: he comes down with no control,
                // and is rooted from the moment he touches the ground until the beat has played.
                outer.SetNextState(new DragonflightLanding());
            }
        }

        /// <summary>
        /// Spits a fireball down at what he is aiming at, no faster than his own breath allows. It is
        /// aimed at the surface under the cursor rather than straight down, so he can lead a target he
        /// is flying over instead of only bombing what is directly beneath him.
        /// </summary>
        private void Breathe()
        {
            fireStopwatch += GetDeltaTime();
            if (fireStopwatch < Tuning.dragonFireInterval.Value)
            {
                return;
            }

            fireStopwatch = 0f;
            characterBody.SetAimTimer(1f);

            Ray aimRay = GetAimRay();
            Vector3 origin = aimRay.origin + aimRay.direction * (2.5f * characterScale);
            Vector3 target = Physics.Raycast(aimRay, out RaycastHit hit, 400f,
                LayerIndex.world.mask | LayerIndex.entityPrecise.mask)
                ? hit.point
                : aimRay.GetPoint(200f);
            Vector3 direction = (target - origin).normalized;

            DeathwingVoice.Play(DeathwingVoice.flameBreath, gameObject, 0.5f);

            if (Projectiles.dragonFire)
            {
                ProjectileManager.instance.FireProjectile(
                    Projectiles.dragonFire,
                    origin,
                    Quaternion.LookRotation(direction),
                    gameObject,
                    damageStat * Tuning.dragonFireDamageCoefficient.Value,
                    900f,
                    RollCrit(),
                    DamageColorIndex.Item,
                    null,
                    fireballSpeed);
                return;
            }

            // Without the borrowed projectile the fireball still has to land somewhere.
            CreateFireBlast(target, 9f, Tuning.dragonFireDamageCoefficient.Value, 900f).Fire();
            SpawnFireEffect(target, 4f);
        }

        /// <summary>Closes his wounds a fraction of his maximum health at a time while he is up.</summary>
        private void Heal()
        {
            if (!isAuthority || !healthComponent)
            {
                return;
            }

            healStopwatch += GetDeltaTime();
            if (healStopwatch < 0.25f)
            {
                return;
            }

            healthComponent.Heal(
                healthComponent.fullHealth * Tuning.dragonflightHealFraction.Value * healStopwatch, default);
            healStopwatch = 0f;
        }

        /// <summary>Climbs on takeoff, then steers with the camera; hold still to hover.</summary>
        private Vector3 FlightVelocity()
        {
            // Planted through the windup: only whatever gravity is doing to him is kept.
            if (!airborne)
            {
                return new Vector3(0f, Mathf.Min(0f, characterMotor.velocity.y), 0f);
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

                    // Forward follows the full aim direction, so looking up climbs and looking down dives;
                    // strafing stays level.
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

            // Whatever ends the flight - a dive, the landing state, a death - he is drawn again; only
            // the flight itself hides him.
            SetModelHidden(false);

            // The dive and the landing state each play their own clip, so the hold is only released here.
            if (dragon)
            {
                dragon.Release();
            }

            // Reforged on landing from the health he lands with, exactly as his trait does it. A dive is
            // still a landing, so it counts too.
            DeathwingForms forms = GetComponent<DeathwingForms>();
            if (forms)
            {
                forms.OnDragonflightLanded(forms.currentForm);
            }

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
