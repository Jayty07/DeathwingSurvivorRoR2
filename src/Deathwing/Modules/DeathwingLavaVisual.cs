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

        private Renderer[] borrowedRenderers;
        private Projector[] borrowedProjectors;

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
        }

        private void Update()
        {
            // Re-applied every frame rather than once: the acid pool scales and re-enables its visuals as
            // it grows, so a single pass at spawn is undone a moment later.
            Suppress();
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
        /// A pool of flame licking upwards off the ground: fast, small, short-lived particles spread over
        /// the zone's footprint, cooling from yellow through orange to ember as they rise.
        /// </summary>
        private void BuildFlames()
        {
            Material material = DeathwingAssets.FlameParticleMaterial();
            if (!material)
            {
                return;
            }

            GameObject holder = new GameObject("DeathwingLavaFlames");
            holder.transform.SetParent(transform, false);

            ParticleSystem flames = holder.AddComponent<ParticleSystem>();
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = flames.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * radius, 0.5f * radius);
            main.startColor = new ParticleSystem.MinMaxGradient(DeathwingAssets.fireCore, DeathwingAssets.fireEdge);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = 20f + radius * 4f;

            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flames.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(DeathwingAssets.FireGradient());

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flames.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flames.Play();
        }
    }
}
