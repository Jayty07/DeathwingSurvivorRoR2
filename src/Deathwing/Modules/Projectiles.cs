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
        internal const float lavaPoolRadiusScale = 1.6f;

        internal static void Init()
        {
            lavaPool = CreateLavaPool();
            moltenBoulder = CreateMoltenBoulder();
        }

        private static GameObject CreateLavaPool()
        {
            GameObject source = DeathwingAssets.Load<GameObject>(
                "RoR2/Base/Croco/CrocoLeapAcid.prefab",
                "RoR2/Base/Croco/CrocoSpitAcid.prefab");
            if (!source)
            {
                Log.Warning("No lava pool base projectile found; Molten Boulder will not leave lava.");
                return null;
            }

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

            Retint(prefab, new Color(1f, 0.35f, 0.05f));
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

            if (source.TryGetComponent(out ProjectileImpactExplosion sourceExplosion))
            {
                DeathwingAssets.AdoptEffectsFrom(sourceExplosion.explosionEffect, sourceExplosion.impactEffect);
            }

            GameObject prefab = PrefabAPI.InstantiateClone(source, "DeathwingMoltenBoulder");
            prefab.transform.localScale *= 2.2f;

            if (prefab.TryGetComponent(out ProjectileImpactExplosion impactExplosion))
            {
                impactExplosion.blastRadius = 11f;
                impactExplosion.blastDamageCoefficient = 1f;
                impactExplosion.destroyOnEnemy = true;
                impactExplosion.destroyOnWorld = true;
                impactExplosion.impactOnWorld = true;
                impactExplosion.timerAfterImpact = false;
                impactExplosion.lifetime = 8f;
                impactExplosion.falloffModel = BlastAttack.FalloffModel.None;

                if (DeathwingAssets.explosionEffect)
                {
                    impactExplosion.explosionEffect = DeathwingAssets.explosionEffect;
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

            Retint(prefab, new Color(1f, 0.3f, 0.05f));
            ContentAddition.AddProjectile(prefab);
            return prefab;
        }

        /// <summary>Pushes an orange tint into whatever renderers the borrowed prefab happens to use.</summary>
        private static void Retint(GameObject prefab, Color color)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i])
                    {
                        continue;
                    }

                    Material copy = Object.Instantiate(materials[i]);
                    copy.name = materials[i].name + "Deathwing";
                    foreach (string property in new[] { "_Color", "_TintColor", "_EmColor" })
                    {
                        if (copy.HasProperty(property))
                        {
                            copy.SetColor(property, color);
                        }
                    }

                    materials[i] = copy;
                }

                renderer.sharedMaterials = materials;
            }

            foreach (ParticleSystemRenderer particles in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                ParticleSystem system = particles.GetComponent<ParticleSystem>();
                if (!system)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.startColor = color;
            }
        }
    }
}
