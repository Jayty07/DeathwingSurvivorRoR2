using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// His heroic. Deathwing rears up and climbs high over the field, then flies the length of a lane
    /// ahead of him, the ground tearing open and burning beneath him as he passes the way he scorches a
    /// path down the battlefield in Heroes of the Storm. The lane is shown for the whole two-second
    /// climb; he comes down where it ends and is rooted for his landing beat.
    /// </summary>
    public class Cataclysm : BaseDeathwingSkillState
    {
        /// <summary>His own two seconds between the call and the ground opening.</summary>
        public static float baseChannelDuration = 2f;
        public static float baseFlightDuration = 2.4f;
        public static float laneLength = 60f;
        public static float laneWidth = 16f;
        public static int segments = 8;
        public static float segmentForce = 2600f;
        public static float shakeMagnitude = 8f;
        /// <summary>
        /// The rear-up clip has him planted through its first stretch and throws him skyward late in
        /// it; the climb starts at this fraction of the channel and has him this high (pre-scale) by
        /// its end, low enough to still read as skimming the field.
        /// </summary>
        public static float climbStartFraction = 0.6f;
        public static float climbHeight = 6f;
        public static float descentSpeed = 45f;
        public static float maxDescentDuration = 8f;
        public static float minDescentDuration = 0.15f;
        public static float minLandingDuration = 0.4f;
        public static float remoteGroundProbe = 1.5f;

        private float channelDuration;
        private float flightDuration;
        private Vector3 aimDirection;
        private Vector3 origin;
        private float cruiseHeight;
        private int segmentsFired;
        private bool climbing;
        private bool flying;
        private bool descending;
        private bool landed;
        private float descentStartedAt;
        private float landedAt;
        private float landingDuration = minLandingDuration;

        private bool IsChannelling => fixedAge < channelDuration;
        private bool IsFlying => !IsChannelling && fixedAge < channelDuration + flightDuration;

        private float ClimbStart => channelDuration * climbStartFraction;
        private float ClimbHeight => climbHeight * characterScale;

        private float HalfWidth => laneWidth * 0.5f * Mathf.Max(1f, characterScale * 0.6f);

        public override void OnEnter()
        {
            base.OnEnter();
            // Flat, not scaled by attack speed: his figure, and the telegraph is the point.
            channelDuration = baseChannelDuration;
            flightDuration = baseFlightDuration;

            aimDirection = GetAimRay().direction;
            aimDirection.y = 0f;
            aimDirection = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : transform.forward;
            if (characterDirection)
            {
                characterDirection.forward = aimDirection;
            }

            origin = characterBody ? characterBody.footPosition : transform.position;

            characterBody.AddTimedBuff(Buffs.elementiumPlating, channelDuration + flightDuration + 1f);
            characterBody.SetAimTimer(channelDuration + flightDuration);
            Util.PlaySound(Sounds.cataclysmChannel, gameObject);
            DeathwingVoice.Play(DeathwingVoice.bellowingRoar, gameObject, 1f, false, 120f);
            PlayDragonAnimation(DeathwingClips.cataclysmChannel, channelDuration,
                "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
            dragonModel?.Silhouette(channelDuration);
            DeathwingAssets.SpawnEffect(DeathwingAssets.roarEffect, transform.position, 4f * characterScale, gameObject);

            DeathwingEffects.Telegraph(GroundPosition(origin + aimDirection * 3f), HalfWidth, channelDuration,
                aimDirection, laneLength);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (characterMotor)
            {
                characterMotor.moveDirection = Vector3.zero;
            }

            if (IsChannelling)
            {
                Channel();
            }
            else if (IsFlying)
            {
                Fly();
            }
            else if (!landed)
            {
                Descend();
            }
            else
            {
                // Rooted until the landing beat has played out.
                if (characterMotor)
                {
                    characterMotor.velocity = Vector3.zero;
                }

                if (isAuthority && fixedAge - landedAt >= landingDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }

        /// <summary>
        /// Rooted through the first stretch of the rear-up, then carried straight up with it so he is
        /// high over the field when the clip ends. The climb builds through the clip rather than
        /// snapping him off the ground, arriving at the cruise height as the channel closes.
        /// </summary>
        private void Channel()
        {
            if (!characterMotor)
            {
                return;
            }

            if (fixedAge < ClimbStart)
            {
                characterMotor.velocity = new Vector3(0f, Mathf.Min(0f, characterMotor.velocity.y), 0f);
                return;
            }

            if (!climbing)
            {
                climbing = true;
                characterMotor.useGravity = false;
                if (characterMotor.Motor)
                {
                    characterMotor.Motor.ForceUnground();
                }

                Util.PlaySound(Sounds.wingFlap, gameObject);
                DeathwingEffects.DustPuff(characterBody.footPosition, 6f * characterScale);
            }

            // Height h(t) = H * u^2 over the window, so the rate is 2H u / window.
            float window = Mathf.Max(channelDuration - ClimbStart, 0.05f);
            float u = Mathf.Clamp01((fixedAge - ClimbStart) / window);
            characterMotor.velocity = Vector3.up * (2f * ClimbHeight * u / window);
        }

        /// <summary>
        /// Level flight down the lane at the height the climb reached, the ground opening under him
        /// segment by segment as he crosses it.
        /// </summary>
        private void Fly()
        {
            if (!flying)
            {
                flying = true;
                cruiseHeight = transform.position.y;
                // One wingbeat drawn out over the whole run down the lane rather than the cycle looping,
                // so the clip's end lands with the end of the flight.
                if (dragon)
                {
                    dragon.PlayOnce(DeathwingClips.flightLoop, flightDuration, 0.25f);
                }

                Util.PlaySound(Sounds.wingFlap, gameObject);
            }

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                if (characterMotor.Motor)
                {
                    characterMotor.Motor.ForceUnground();
                }

                // Held to the cruise height so a bump on the way up cannot leave him drifting.
                float correction = (cruiseHeight - transform.position.y) * 4f;
                characterMotor.velocity = aimDirection * (laneLength / flightDuration) + Vector3.up * correction;
            }

            float progress = Mathf.Clamp01((fixedAge - channelDuration) / flightDuration);
            int wanted = Mathf.Min(segments, Mathf.FloorToInt(progress * segments) + 1);
            while (segmentsFired < wanted)
            {
                FireSegment(segmentsFired);
                segmentsFired++;
            }
        }

        /// <summary>
        /// Dropped at the end of the lane with no say in where he goes, wings out, and rooted the moment
        /// he touches down for as long as the landing clip runs.
        /// </summary>
        private void Descend()
        {
            if (!descending)
            {
                descending = true;
                descentStartedAt = fixedAge;
                while (segmentsFired < segments)
                {
                    FireSegment(segmentsFired);
                    segmentsFired++;
                }

            }

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.velocity = Vector3.down * descentSpeed;
            }

            float dropping = fixedAge - descentStartedAt;
            if ((dropping >= minDescentDuration && TouchingGround()) || dropping >= maxDescentDuration)
            {
                Land();
            }
        }

        private bool TouchingGround()
        {
            if (!characterMotor)
            {
                return false;
            }

            if (characterMotor.hasEffectiveAuthority)
            {
                return characterMotor.isGrounded;
            }

            Vector3 feet = characterBody ? characterBody.footPosition : transform.position;
            return Physics.Raycast(
                feet + Vector3.up * 0.5f,
                Vector3.down,
                remoteGroundProbe * Mathf.Max(characterScale, 1f),
                LayerIndex.world.mask,
                QueryTriggerInteraction.Ignore);
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

            Util.PlaySound(Sounds.diveImpact, gameObject);
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 0.7f, false, 110f);
            ShakeCamera(characterBody.footPosition, shakeMagnitude * 0.5f, 0.4f, 60f);

            if (isAuthority)
            {
                Vector3 ground = characterBody.footPosition;
                DeathwingEffects.SpawnShockwave(ground, 14f * characterScale, 8f, gameObject);
                DeathwingEffects.SpawnGroundFire(ground, 5f * characterScale, 3f, gameObject);
            }

            dragonModel?.Silhouette(0.6f);
        }

        private void FireSegment(int index)
        {
            float step = laneLength / segments;
            float distance = step * (index + 0.5f);
            Vector3 center = GroundPosition(origin + aimDirection * distance, 60f);
            float halfWidth = HalfWidth;

            Util.PlaySound(Sounds.cataclysmErupt, gameObject);
            DeathwingVoice.Play(DeathwingVoice.stoneImpact, gameObject, 1f, false, 140f);
            DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, center, 4f, gameObject);

            // Two side fissures per segment so the lane reads as a torn strip rather than a line of pits.
            Vector3 across = Vector3.Cross(Vector3.up, aimDirection);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 edge = GroundPosition(center + across * (halfWidth * 0.6f * side), 60f);
                DeathwingAssets.SpawnEffect(DeathwingAssets.eruptionEffect, edge, 2.5f, gameObject);
            }

            if (index == 0)
            {
                ShakeCamera(center, shakeMagnitude, 0.6f, laneLength + 40f);
            }

            if (!isAuthority)
            {
                return;
            }

            DeathwingEffects.SpawnShockwave(center, halfWidth * 1.2f, 3f, gameObject, true);
            DeathwingEffects.SpawnGroundFire(center, halfWidth, 6f, gameObject, aimDirection, step);

            if (Projectiles.lavaPool)
            {
                ProjectileManager.instance.FireProjectile(
                    Projectiles.lavaPool,
                    center,
                    Quaternion.identity,
                    gameObject,
                    damageStat * Projectiles.lavaPoolDamageCoefficient,
                    0f,
                    RollCrit(),
                    DamageColorIndex.Item);
            }

            // Each segment is its own blast covering its stretch of the lane; a segment radius a little
            // over the half-width leaves no seams between neighbours.
            BlastAttack blast = CreateFireBlast(center, Mathf.Max(halfWidth, step * 0.75f),
                Tuning.heroicCataclysmDamageCoefficient.Value, segmentForce);
            blast.bonusForce = Vector3.up * (segmentForce * 0.5f);
            blast.Fire();
        }

        public override void OnExit()
        {
            if (characterMotor)
            {
                characterMotor.useGravity = true;
            }

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }
}
