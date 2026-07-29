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

        internal static Material moltenSkinMaterial;
        internal static Material rockSkinMaterial;

        internal static GameObject explosionEffect;
        internal static GameObject fireImpactEffect;
        internal static GameObject eruptionEffect;
        internal static GameObject roarEffect;

        internal static void Init()
        {
            explosionEffect = Load<GameObject>(
                "RoR2/Base/Common/VFX/OmniExplosionVFXQuick.prefab",
                "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab");

            fireImpactEffect = Load<GameObject>(
                "RoR2/Base/Common/VFX/OmniImpactVFXFire.prefab",
                "RoR2/Base/Common/VFX/OmniImpactVFX.prefab");

            eruptionEffect = Load<GameObject>(
                "RoR2/Base/MagmaWorm/MagmaWormBurrowEffect.prefab",
                "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab");

            roarEffect = Load<GameObject>(
                "RoR2/Base/Titan/TitanFistImpact.prefab",
                "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab");
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

        /// <summary>
        /// Builds Deathwing's materials from a material already shipped with the game so the
        /// Hopoo shader (and its lighting/overlay support) is preserved.
        /// </summary>
        internal static void CreateMaterials(Material template)
        {
            moltenSkinMaterial = TintedCopy(template, "matDeathwingMolten", new Color(0.32f, 0.05f, 0.03f), new Color(3.4f, 0.75f, 0.12f), 3.5f);
            rockSkinMaterial = TintedCopy(template, "matDeathwingElementium", new Color(0.16f, 0.14f, 0.13f), new Color(0.9f, 0.25f, 0.05f), 1.1f);
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
