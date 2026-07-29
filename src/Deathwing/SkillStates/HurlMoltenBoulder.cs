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

        private float duration;
        private bool hasThrown;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            StartAimMode(duration + 1f);
            characterBody.SetAimTimer(duration + 1f);
            Util.PlaySound(Sounds.boulderWindup, gameObject);
            PlayCrossfade("Gesture, Override", "ThrowGrenade", "ThrowGrenade.playbackRate", duration, 0.1f);
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

            // Spawned clear of a body this wide so the boulder cannot clip his own collider, but along
            // the aim ray rather than above it: an upward bias plus a fast projectile threw it well over
            // the crosshair. It is aimed at the point the crosshair is actually over, so the arc from
            // the raised spawn point still converges on what the player is looking at.
            Vector3 origin = aimRay.origin + aimRay.direction * (2.5f * characterScale);
            Vector3 target = Physics.Raycast(aimRay, out RaycastHit aimHit, 400f, LayerIndex.world.mask | LayerIndex.entityPrecise.mask)
                ? aimHit.point
                : aimRay.GetPoint(200f);
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

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
