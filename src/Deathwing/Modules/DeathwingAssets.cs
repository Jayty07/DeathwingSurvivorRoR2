using System;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Deathwing.Modules
{
    /// <summary>
    /// Resolves vanilla content that the survivor borrows (models, effects, base projectiles) and
    /// derives Deathwing's molten materials from it. Every lookup is tolerant of missing keys so a
    /// game update that renames an address degrades visuals instead of breaking the survivor.
    /// </summary>
    internal static class DeathwingAssets
    {
        internal const string commandoBodyKey = "RoR2/Base/Commando/CommandoBody.prefab";

        internal static GameObject explosionEffect;
        internal static GameObject fireImpactEffect;
        internal static GameObject eruptionEffect;
        internal static GameObject roarEffect;

        internal static void Init()
        {
            explosionEffect = Load<GameObject>(
                "RoR2/Base/Common/VFX/OmniExplosionVFXQuick.prefab",
                "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab");

            // Every effect falls back to the explosion that is known to resolve, so a skill never
            // silently loses all of its feedback.
            fireImpactEffect = Load<GameObject>("RoR2/Base/Common/VFX/OmniImpactVFX.prefab") ?? explosionEffect;
            eruptionEffect = explosionEffect;
            roarEffect = explosionEffect;
        }

        /// <summary>
        /// Adopts effects off a prefab that already resolved. Vanilla projectiles carry effects that are
        /// guaranteed to be registered in the effect catalog, which is more reliable than guessing at
        /// effect addresses.
        /// </summary>
        internal static void AdoptEffectsFrom(GameObject explosionSource, GameObject impactSource)
        {
            if (explosionSource)
            {
                explosionEffect = explosionSource;
                eruptionEffect = explosionSource;
                roarEffect = explosionSource;
            }

            if (impactSource)
            {
                fireImpactEffect = impactSource;
            }
        }

        /// <summary>Tries each address in order, returning the first asset that resolves.</summary>
        internal static T Load<T>(params string[] keys) where T : UnityEngine.Object
        {
            foreach (string key in keys)
            {
                try
                {
                    T asset = Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
                    if (asset)
                    {
                        return asset;
                    }
                }
                catch (Exception exception)
                {
                    Log.Debug($"Address '{key}' did not resolve: {exception.Message}");
                }
            }

            Log.Warning($"None of the addresses [{string.Join(", ", keys)}] resolved to a {typeof(T).Name}.");
            return null;
        }

        internal static Material TintedCopy(Material template, string name, Color albedo, Color emissionColor, float emissionPower)
        {
            if (!template)
            {
                return null;
            }

            Material material = UnityEngine.Object.Instantiate(template);
            material.name = name;
            TrySetColor(material, "_Color", albedo);
            TrySetColor(material, "_EmColor", emissionColor);
            TrySetFloat(material, "_EmPower", emissionPower);
            TrySetFloat(material, "_SpecularStrength", 0.35f);
            TrySetFloat(material, "_SpecularExponent", 8f);
            return material;
        }

        private static void TrySetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void TrySetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        /// <summary>Spawns an effect only if it actually resolved and was registered.</summary>
        internal static void SpawnEffect(GameObject effectPrefab, Vector3 position, float scale = 1f, GameObject origin = null)
        {
            if (!effectPrefab)
            {
                return;
            }

            EffectManager.SpawnEffect(effectPrefab, new EffectData
            {
                origin = position,
                scale = scale,
                rootObject = origin
            }, true);
        }
    }
}
