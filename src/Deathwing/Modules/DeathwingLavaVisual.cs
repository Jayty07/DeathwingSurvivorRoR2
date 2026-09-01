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
        /// Fire rooted in the ground: each particle is planted where it spawns, stretches upwards as it
        /// burns and fades out rather than travelling. Moving particles read as debris being thrown around
        /// instead of ground that is on fire, and it is drawn as an upright mesh so a flame can never lie
        /// flat or come out angled the way a borrowed sprite does.
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
            // Small enough to sit in the grass rather than block the view of what is standing in the pool.
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f * radius, 0.22f * radius);
            // Overbright so the additive material glows rather than washing into the ground.
            main.startColor = new ParticleSystem.MinMaxGradient(
                DeathwingAssets.fireCore * 2.2f,
                DeathwingAssets.fireEdge * 2.2f);
            main.startRotation3D = true;
            // Spun about the vertical axis only, so each flame is a different silhouette while still
            // standing upright.
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f + radius * 2f;

            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flames.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(flameGradient);

            // Separate axes: the flame grows tall from a fixed footprint rather than swelling in every
            // direction, which is what makes it look anchored to the ground, and it collapses at the end of
            // its life so the fade-out is a flame dying down rather than a shape blinking away.
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flames.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = true;
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.7f, 0.7f),
                    new Keyframe(1f, 0f)));
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.5f, 1.5f),
                    new Keyframe(0.85f, 1.6f),
                    new Keyframe(1f, 0f)));
            sizeOverLifetime.z = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.7f, 0.7f),
                    new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            // Mesh particles rather than sprites: the flame sprites the game ships with are drawn at an
            // angle inside their texture, so no billboard mode stands them upright. A generated spike keeps
            // the orientation it was built with.
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            // Several widths, picked per particle: a pool of identical spikes looks like spears, while a mix
            // of sharp tongues and broad sheets covers the ground and reads as fire.
            renderer.SetMeshes(DeathwingRocks.FlameMeshes());
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flames.Play();
        }

        /// <summary>
        /// Colour and opacity over a flame's life: it catches quickly, burns yellow-hot, then cools to
        /// ember as it fades out completely. Shared, because a pool never changes it.
        /// </summary>
        private static readonly Gradient flameGradient = new Gradient
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
