using System;
using R2API;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Deathwing's projectiles are clones of vanilla ones: the boulder reuses the engineer grenade's
    /// physics (an arcing rigidbody that explodes on contact) and the lava pool reuses Acrid's acid
    /// pool (a networked damage-over-time zone). Both are retuned and retinted rather than rebuilt,
    /// which keeps their networking and prediction behaviour intact.
    /// </summary>
    internal static class Projectiles
    {
        internal static GameObject moltenBoulder;
        internal static GameObject lavaPool;
        internal static GameObject dragonFire;

        internal const float lavaPoolLifetime = 6f;
        internal const float lavaPoolDamageCoefficient = 0.4f;
        internal const float lavaPoolRadiusScale = 2.2f;

        internal static void Init()
        {
            lavaPool = CreateLavaPool();
            moltenBoulder = CreateMoltenBoulder();
            dragonFire = CreateDragonFire();
        }

        /// <summary>
        /// First address that both resolves and carries a damage zone, so a candidate that exists but is
        /// the wrong kind of projectile is skipped rather than producing a pool that does nothing.
        /// </summary>
        private static GameObject FindDotZoneSource(params string[] addresses)
        {
            foreach (string address in addresses)
            {
                GameObject candidate = DeathwingAssets.Load<GameObject>(address);
                if (candidate && candidate.GetComponent<ProjectileDotZone>())
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// First address that both resolves and explodes on impact, so a projectile that only pokes a
        /// single target is skipped rather than producing a fireball that does not burst.
        /// </summary>
        private static GameObject FindExplosionSource(params string[] addresses)
        {
            foreach (string address in addresses)
            {
                GameObject candidate = DeathwingAssets.Load<GameObject>(address);
                if (candidate && candidate.GetComponent<ProjectileImpactExplosion>())
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// A projectile's visible body is not part of the projectile: it lives in a separate ghost prefab
        /// that the controller instantiates locally, which is why recolouring the projectile itself left
        /// Acrid's green pool and the engineer's green grenade untouched. The ghost is cloned so it can be
        /// recoloured, or dropped entirely when its artwork is the wrong colour beyond tinting.
        /// </summary>
        private static void ReplaceGhost(GameObject prefab, string name, bool discard, float scale = 1f)
        {
            if (!prefab.TryGetComponent(out ProjectileController controller) || !controller.ghostPrefab)
            {
                return;
            }

            if (discard)
            {
                controller.ghostPrefab = null;
                return;
            }

            GameObject ghost = PrefabAPI.InstantiateClone(controller.ghostPrefab, name, false);
            DeathwingAssets.Recolor(ghost);

            // The ghost is what is actually seen: the controller spawns it at the projectile's position
            // and rotation but never at its scale, so growing the projectile alone grows its collider
            // and leaves the fireball drawn the size the mage throws it.
            if (!Mathf.Approximately(scale, 1f))
            {
                ghost.transform.localScale *= scale;
            }

            controller.ghostPrefab = ghost;
        }

        private static GameObject CreateLavaPool()
        {
            // Fire-coloured damage zones are preferred over Acrid's acid: recolouring only reaches a
            // material's colour properties, so a prefab whose artwork is green stays greenish however it
            // is tinted. Acid is the fallback because it is the one that always resolves.
            GameObject source = FindDotZoneSource(
                "RoR2/Base/Mage/MageFirewallSegment.prefab",
                "RoR2/Base/Brother/LunarNeedleGroundZone.prefab",
                "RoR2/Base/Croco/CrocoLeapAcid.prefab",
                "RoR2/Base/Croco/CrocoSpitAcid.prefab");
            if (!source)
            {
                Log.Warning("No lava pool base projectile found; Molten Boulder will not leave lava.");
                return null;
            }

            Log.Info($"Lava pool cloned from '{source.name}'.");

            GameObject prefab = PrefabAPI.InstantiateClone(source, "DeathwingLavaPool");
            prefab.transform.localScale *= lavaPoolRadiusScale;

            if (prefab.TryGetComponent(out ProjectileDotZone dotZone))
            {
                dotZone.lifetime = lavaPoolLifetime;
                dotZone.damageCoefficient = lavaPoolDamageCoefficient;
                dotZone.resetFrequency = 2f;
                dotZone.fireFrequency = 5f;
                dotZone.overlapProcCoefficient = 0.2f;
            }

            if (prefab.TryGetComponent(out ProjectileDamage damage))
            {
                damage.damageType = DamageType.IgniteOnHit;
                damage.damageColorIndex = DamageColorIndex.Item;
            }

            // Acrid's pool is acid green; recolouring it is what turns it into lava.
            DeathwingAssets.Recolor(prefab);

            // Acid's green is in its artwork rather than in a material colour, so when that is the prefab
            // we ended up with, its visuals are dropped outright and the pool is drawn with embers.
            if (source.name.IndexOf("Acid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReplaceGhost(prefab, "DeathwingLavaPoolGhost", discard: true);

                if (dotZone)
                {
                    dotZone.impactEffect = DeathwingAssets.emberEffect;
                }

                DeathwingLavaVisual visual = prefab.AddComponent<DeathwingLavaVisual>();
                visual.radius = 2f * lavaPoolRadiusScale;
                visual.lifetime = lavaPoolLifetime;
            }
            else
            {
                ReplaceGhost(prefab, "DeathwingLavaPoolGhost", discard: false);
            }

            ContentAddition.AddProjectile(prefab);
            return prefab;
        }

        /// <summary>
        /// The fireballs he spits down while flying: the mage's firebolt, which already flies fast and
        /// straight and bursts into flame, rather than the boulder's heavy arc.
        /// </summary>
        private static GameObject CreateDragonFire()
        {
            GameObject source = FindExplosionSource(
                "RoR2/Base/Mage/MageFireboltBasic.prefab",
                "RoR2/Base/Titan/TitanRockProjectile.prefab",
                "RoR2/Base/Engi/EngiGrenadeProjectile.prefab",
                "RoR2/Base/Commando/CommandoGrenadeProjectile.prefab");
            if (!source)
            {
                Log.Warning("No fireball base projectile found; his flight will spit point blasts instead.");
                return null;
            }

            GameObject prefab = PrefabAPI.InstantiateClone(source, "DeathwingDragonFire");
            float scale = Mathf.Max(0.1f, Tuning.dragonFireSize.Value);
            prefab.transform.localScale *= scale;

            if (prefab.TryGetComponent(out ProjectileImpactExplosion impactExplosion))
            {
                impactExplosion.blastRadius = 9f;
                impactExplosion.blastDamageCoefficient = 1f;
                impactExplosion.destroyOnEnemy = true;
                impactExplosion.destroyOnWorld = true;
                impactExplosion.impactOnWorld = true;
                impactExplosion.timerAfterImpact = false;
                impactExplosion.lifetime = 6f;
                impactExplosion.falloffModel = BlastAttack.FalloffModel.None;

                if (DeathwingAssets.explosionEffect)
                {
                    impactExplosion.explosionEffect = DeathwingAssets.explosionEffect;
                }

                if (DeathwingAssets.fireImpactEffect)
                {
                    impactExplosion.impactEffect = DeathwingAssets.fireImpactEffect;
                }
            }

            if (prefab.TryGetComponent(out ProjectileDamage damage))
            {
                damage.damageType = DamageType.IgniteOnHit;
                damage.damageColorIndex = DamageColorIndex.Item;
                damage.force = 900f;
            }

            DeathwingAssets.Recolor(prefab);
            ReplaceGhost(prefab, "DeathwingDragonFireGhost", discard: false, scale: scale);
            ContentAddition.AddProjectile(prefab);
            return prefab;
        }

        private static GameObject CreateMoltenBoulder()
        {
            GameObject source = DeathwingAssets.Load<GameObject>(
                "RoR2/Base/Engi/EngiGrenadeProjectile.prefab",
                "RoR2/Base/Commando/CommandoGrenadeProjectile.prefab");
            if (!source)
            {
                Log.Error("No boulder base projectile found; Molten Boulder will fall back to a point blast.");
                return null;
            }

            GameObject prefab = PrefabAPI.InstantiateClone(source, "DeathwingMoltenBoulder");
            prefab.transform.localScale *= 2.2f;

            if (prefab.TryGetComponent(out ProjectileImpactExplosion impactExplosion))
            {
                impactExplosion.blastRadius = 16f;
                impactExplosion.blastDamageCoefficient = 1f;
                impactExplosion.destroyOnEnemy = true;
                impactExplosion.destroyOnWorld = true;
                impactExplosion.impactOnWorld = true;
                impactExplosion.timerAfterImpact = false;
                impactExplosion.lifetime = 8f;
                impactExplosion.falloffModel = BlastAttack.FalloffModel.None;

                // The engineer's grenade explodes green, so its effects are replaced outright rather
                // than recoloured in place.
                if (DeathwingAssets.boulderExplosionEffect)
                {
                    impactExplosion.explosionEffect = DeathwingAssets.boulderExplosionEffect;
                }

                if (DeathwingAssets.fireImpactEffect)
                {
                    impactExplosion.impactEffect = DeathwingAssets.fireImpactEffect;
                }

                if (lavaPool)
                {
                    impactExplosion.fireChildren = true;
                    impactExplosion.childrenProjectilePrefab = lavaPool;
                    impactExplosion.childrenCount = 1;
                    impactExplosion.childrenDamageCoefficient = 1f;
                    impactExplosion.childrenInheritDamageType = true;
                }
            }

            if (prefab.TryGetComponent(out ProjectileDamage damage))
            {
                damage.damageType = DamageType.IgniteOnHit;
                damage.damageColorIndex = DamageColorIndex.Item;
                damage.force = 2200f;
            }

            DeathwingAssets.Recolor(prefab);
            ReplaceGhost(prefab, "DeathwingMoltenBoulderGhost", discard: false);
            ContentAddition.AddProjectile(prefab);
            return prefab;
        }
    }
}
