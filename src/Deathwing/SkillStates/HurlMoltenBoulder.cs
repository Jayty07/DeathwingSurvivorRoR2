using Deathwing.Modules;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.SkillStates
{
    /// <summary>
    /// Secondary: tears a boulder out of the ground and hurls it. The boulder arcs, explodes on
    /// contact and leaves a lava pool behind.
    /// </summary>
    public class HurlMoltenBoulder : BaseDeathwingSkillState
    {
        public static float baseDuration = 1.5f;
        public static float throwFraction = 0.55f;
        public static float projectileSpeed = 110f;
        public static float fallbackBlastRadius = 16f;
        public static float forwardOffset = 2f;
        public static float minGroundClearance = 1.5f;

        private float duration;
        private bool hasThrown;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            StartAimMode(duration + 1f);
            characterBody.SetAimTimer(duration + 1f);
            Util.PlaySound(Sounds.boulderWindup, gameObject);
            PlayDragonAnimation(DeathwingClips.boulder, duration,
                "Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!hasThrown && fixedAge >= duration * throwFraction)
            {
                hasThrown = true;
                Throw();
            }

            if (fixedAge >= duration && isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }

        private void Throw()
        {
            Util.PlaySound(Sounds.boulderThrow, gameObject);
            ShakeCamera(transform.position, 3.5f, 0.3f, 30f);
            if (!isAuthority)
            {
                return;
            }

            Ray aimRay = GetAimRay();
            Vector3 target = Physics.Raycast(aimRay, out RaycastHit aimHit, 400f, LayerIndex.world.mask | LayerIndex.entityPrecise.mask)
                ? aimHit.point
                : aimRay.GetPoint(200f);

            Vector3 origin = SpawnPoint(aimRay);
            Vector3 direction = (target - origin).normalized;

            if (Projectiles.moltenBoulder)
            {
                ProjectileManager.instance.FireProjectile(
                    Projectiles.moltenBoulder,
                    origin,
                    Quaternion.LookRotation(direction),
                    gameObject,
                    damageStat * Tuning.boulderDamageCoefficient.Value,
                    2200f,
                    RollCrit(),
                    DamageColorIndex.Item,
                    null,
                    projectileSpeed);
                return;
            }

            // Without the borrowed projectile prefab the skill still has to do something useful.
            Vector3 impact = Physics.Raycast(aimRay, out RaycastHit hit, 200f, LayerIndex.world.mask | LayerIndex.entityPrecise.mask)
                ? hit.point
                : aimRay.GetPoint(60f);

            CreateFireBlast(impact, fallbackBlastRadius, Tuning.boulderDamageCoefficient.Value, 2200f).Fire();
            SpawnFireEffect(impact, fallbackBlastRadius * 0.4f);
        }

        /// <summary>
        /// Where the boulder leaves his hands. It has to clear a body this wide without ending up
        /// underground: pushing it straight out along the aim ray buried it in any slope Deathwing was
        /// standing on, so the forward offset is shortened until the spawn point has line of sight from
        /// his chest, and then lifted to a minimum height above whatever is underneath it.
        /// </summary>
        private Vector3 SpawnPoint(Ray aimRay)
        {
            Vector3 chest = characterBody.corePosition + Vector3.up * (0.6f * characterScale);
            Vector3 forward = aimRay.direction;
            forward.y = Mathf.Max(forward.y, 0f);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.up;

            float reach = forwardOffset * characterScale;
            if (Physics.Raycast(chest, forward, out RaycastHit blocked, reach, LayerIndex.world.mask))
            {
                reach = Mathf.Max(0f, blocked.distance - 0.5f);
            }

            Vector3 origin = chest + forward * reach;
            float clearance = minGroundClearance * characterScale;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, clearance, LayerIndex.world.mask))
            {
                origin = ground.point + Vector3.up * clearance;
            }

            return origin;
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
