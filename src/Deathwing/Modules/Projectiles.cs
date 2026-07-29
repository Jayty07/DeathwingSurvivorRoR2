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

        internal const float lavaPoolLifetime = 6f;
        internal const float lavaPoolDamageCoefficient = 0.4f;
        internal const float lavaPoolRadiusScale = 2.2f;

        internal static void Init()
        {
            lavaPool = CreateLavaPool();
            moltenBoulder = CreateMoltenBoulder();
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

            // Acid's green comes from its textures rather than a colour property, so a tint alone leaves
            // it olive. When that is the prefab we ended up with, its own visuals are dropped and the
            // pool is drawn out of Deathwing's fire effect instead.
            if (source.name.IndexOf("Acid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }

                DeathwingLavaVisual visual = prefab.AddComponent<DeathwingLavaVisual>();
                visual.radius = 3f * lavaPoolRadiusScale;
                visual.scale = 1.4f;
            }

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
            ContentAddition.AddProjectile(prefab);
            return prefab;
        }
    }
}
