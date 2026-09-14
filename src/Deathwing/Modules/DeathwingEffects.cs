using R2API;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// The shared pieces his abilities are dressed with: scorched ground, standing fire, smoke, dust
    /// shockwaves and the lava-silhouette flash. Everything here is authored in code from a generated
    /// soft-disc texture and the game's own flame material, so nothing is lifted from another game.
    ///
    /// The networked effects are registered as effect prefabs and spawned through EffectManager, so a
    /// single call on the casting machine draws them for everyone in a lobby.
    /// </summary>
    internal static class DeathwingEffects
    {
        internal static GameObject groundFireEffect;
        internal static GameObject shockwaveEffect;
        internal static GameObject silhouetteEffect;

        private static Material softMaterialTemplate;
        private static Texture2D softTexture;
        private static Mesh discMesh;
        private static Mesh ringMesh;
        private static Mesh laneMesh;
        private static Mesh laneOutlineMesh;
        private static bool softMaterialSearched;

        /// <summary>Shaders that alpha-blend a tinted texture with vertex colour, in order of preference.</summary>
        private static readonly string[] softShaderNames =
        {
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended Premultiply",
            "Sprites/Default",
            "Unlit/Transparent"
        };

        internal static void Init()
        {
            groundFireEffect = CreateEffect<GroundFireEffect>("DeathwingGroundFireEffect");
            shockwaveEffect = CreateEffect<ShockwaveEffect>("DeathwingShockwaveEffect");
            silhouetteEffect = CreateEffect<SilhouetteEffect>("DeathwingSilhouetteEffect");
        }

        private static GameObject CreateEffect<T>(string name) where T : MonoBehaviour
        {
            GameObject source = new GameObject(name);
            EffectComponent effectComponent = source.AddComponent<EffectComponent>();
            effectComponent.applyScale = false;
            effectComponent.positionAtReferencedTransform = false;
            effectComponent.parentToReferencedTransform = false;
            VFXAttributes attributes = source.AddComponent<VFXAttributes>();
            attributes.vfxPriority = VFXAttributes.VFXPriority.Medium;
            // Built fresh every time: these author their own children from the effect data, which a
            // pooled instance would carry over from its last use.
            attributes.DoNotPool = true;
            source.AddComponent<T>();

            GameObject prefab = PrefabAPI.InstantiateClone(source, name, false);
            Object.Destroy(source);

            if (!ContentAddition.AddEffect(prefab))
            {
                Log.Warning($"Effect '{name}' could not be registered.");
                return null;
            }

            return prefab;
        }

        /// <summary>Camera shake strength from the config; zero turns his shakes off entirely.</summary>
        internal static float ShakeScale => Tuning.cameraShake != null ? Mathf.Max(0f, Tuning.cameraShake.Value) : 1f;

        /// <summary>
        /// Burning, scorched ground for everyone. A circle when <paramref name="length"/> is zero,
        /// otherwise a lane of that length running along <paramref name="direction"/> from the origin.
        /// </summary>
        internal static void SpawnGroundFire(Vector3 origin, float radius, float lifetime, GameObject owner,
            Vector3 direction = default, float length = 0f)
        {
            if (!groundFireEffect)
            {
                return;
            }

            Quaternion rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z).normalized)
                : Quaternion.identity;

            EffectManager.SpawnEffect(groundFireEffect, new EffectData
            {
                origin = origin,
                rotation = rotation,
                scale = radius,
                genericFloat = lifetime,
                genericUInt = (uint)Mathf.RoundToInt(Mathf.Max(0f, length) * 10f),
                rootObject = owner
            }, true);
        }

        /// <summary>
        /// A ring racing out over the ground from the origin, with rocks thrown up behind it. Dust by
        /// default; <paramref name="fire"/> makes it a wall of flame instead.
        /// </summary>
        internal static void SpawnShockwave(Vector3 origin, float radius, float shake, GameObject owner, bool fire = false)
        {
            if (!shockwaveEffect)
            {
                return;
            }

            EffectManager.SpawnEffect(shockwaveEffect, new EffectData
            {
                origin = origin,
                rotation = Quaternion.identity,
                scale = radius,
                genericFloat = shake,
                genericUInt = fire ? 1u : 0u,
                rootObject = owner
            }, true);
        }

        /// <summary>Lights the dragon up from the inside for a moment, as his lava silhouette in Heroes.</summary>
        internal static void SpawnSilhouette(GameObject body, float duration)
        {
            if (!silhouetteEffect || !body)
            {
                return;
            }

            EffectManager.SpawnEffect(silhouetteEffect, new EffectData
            {
                origin = body.transform.position,
                genericFloat = duration,
                rootObject = body
            }, true);
        }

        /// <summary>
        /// A tinted alpha-blended material drawn with a soft radial disc, the workhorse for smoke, dust
        /// and scorch decals. Null if the build ships none of the shaders it can use.
        /// </summary>
        internal static Material SoftMaterial(Color tint)
        {
            if (!softMaterialSearched)
            {
                softMaterialSearched = true;
                foreach (string shaderName in softShaderNames)
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader)
                    {
                        softMaterialTemplate = new Material(shader)
                        {
                            name = "matDeathwingSoft",
                            hideFlags = HideFlags.DontUnloadUnusedAsset,
                            mainTexture = SoftTexture()
                        };
                        break;
                    }
                }

                if (!softMaterialTemplate)
                {
                    Log.Warning("No alpha-blended particle shader found; smoke and scorch decals are skipped.");
                }
            }

            if (!softMaterialTemplate)
            {
                return null;
            }

            Material material = new Material(softMaterialTemplate);
            if (material.HasProperty("_TintColor"))
            {
                material.SetColor("_TintColor", tint);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            return material;
        }

        /// <summary>A white disc that fades to transparent at its edge, so quads read as soft puffs.</summary>
        private static Texture2D SoftTexture()
        {
            if (softTexture)
            {
                return softTexture;
            }

            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "texDeathwingSoft",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            softTexture.SetPixels(pixels);
            softTexture.Apply(false, true);
            return softTexture;
        }

        /// <summary>A flat unit disc whose vertex alpha falls off towards the rim.</summary>
        internal static Mesh DiscMesh()
        {
            if (discMesh)
            {
                return discMesh;
            }

            discMesh = BuildRadial(0f, 1f, 8, t => Mathf.Lerp(1f, 0f, t * t), "DeathwingDisc");
            return discMesh;
        }

        /// <summary>A thin flat ring of unit radius, soft on both edges.</summary>
        internal static Mesh RingMesh()
        {
            if (ringMesh)
            {
                return ringMesh;
            }

            ringMesh = BuildRadial(0.75f, 1f, 2, t => 1f - Mathf.Abs(t * 2f - 1f), "DeathwingRing");
            return ringMesh;
        }

        /// <summary>
        /// A flat unit lane: two wide (x from -1 to 1) and one long (z from 0 to 1), its alpha falling
        /// off towards the long edges and the far end so it reads as a strip, not an oval.
        /// </summary>
        internal static Mesh LaneMesh()
        {
            if (laneMesh)
            {
                return laneMesh;
            }

            laneMesh = BuildLane((u, v) =>
            {
                float side = 1f - Mathf.Pow(Mathf.Abs(u * 2f - 1f), 3f);
                float ends = Mathf.Min(1f, Mathf.Min(v, 1f - v) * 8f);
                return side * ends;
            }, "DeathwingLane");
            return laneMesh;
        }

        /// <summary>The border of a lane: a hard band along its two long edges and across both ends.</summary>
        internal static Mesh LaneOutlineMesh()
        {
            if (laneOutlineMesh)
            {
                return laneOutlineMesh;
            }

            laneOutlineMesh = BuildLane((u, v) =>
            {
                float fromSide = (1f - Mathf.Abs(u * 2f - 1f)) / 0.2f;
                float fromEnd = Mathf.Min(v, 1f - v) / 0.03f;
                return Mathf.Clamp01(1f - Mathf.Min(fromSide, fromEnd));
            }, "DeathwingLaneOutline");
            return laneOutlineMesh;
        }

        private static Mesh BuildLane(System.Func<float, float, float> alpha, string name)
        {
            const int across = 10;
            const int along = 48;
            Vector3[] vertices = new Vector3[(across + 1) * (along + 1)];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int j = 0; j <= along; j++)
            {
                float v = j / (float)along;
                for (int i = 0; i <= across; i++)
                {
                    float u = i / (float)across;
                    int index = j * (across + 1) + i;
                    vertices[index] = new Vector3(u * 2f - 1f, 0f, v);
                    colors[index] = new Color(1f, 1f, 1f, alpha(u, v));
                    uvs[index] = new Vector2(u, v);
                }
            }

            int[] triangles = new int[across * along * 6];
            int t = 0;
            for (int j = 0; j < along; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int a = j * (across + 1) + i;
                    int b = a + across + 1;
                    triangles[t++] = a;
                    triangles[t++] = b + 1;
                    triangles[t++] = b;
                    triangles[t++] = a;
                    triangles[t++] = a + 1;
                    triangles[t++] = b + 1;
                }
            }

            return FinishMesh(vertices, colors, uvs, triangles, name);
        }

        /// <summary>
        /// A disc or annulus of unit outer radius built from <paramref name="steps"/> concentric bands,
        /// dense enough to be draped over uneven ground. <paramref name="alpha"/> gives the vertex alpha
        /// from the inner edge (0) to the outer (1).
        /// </summary>
        private static Mesh BuildRadial(float inner, float outer, int steps, System.Func<float, float> alpha, string name)
        {
            const int segments = 48;
            bool hasHole = inner > 0f;
            int rings = steps + 1;
            int centre = hasHole ? 0 : 1;
            Vector3[] vertices = new Vector3[segments * rings + centre];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];

            if (!hasHole)
            {
                vertices[0] = Vector3.zero;
                colors[0] = new Color(1f, 1f, 1f, alpha(0f));
            }

            for (int ring = 0; ring < rings; ring++)
            {
                float t = hasHole ? ring / (float)steps : (ring + 1) / (float)rings;
                float radius = Mathf.Lerp(inner, outer, t);
                Color colour = new Color(1f, 1f, 1f, alpha(t));
                for (int i = 0; i < segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    int index = centre + ring * segments + i;
                    vertices[index] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    colors[index] = colour;
                }
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].x * 0.5f + 0.5f, vertices[i].z * 0.5f + 0.5f);
            }

            int quadRings = rings - 1;
            int[] triangles = new int[segments * quadRings * 6 + (hasHole ? 0 : segments * 3)];
            int t2 = 0;

            if (!hasHole)
            {
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles[t2++] = 0;
                    triangles[t2++] = 1 + next;
                    triangles[t2++] = 1 + i;
                }
            }

            for (int ring = 0; ring < quadRings; ring++)
            {
                int a = centre + ring * segments;
                int b = centre + (ring + 1) * segments;
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles[t2++] = a + i;
                    triangles[t2++] = b + next;
                    triangles[t2++] = b + i;
                    triangles[t2++] = a + i;
                    triangles[t2++] = a + next;
                    triangles[t2++] = b + next;
                }
            }

            return FinishMesh(vertices, colors, uvs, triangles, name);
        }

        private static Mesh FinishMesh(Vector3[] vertices, Color[] colors, Vector2[] uvs, int[] triangles, string name)
        {
            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A flat decal on the ground, pushed just above it, sized to a circle or stretched into a lane.
        /// Returns the renderer so the owner can fade it.
        /// </summary>
        internal static MeshRenderer AttachDecal(Transform parent, Mesh mesh, Color tint, float radius, float length)
        {
            Material material = SoftMaterial(tint);
            if (!material)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingDecal");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = Vector3.up * 0.15f;
            if (length > 0f)
            {
                // A lane is a strip with its ends softened, not a disc stretched into an oval.
                mesh = LaneMesh();
                holder.transform.localScale = new Vector3(radius, 1f, length + radius);
                holder.transform.localPosition += Vector3.back * (radius * 0.5f);
            }
            else
            {
                holder.transform.localScale = new Vector3(radius, 1f, radius);
            }

            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Drape(holder, 0.15f);
            return renderer;
        }

        /// <summary>
        /// Makes a flat ground mesh follow the terrain under it, as a projected decal would: every
        /// vertex is dropped onto the surface beneath it and lifted clear by <paramref name="clearance"/>.
        /// </summary>
        internal static GroundDrape Drape(GameObject holder, float clearance)
        {
            GroundDrape drape = holder.AddComponent<GroundDrape>();
            drape.clearance = clearance;
            return drape;
        }

        /// <summary>
        /// The warning drawn where an area is about to go up: a hard molten ring closing in on the area
        /// over <paramref name="duration"/>, with embers rising inside it. A circle, or a lane along
        /// <paramref name="direction"/> when a length is given. Removes itself when the time is up.
        /// </summary>
        internal static GameObject Telegraph(Vector3 origin, float radius, float duration,
            Vector3 direction = default, float length = 0f)
        {
            Material flame = DeathwingAssets.FlameParticleMaterial();
            if (!flame)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingTelegraph");
            holder.transform.position = origin;
            if (direction.sqrMagnitude > 0.001f)
            {
                holder.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z).normalized);
            }

            GameObject rim = new GameObject("Rim");
            rim.transform.SetParent(holder.transform, false);
            rim.transform.localPosition = Vector3.up * 0.2f;
            rim.AddComponent<MeshFilter>().sharedMesh = length > 0f ? LaneOutlineMesh() : RingMesh();
            MeshRenderer rimRenderer = rim.AddComponent<MeshRenderer>();
            rimRenderer.sharedMaterial = new Material(flame);
            DeathwingAssets.TrySetColor(rimRenderer.sharedMaterial, "_TintColor", DeathwingAssets.fireCore * 1.8f);
            DeathwingAssets.TrySetColor(rimRenderer.sharedMaterial, "_Color", DeathwingAssets.fireCore * 1.8f);
            rimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rimRenderer.receiveShadows = false;
            Drape(rim, 0.2f);

            AttachDecal(holder.transform, DiscMesh(), new Color(0.5f, 0.12f, 0.02f, 0.35f), radius, length);

            ParticleSystem embers = AttachEmbers(holder.transform,
                new Vector3(radius * 2f, 0.5f, length > 0f ? length : radius * 2f), Mathf.Clamp(radius * 3f, 10f, 60f));
            if (embers)
            {
                embers.transform.localPosition = length > 0f ? Vector3.forward * (length * 0.5f) : Vector3.zero;
            }

            TelegraphEffect telegraph = holder.AddComponent<TelegraphEffect>();
            telegraph.rim = rim.transform;
            telegraph.radius = radius;
            telegraph.length = length;
            telegraph.duration = Mathf.Max(0.05f, duration);
            return holder;
        }

        /// <summary>Configures a system's shape to cover a circle, or a lane when a length is given.</summary>
        internal static void ShapeArea(ParticleSystem system, float radius, float length)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            if (length > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(radius * 2f, 0.1f, length);
                shape.position = Vector3.forward * (length * 0.5f);
                shape.rotation = Vector3.zero;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius;
                shape.rotation = new Vector3(90f, 0f, 0f);
                shape.position = Vector3.zero;
            }
        }

        /// <summary>
        /// Upright tongues of fire over an area: the pool fire, moved here so a trench, a lane and a
        /// ring can all burn with the same flame.
        /// </summary>
        internal static ParticleSystem AttachFlames(Transform parent, float radius, float length = 0f, float sizeScale = 1f)
        {
            Material material = DeathwingAssets.FlameParticleMaterial();
            if (!material)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingFlames");
            holder.transform.SetParent(parent, false);

            ParticleSystem flames = holder.AddComponent<ParticleSystem>();
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float area = length > 0f ? radius * 2f * length : Mathf.PI * radius * radius;
            float extent = Mathf.Max(radius, length > 0f ? Mathf.Sqrt(area) * 0.5f : radius);

            ParticleSystem.MainModule main = flames.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f * extent * sizeScale, 0.22f * extent * sizeScale);
            main.startColor = new ParticleSystem.MinMaxGradient(
                DeathwingAssets.fireCore * 2.2f,
                DeathwingAssets.fireEdge * 2.2f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = flames.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Clamp(area * 0.25f, 8f, 90f);

            ShapeArea(flames, radius, length);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flames.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(flameGradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flames.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = true;
            AnimationCurve footprint = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.7f), new Keyframe(1f, 0f));
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(1f, footprint);
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1.5f), new Keyframe(0.85f, 1.6f), new Keyframe(1f, 0f)));
            sizeOverLifetime.z = new ParticleSystem.MinMaxCurve(1f, footprint);

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(DeathwingRocks.FlameMeshes());
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flames.Play();
            return flames;
        }

        /// <summary>Slow dark smoke rising off an area, drawn as soft billboards.</summary>
        internal static ParticleSystem AttachSmoke(Transform parent, float radius, float length = 0f,
            float rate = -1f, Color? colour = null)
        {
            Material material = SoftMaterial(colour ?? new Color(0.08f, 0.06f, 0.05f, 0.55f));
            if (!material)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingSmoke");
            holder.transform.SetParent(parent, false);

            ParticleSystem smoke = holder.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float area = length > 0f ? radius * 2f * length : Mathf.PI * radius * radius;

            ParticleSystem.MainModule main = smoke.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.enabled = true;
            emission.rateOverTime = rate > 0f ? rate : Mathf.Clamp(area * 0.06f, 3f, 30f);

            ShapeArea(smoke, radius * 0.8f, length);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.6f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));

            ParticleSystem.RotationOverLifetimeModule rotation = smoke.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -10f;

            smoke.Play();
            return smoke;
        }

        /// <summary>
        /// Embers drawn inwards from a sphere around the holder to its centre: the breath being gathered.
        /// The sphere's radius and each ember's lifetime are matched so they arrive as they die.
        /// </summary>
        internal static ParticleSystem AttachIntake(Transform parent, float radius, float rate)
        {
            Material material = DeathwingAssets.FlameParticleMaterial();
            if (!material)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingIntake");
            holder.transform.SetParent(parent, false);

            ParticleSystem intake = holder.AddComponent<ParticleSystem>();
            intake.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            const float travel = 0.5f;
            ParticleSystem.MainModule main = intake.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(travel);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-radius / travel);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.03f, radius * 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                DeathwingAssets.fireCore * 2.5f, DeathwingAssets.fireEdge * 2.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = intake.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = intake.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.3f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = intake.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(1f, 1.2f)));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = intake.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 1f)
                }
            });

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            intake.Play();
            return intake;
        }

        /// <summary>
        /// Glowing embers drifting up out of a volume, as off his lava seams. Billboarded sparks on the
        /// flame material, so they glow rather than sit flat.
        /// </summary>
        internal static ParticleSystem AttachEmbers(Transform parent, Vector3 boxSize, float rate)
        {
            Material material = DeathwingAssets.FlameParticleMaterial();
            if (!material)
            {
                return null;
            }

            GameObject holder = new GameObject("DeathwingEmbers");
            holder.transform.SetParent(parent, false);

            ParticleSystem embers = holder.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = embers.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                DeathwingAssets.fireCore * 2.5f, DeathwingAssets.fireEdge * 2.5f);
            main.gravityModifier = -0.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = embers.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = embers.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = embers.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(flameGradient);

            ParticleSystem.NoiseModule noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.3f;

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            embers.Play();
            return embers;
        }

        /// <summary>One puff of dust kicked up at a point, drawn on this client only.</summary>
        internal static void DustPuff(Vector3 position, float size, float lifetime = 1.6f)
        {
            Material material = SoftMaterial(new Color(0.45f, 0.4f, 0.34f, 0.5f));
            if (!material)
            {
                return;
            }

            GameObject holder = new GameObject("DeathwingDust");
            holder.transform.position = position;

            ParticleSystem dust = holder.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = dust.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;

            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(size * 3f, 6f, 20f)) });

            ParticleSystem.ShapeModule shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = size * 0.3f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dust.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f)
                }
            });

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = dust.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.8f)));

            ParticleSystem.LimitVelocityOverLifetimeModule drag = dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.4f;

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            dust.Play();
            Object.Destroy(holder, lifetime + 0.5f);
        }

        /// <summary>Colour and opacity over a flame's life: catches quickly, burns yellow-hot, cools to ember.</summary>
        internal static readonly Gradient flameGradient = new Gradient
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

    /// <summary>
    /// Ground he has set alight: a scorch mark that stays, fire that burns for the lifetime and dies
    /// down over its last stretch, smoke, and rock thrown up around it. Reads its size and shape from the
    /// effect data it was spawned with.
    /// </summary>
    public class GroundFireEffect : MonoBehaviour
    {
        private const float dieDownDuration = 1.5f;
        private const float scorchLinger = 6f;

        private float lifetime;
        private float age;
        private bool built;
        private bool dyingDown;
        private ParticleSystem flames;
        private ParticleSystem smoke;
        private MeshRenderer scorch;
        private Color scorchColour;

        private void Start()
        {
            EffectComponent effect = GetComponent<EffectComponent>();
            EffectData data = effect ? effect.effectData : null;
            if (data == null)
            {
                Destroy(gameObject);
                return;
            }

            float radius = Mathf.Max(0.5f, data.scale);
            float length = data.genericUInt / 10f;
            lifetime = Mathf.Max(0.5f, data.genericFloat);
            transform.rotation = data.rotation;

            scorchColour = new Color(0.02f, 0.01f, 0.01f, 0.85f);
            scorch = DeathwingEffects.AttachDecal(transform, DeathwingEffects.DiscMesh(), scorchColour, radius, length);
            flames = DeathwingEffects.AttachFlames(transform, radius, length);
            smoke = DeathwingEffects.AttachSmoke(transform, radius, length);

            float area = length > 0f ? radius * 2f * length : Mathf.PI * radius * radius;
            int rocks = Mathf.Clamp(Mathf.RoundToInt(area / 30f), 3, 40);
            if (length > 0f)
            {
                for (int i = 0; i < rocks; i++)
                {
                    Vector3 along = transform.position + transform.forward * Random.Range(0f, length)
                        + transform.right * Random.Range(-radius, radius);
                    DeathwingRocks.Scatter(along, 1f, 1, radius * 0.25f, lifetime + scorchLinger);
                }
            }
            else
            {
                DeathwingRocks.Scatter(transform.position, radius * 0.9f, rocks, radius * 0.18f, lifetime + scorchLinger);
            }

            built = true;
            Destroy(gameObject, lifetime + scorchLinger + 0.5f);
        }

        private void Update()
        {
            if (!built)
            {
                return;
            }

            age += Time.deltaTime;

            if (!dyingDown && age >= lifetime - dieDownDuration)
            {
                dyingDown = true;
                if (flames)
                {
                    flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                if (smoke)
                {
                    ParticleSystem.EmissionModule emission = smoke.emission;
                    emission.rateOverTimeMultiplier *= 0.4f;
                }
            }

            if (age >= lifetime && smoke && smoke.isEmitting && age >= lifetime + 2f)
            {
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // The scorch stays after the fire is out and fades away in the last seconds.
            if (scorch && age > lifetime)
            {
                float fade = 1f - Mathf.Clamp01((age - lifetime) / scorchLinger);
                Color colour = scorchColour;
                colour.a *= fade;
                Material material = scorch.material;
                if (material.HasProperty("_TintColor"))
                {
                    material.SetColor("_TintColor", colour);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }
            }
        }
    }

    /// <summary>
    /// A ring racing outwards over the ground, with a dust cloud and rock thrown up where it started
    /// and a camera kick for anyone nearby. As fire it becomes Incinerate's wall of flame.
    /// </summary>
    public class ShockwaveEffect : MonoBehaviour
    {
        private const float expandDuration = 0.45f;

        private Transform ring;
        private MeshRenderer ringRenderer;
        private Color ringColour;
        private float radius;
        private float age;
        private bool built;

        private void Start()
        {
            EffectComponent effect = GetComponent<EffectComponent>();
            EffectData data = effect ? effect.effectData : null;
            if (data == null)
            {
                Destroy(gameObject);
                return;
            }

            radius = Mathf.Max(1f, data.scale);
            bool fire = data.genericUInt == 1u;
            float shake = data.genericFloat * DeathwingEffects.ShakeScale;

            ringColour = fire
                ? new Color(1f, 0.45f, 0.1f, 0.9f)
                : new Color(0.55f, 0.5f, 0.42f, 0.7f);
            Mesh mesh = DeathwingEffects.RingMesh();
            Material material = fire ? DeathwingAssets.FlameParticleMaterial() : DeathwingEffects.SoftMaterial(ringColour);
            if (material)
            {
                GameObject holder = new GameObject("DeathwingShockRing");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = Vector3.up * 0.4f;
                holder.transform.localScale = Vector3.one * (radius * 0.15f);
                holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                ringRenderer = holder.AddComponent<MeshRenderer>();
                ringRenderer.sharedMaterial = fire ? new Material(material) : material;
                ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ringRenderer.receiveShadows = false;
                DeathwingEffects.Drape(holder, 0.4f);
                ring = holder.transform;
                if (fire)
                {
                    DeathwingAssets.TrySetColor(ringRenderer.sharedMaterial, "_TintColor", DeathwingAssets.fireCore * 2f);
                    DeathwingAssets.TrySetColor(ringRenderer.sharedMaterial, "_Color", DeathwingAssets.fireCore * 2f);
                }
            }

            if (fire)
            {
                ParticleSystem flames = DeathwingEffects.AttachFlames(transform, radius, 0f, 0.7f);
                if (flames)
                {
                    ParticleSystem.ShapeModule shape = flames.shape;
                    shape.radiusThickness = 0.25f;
                    ParticleSystem.EmissionModule emission = flames.emission;
                    emission.rateOverTime = Mathf.Clamp(radius * 6f, 20f, 120f);
                    flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    ParticleSystem.MainModule main = flames.main;
                    main.loop = false;
                    main.duration = 0.6f;
                    flames.Play();
                }

                DeathwingEffects.AttachSmoke(transform, radius * 0.7f, 0f, radius * 2f)
                    ?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else
            {
                DeathwingEffects.DustPuff(transform.position, radius * 0.45f, 2.2f);
                DeathwingRocks.Scatter(transform.position, radius * 0.5f, Mathf.Clamp(Mathf.RoundToInt(radius), 4, 14),
                    radius * 0.12f, 8f);
            }

            if (shake > 0f)
            {
                GameObject emitterObject = new GameObject("DeathwingShakeEmitter");
                emitterObject.transform.position = transform.position;
                ShakeEmitter emitter = emitterObject.AddComponent<ShakeEmitter>();
                emitter.wave = new Wave { amplitude = shake, frequency = 40f, cycleOffset = 0f };
                emitter.duration = 0.45f;
                emitter.radius = radius + 40f;
                emitter.amplitudeTimeDecay = true;
                emitter.shakeOnStart = true;
                Destroy(emitterObject, 1f);
            }

            built = true;
            Destroy(gameObject, 4f);
        }

        private void Update()
        {
            if (!built || !ring)
            {
                return;
            }

            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / expandDuration);
            float eased = 1f - (1f - t) * (1f - t);
            ring.localScale = Vector3.one * Mathf.Lerp(radius * 0.15f, radius * 1.15f, eased);

            Color colour = ringColour;
            colour.a *= 1f - t;
            Material material = ringRenderer.sharedMaterial;
            DeathwingAssets.TrySetColor(material, "_TintColor", colour);
            DeathwingAssets.TrySetColor(material, "_Color", colour);
            if (t >= 1f)
            {
                ringRenderer.enabled = false;
            }
        }
    }

    /// <summary>Asks the dragon's model on this client to flash to lava for the given time.</summary>
    public class SilhouetteEffect : MonoBehaviour
    {
        private void Start()
        {
            EffectComponent effect = GetComponent<EffectComponent>();
            EffectData data = effect ? effect.effectData : null;
            GameObject body = data?.rootObject;
            if (body)
            {
                ModelLocator locator = body.GetComponent<ModelLocator>();
                Transform model = locator ? locator.modelTransform : null;
                DeathwingCustomModel custom = model ? model.GetComponent<DeathwingCustomModel>() : null;
                if (custom)
                {
                    custom.Silhouette(Mathf.Max(0.2f, data.genericFloat));
                }
            }

            Destroy(gameObject, 0.1f);
        }
    }
}

namespace Deathwing.Modules
{
    /// <summary>
    /// Drapes a flat mesh over the ground beneath it. Unity's decal projector is not in the game's
    /// build, so the projection is done by hand: each vertex of the mesh is cast straight down from
    /// above onto the world and moved to where it lands, a little clear of the surface. The mesh is
    /// re-draped whenever its holder moves or scales, so closing rings and a marker that follows the
    /// aim keep hugging slopes, steps and rocks instead of cutting through them.
    /// </summary>
    public class GroundDrape : MonoBehaviour
    {
        public float clearance = 0.15f;
        public float reach = 8f;

        private MeshFilter filter;
        private Mesh template;
        private Mesh draped;
        private Vector3[] source;
        private Vector3[] work;
        private Matrix4x4 placed;

        private void LateUpdate()
        {
            if (!filter)
            {
                filter = GetComponent<MeshFilter>();
                if (!filter)
                {
                    return;
                }
            }

            Mesh current = filter.sharedMesh;
            if (!current)
            {
                return;
            }

            if (current != draped)
            {
                template = current;
                source = template.vertices;
                work = new Vector3[source.Length];
                if (draped)
                {
                    Destroy(draped);
                }

                draped = Instantiate(template);
                draped.name = template.name + "Draped";
                draped.MarkDynamic();
                filter.sharedMesh = draped;
                placed = Matrix4x4.zero;
            }

            Matrix4x4 now = transform.localToWorldMatrix;
            if (now == placed)
            {
                return;
            }

            placed = now;
            Redrape();
        }

        private void Redrape()
        {
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 flat = source[i];
                flat.y = 0f;
                Vector3 world = transform.TransformPoint(flat);
                Vector3 rest = world + Vector3.up * clearance;

                if (Physics.Raycast(world + Vector3.up * reach, Vector3.down, out RaycastHit hit, reach * 2f,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    rest = hit.point + Vector3.up * clearance;
                }

                work[i] = transform.InverseTransformPoint(rest);
            }

            draped.vertices = work;
            draped.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (draped)
            {
                Destroy(draped);
            }
        }
    }

    /// <summary>The closing ring of a telegraph; everything else on the holder just sits until it is destroyed.</summary>
    public class TelegraphEffect : MonoBehaviour
    {
        public Transform rim;
        public float radius;
        public float length;
        public float duration;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);

            if (rim)
            {
                // Starts wide and settles onto the real edge just as the area goes up.
                float ring = radius * Mathf.Lerp(1.5f, 1f, t);
                float pulse = 1f + 0.06f * Mathf.Sin(age * 18f);
                rim.localScale = length > 0f
                    ? new Vector3(ring * pulse, 1f, length + ring * pulse)
                    : Vector3.one * (ring * pulse);
                rim.localPosition = Vector3.up * 0.2f + (length > 0f ? Vector3.back * (ring * pulse * 0.5f) : Vector3.zero);
            }

            if (age >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
