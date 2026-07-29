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
        /// Fire rooted in the ground: each particle is planted where it spawns, stretches upwards as it
        /// burns and fades out rather than travelling. Moving particles read as debris being thrown around
        /// instead of ground that is on fire, and the flame sprite is only convincing while it stands
        /// upright, so it is stretched on Y alone and kept vertical rather than facing the camera.
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * radius, 0.55f * radius);
            main.startColor = new ParticleSystem.MinMaxGradient(DeathwingAssets.fireCore, DeathwingAssets.fireEdge);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = 20f + radius * 4f;

            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Fades to nothing so a flame dies out instead of vanishing mid-burn.
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flames.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FlameGradient());

            // Separate axes: the flame grows tall from a fixed footprint rather than swelling in every
            // direction, which is what makes it look anchored to the ground.
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flames.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = true;
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.45f, 1.4f),
                    new Keyframe(1f, 1.7f)));
            sizeOverLifetime.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            // Billboards that stay upright: view alignment let the sprite lie flat on the ground when the
            // camera looked down at the pool, which is why the flames appeared to be on their side.
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.pivot = new Vector3(0f, 0.5f, 0f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flames.Play();
        }

        /// <summary>
        /// Colour and opacity over a flame's life: it catches quickly, burns yellow-hot, then cools to
        /// ember as it fades out completely.
        /// </summary>
        private static Gradient FlameGradient()
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(DeathwingAssets.fireCore, 0.3f),
                    new GradientColorKey(DeathwingAssets.fireEdge, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.55f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            };
        }
    }
}
