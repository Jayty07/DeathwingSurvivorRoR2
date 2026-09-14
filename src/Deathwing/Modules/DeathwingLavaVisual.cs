using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Draws burning ground for a borrowed damage zone. Deathwing's pools are clones of Acrid's acid,
    /// whose green lives in a separate ghost prefab and in its textures rather than in any material
    /// colour, so recolouring it was never going to work: the borrowed artwork is suppressed and the pool
    /// is drawn by a particle system authored here instead, which owes nothing to another survivor's
    /// palette.
    /// </summary>
    public class DeathwingLavaVisual : MonoBehaviour
    {
        public float radius = 4f;
        public float lifetime = 6f;
        public int rockCount = 5;

        /// <summary>How long the borrowed artwork keeps switching itself back on. The zone re-enables its
        /// visuals while it grows into place, so suppression has to be repeated over that window; past it
        /// nothing touches them again and the component stops updating.</summary>
        private const float SuppressionWindow = 1.5f;

        private Renderer[] borrowedRenderers;
        private Projector[] borrowedProjectors;
        private float age;

        private void Start()
        {
            borrowedRenderers = GetComponentsInChildren<Renderer>(true);
            borrowedProjectors = GetComponentsInChildren<Projector>(true);

            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            Suppress();
            BuildFlames();
            DeathwingRocks.Scatter(transform.position, radius * 0.9f, rockCount, radius * 0.28f, lifetime);
        }

        private void Update()
        {
            // A single pass at spawn is undone a moment later, so it is repeated until the zone has settled
            // and then this stops running entirely.
            Suppress();

            age += Time.deltaTime;
            if (age >= SuppressionWindow)
            {
                enabled = false;
            }
        }

        private void Suppress()
        {
            // Only the renderers present before the flames were built, so this never switches off the
            // particle system added below.
            for (int i = 0; i < borrowedRenderers.Length; i++)
            {
                if (borrowedRenderers[i])
                {
                    // forceRenderingOff survives the borrowed scripts, which only touch `enabled`.
                    borrowedRenderers[i].forceRenderingOff = true;
                }
            }

            for (int i = 0; i < borrowedProjectors.Length; i++)
            {
                if (borrowedProjectors[i])
                {
                    borrowedProjectors[i].enabled = false;
                }
            }
        }

        /// <summary>
        /// The pool's fire, smoke and a hard molten rim: the same standing flame every other patch of
        /// burning ground uses, with the ring the lava is held in drawn brighter so the pool has an edge
        /// rather than fading out into the grass.
        /// </summary>
        private void BuildFlames()
        {
            DeathwingEffects.AttachFlames(transform, radius);
            DeathwingEffects.AttachSmoke(transform, radius);

            Material material = DeathwingAssets.FlameParticleMaterial();
            if (material)
            {
                GameObject rim = new GameObject("DeathwingLavaRim");
                rim.transform.SetParent(transform, false);
                rim.transform.localPosition = Vector3.up * 0.25f;
                rim.transform.localScale = Vector3.one * radius;
                rim.AddComponent<MeshFilter>().sharedMesh = DeathwingEffects.RingMesh();
                MeshRenderer renderer = rim.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(material);
                DeathwingAssets.TrySetColor(renderer.sharedMaterial, "_TintColor", DeathwingAssets.fireCore * 1.6f);
                DeathwingAssets.TrySetColor(renderer.sharedMaterial, "_Color", DeathwingAssets.fireCore * 1.6f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                DeathwingEffects.Drape(rim, 0.25f);
            }

            DeathwingEffects.AttachDecal(transform, DeathwingEffects.DiscMesh(),
                new Color(0.02f, 0.01f, 0.01f, 0.8f), radius * 1.1f, 0f);
        }
    }
}
