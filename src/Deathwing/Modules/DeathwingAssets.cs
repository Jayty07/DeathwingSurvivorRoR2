using System;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Deathwing.Modules
{
    /// <summary>
    /// Resolves vanilla content that the survivor borrows (models, effects, base projectiles) and
    /// derives Deathwing's molten look from it. Every lookup is tolerant of missing keys so a game
    /// update that renames an address degrades visuals instead of breaking the survivor.
    /// </summary>
    internal static class DeathwingAssets
    {
        internal const string commandoBodyKey = "RoR2/Base/Commando/CommandoBody.prefab";

        /// <summary>Fire colours every borrowed effect and projectile is recoloured towards.</summary>
        internal static readonly Color fireCore = new Color(1f, 0.72f, 0.18f);
        internal static readonly Color fireEdge = new Color(1f, 0.24f, 0.03f);

        internal static GameObject explosionEffect;
        internal static GameObject boulderExplosionEffect;
        internal static GameObject fireImpactEffect;
        internal static GameObject eruptionEffect;
        internal static GameObject roarEffect;

        internal static void Init()
        {
            GameObject explosionSource = Load<GameObject>(
                "RoR2/Base/Common/VFX/OmniExplosionVFXQuick.prefab",
                "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab");
            GameObject impactSource = Load<GameObject>("RoR2/Base/Common/VFX/OmniImpactVFX.prefab") ?? explosionSource;

            // Borrowed effects are cloned, recoloured and enlarged rather than used as-is: the vanilla
            // prefabs that reliably resolve are tinted for their own survivor (the engineer's grenade
            // blast is green, for instance), and Deathwing's blasts should read as bigger and hotter
            // than anything he borrowed them from.
            explosionEffect = CreateFireEffect(explosionSource, "DeathwingExplosionEffect", 3.4f);
            // The boulder's blast is a single rock landing, not a cataclysm; at the shared scale it
            // filled the screen.
            boulderExplosionEffect = CreateFireEffect(explosionSource, "DeathwingBoulderExplosionEffect", 1.8f);
            eruptionEffect = CreateFireEffect(explosionSource, "DeathwingEruptionEffect", 2.6f);
            roarEffect = CreateFireEffect(explosionSource, "DeathwingRoarEffect", 2.2f);
            fireImpactEffect = CreateFireEffect(impactSource, "DeathwingFireImpactEffect", 2.2f) ?? explosionEffect;
        }

        /// <summary>
        /// Clones a vanilla effect, recolours it to fire, scales it up and registers it as Deathwing's
        /// own effect so it can be spawned through <see cref="EffectManager"/>.
        /// </summary>
        private static GameObject CreateFireEffect(GameObject source, string name, float scale)
        {
            if (!source)
            {
                return null;
            }

            GameObject prefab = PrefabAPI.InstantiateClone(source, name, false);
            Recolor(prefab);
            Enlarge(prefab, scale);

            if (prefab.TryGetComponent(out EffectComponent effectComponent))
            {
                // Lets callers size a blast's visual to its actual radius.
                effectComponent.applyScale = true;
            }

            if (!ContentAddition.AddEffect(prefab))
            {
                Log.Warning($"Effect '{name}' could not be registered; falling back to the source effect.");
                return source;
            }

            return prefab;
        }

        /// <summary>
        /// Pushes fire colours into every material, particle system and light on a borrowed prefab.
        /// Materials are copied first so the vanilla asset is never mutated.
        /// </summary>
        internal static void Recolor(GameObject prefab)
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

                    Material copy = UnityEngine.Object.Instantiate(materials[i]);
                    copy.name = materials[i].name + "Deathwing";

                    // Several of the game's effect shaders take their colour from a remap ramp texture
                    // rather than a colour property, which is why tinting alone left Acrid's acid pool
                    // green. Swapping the ramp for a fire one is what actually recolours those.
                    TrySetTexture(copy, "_RemapTex", FireRamp);
                    TrySetTexture(copy, "_ColorRamp", FireRamp);
                    TrySetTexture(copy, "_Ramp", FireRamp);

                    // The colour lives under a different property name depending on which shader the
                    // borrowed effect uses, so every plausible one is set.
                    TrySetColor(copy, "_Color", fireEdge);
                    TrySetColor(copy, "_TintColor", fireEdge);
                    TrySetColor(copy, "_EmColor", fireCore);
                    TrySetColor(copy, "_EmissionColor", fireCore);
                    TrySetColor(copy, "_BrightColor", fireCore);
                    TrySetColor(copy, "_MidColor", fireEdge);
                    TrySetColor(copy, "_DarkColor", new Color(0.25f, 0.04f, 0.01f));
                    TrySetColor(copy, "_RimColor", fireCore);

                    materials[i] = copy;
                }

                renderer.sharedMaterials = materials;

                // Trails carry their own material, which the shared-materials pass above does not cover.
                if (renderer is ParticleSystemRenderer particleRenderer && particleRenderer.trailMaterial)
                {
                    Material trail = UnityEngine.Object.Instantiate(particleRenderer.trailMaterial);
                    trail.name = particleRenderer.trailMaterial.name + "Deathwing";
                    TrySetTexture(trail, "_RemapTex", FireRamp);
                    TrySetColor(trail, "_TintColor", fireEdge);
                    TrySetColor(trail, "_Color", fireEdge);
                    particleRenderer.trailMaterial = trail;
                }
            }

            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = new ParticleSystem.MinMaxGradient(fireCore, fireEdge);

                // A borrowed gradient fades towards its own survivor's colour, so it is replaced with
                // one that cools from yellow-hot to ember instead.
                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
                if (colorOverLifetime.enabled)
                {
                    colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FireGradient());
                }
            }

            foreach (Light light in prefab.GetComponentsInChildren<Light>(true))
            {
                light.color = fireEdge;
            }
        }

        private static Gradient FireGradient()
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(fireCore, 0.35f),
                    new GradientColorKey(fireEdge, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }

        /// <summary>
        /// Grows an effect. Particle sizes and speeds are scaled alongside the transform, because
        /// scaling the transform alone leaves world-simulated particles the same size as the original.
        /// </summary>
        private static void Enlarge(GameObject prefab, float scale)
        {
            prefab.transform.localScale *= scale;

            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.main.scalingMode == ParticleSystemScalingMode.Hierarchy)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.startSizeMultiplier *= scale;
                main.startSpeedMultiplier *= scale;
                main.gravityModifierMultiplier *= scale;
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

        private static Texture2D fireRamp;

        /// <summary>
        /// A black-to-yellow ramp, generated rather than loaded: the game's colour ramps are not
        /// reliably addressable, and a gradient is cheap enough to build.
        /// </summary>
        private static Texture2D FireRamp
        {
            get
            {
                if (fireRamp)
                {
                    return fireRamp;
                }

                const int width = 128;
                fireRamp = new Texture2D(width, 1, TextureFormat.RGBA32, false)
                {
                    name = "texDeathwingFireRamp",
                    wrapMode = TextureWrapMode.Clamp
                };

                for (int i = 0; i < width; i++)
                {
                    float t = (float)i / (width - 1);
                    Color color = t < 0.5f
                        ? Color.Lerp(new Color(0.08f, 0.01f, 0f), fireEdge, t * 2f)
                        : Color.Lerp(fireEdge, new Color(1f, 0.95f, 0.55f), (t - 0.5f) * 2f);
                    fireRamp.SetPixel(i, 0, color);
                }

                fireRamp.Apply();
                return fireRamp;
            }
        }

        private static void TrySetTexture(Material material, string property, Texture texture)
        {
            if (texture && material.HasProperty(property))
            {
                material.SetTexture(property, texture);
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

        /// <summary>
        /// Spawns an effect without going through the network, for visuals that already run on every
        /// client (a projectile's own particles, for instance) and so must not be transmitted again.
        /// </summary>
        internal static void SpawnEffectLocal(GameObject effectPrefab, Vector3 position, float scale, float lifetime)
        {
            if (!effectPrefab)
            {
                return;
            }

            GameObject instance = UnityEngine.Object.Instantiate(effectPrefab, position, Quaternion.identity);
            instance.transform.localScale *= scale;
            UnityEngine.Object.Destroy(instance, lifetime);
        }
    }
}
