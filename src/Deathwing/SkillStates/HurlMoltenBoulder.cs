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
        public static float projectileSpeed = 65f;
        public static float upwardAimBias = 0.12f;
        public static float fallbackBlastRadius = 11f;

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
            if (!isAuthority)
            {
                return;
            }

            Ray aimRay = GetAimRay();
            Vector3 direction = (aimRay.direction + Vector3.up * upwardAimBias).normalized;

            // Spawned from chest height and well clear of a body this wide, so the boulder cannot clip
            // the ground or his own collider and detonate at his feet.
            Vector3 origin = characterBody.corePosition
                + Vector3.up * (1.2f * characterScale)
                + direction * (2.5f * characterScale);

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
            SpawnFireEffect(impact, fallbackBlastRadius * 0.25f);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
