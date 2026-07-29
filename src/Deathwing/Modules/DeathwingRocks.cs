using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Cooling rock for Deathwing's burning ground. The meshes are generated rather than borrowed: the
    /// game's rock props are scattered across stage-specific scenes rather than sitting behind a stable
    /// address, and a low-poly chunk is a handful of vertices to build. Their material is taken off the
    /// chassis so it uses a shader the game is already rendering with, retinted to charred stone.
    /// </summary>
    internal static class DeathwingRocks
    {
        private static readonly Color rockAlbedo = new Color(0.14f, 0.11f, 0.10f);
        private static readonly Color rockEmission = new Color(1.6f, 0.35f, 0.05f);

        private static readonly Color flameAlbedo = new Color(0.22f, 0.03f, 0.01f);
        private static readonly Color flameEmission = new Color(6.5f, 1.7f, 0.2f);

        private static Material rockMaterial;
        private static Material flameMaterial;
        private static bool materialsResolved;

        internal static Material RockMaterial()
        {
            ResolveMaterials();
            return rockMaterial;
        }

        internal static Material FlameMaterial()
        {
            ResolveMaterials();
            return flameMaterial;
        }

        /// <summary>
        /// Builds both materials off the chassis' own. A body prefab's renderers have no material assigned:
        /// the game applies them from the character model's renderer infos when the body spawns, so reading
        /// `sharedMaterial` off the prefab returns nothing and has to be read from those infos instead.
        /// </summary>
        private static void ResolveMaterials()
        {
            if (materialsResolved)
            {
                return;
            }

            materialsResolved = true;

            Material template = ChassisMaterial();
            if (!template)
            {
                Log.Warning("No chassis material resolved; burning ground will have no rock or flame meshes.");
                return;
            }

            rockMaterial = DeathwingAssets.TintedCopy(
                template,
                "matDeathwingScorchedRock",
                rockAlbedo,
                rockEmission,
                2f);
            flameMaterial = DeathwingAssets.TintedCopy(
                template,
                "matDeathwingLavaFlame",
                flameAlbedo,
                flameEmission,
                9f);

            Log.Info($"Burning ground materials built from '{template.name}'.");
        }

        private static Material ChassisMaterial()
        {
            GameObject commandoBody = DeathwingAssets.Load<GameObject>(DeathwingAssets.commandoBodyKey);
            if (!commandoBody)
            {
                return null;
            }

            CharacterModel model = commandoBody.GetComponentInChildren<CharacterModel>(true);
            if (model != null && model.baseRendererInfos != null)
            {
                foreach (CharacterModel.RendererInfo info in model.baseRendererInfos)
                {
                    if (info.defaultMaterial)
                    {
                        return info.defaultMaterial;
                    }

                    if (info.renderer && info.renderer.sharedMaterial)
                    {
                        return info.renderer.sharedMaterial;
                    }
                }
            }

            foreach (Renderer renderer in commandoBody.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is ParticleSystemRenderer) && renderer.sharedMaterial)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        /// <summary>
        /// A tapered spike standing on the origin, used as the mesh for the flame particles. Mesh particles
        /// keep the mesh's own orientation, so unlike a billboard they cannot end up lying flat or angled
        /// with the sprite they were drawn from.
        /// </summary>
        internal static Mesh FlameMesh()
        {
            const int sides = 5;
            Vector3[] vertices = new Vector3[sides * 3];
            int[] triangles = new int[sides * 3];
            Vector3 tip = new Vector3(0f, 1f, 0f);

            for (int i = 0; i < sides; i++)
            {
                float from = i / (float)sides * Mathf.PI * 2f;
                float to = (i + 1) / (float)sides * Mathf.PI * 2f;
                int baseVertex = i * 3;

                vertices[baseVertex] = new Vector3(Mathf.Cos(from) * 0.22f, 0f, Mathf.Sin(from) * 0.22f);
                vertices[baseVertex + 1] = new Vector3(Mathf.Cos(to) * 0.22f, 0f, Mathf.Sin(to) * 0.22f);
                vertices[baseVertex + 2] = tip;

                triangles[baseVertex] = baseVertex;
                triangles[baseVertex + 1] = baseVertex + 2;
                triangles[baseVertex + 2] = baseVertex + 1;
            }

            Mesh mesh = new Mesh
            {
                name = "meshDeathwingFlame",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A cube whose corners are pulled about at random, which reads as a chunk of stone at the scale
        /// these are used and matches the game's faceted art. Vertices are not shared between faces so the
        /// faces stay flat-shaded.
        /// </summary>
        internal static Mesh RockMesh(int seed)
        {
            Random.State state = Random.state;
            Random.InitState(seed);

            Vector3[] corners =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] += new Vector3(
                    Random.Range(-0.22f, 0.22f),
                    Random.Range(-0.22f, 0.22f),
                    Random.Range(-0.22f, 0.22f));

                // Flattened and splayed: rock half-buried in the ground rather than a floating dice.
                corners[i].y *= corners[i].y > 0f ? 0.75f : 1.6f;
            }

            int[][] faces =
            {
                new[] { 0, 1, 2, 3 },
                new[] { 7, 6, 5, 4 },
                new[] { 4, 5, 1, 0 },
                new[] { 6, 7, 3, 2 },
                new[] { 5, 6, 2, 1 },
                new[] { 7, 4, 0, 3 }
            };

            Vector3[] vertices = new Vector3[faces.Length * 4];
            int[] triangles = new int[faces.Length * 6];

            for (int face = 0; face < faces.Length; face++)
            {
                int baseVertex = face * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices[baseVertex + corner] = corners[faces[face][corner]];
                }

                int baseTriangle = face * 6;
                triangles[baseTriangle] = baseVertex;
                triangles[baseTriangle + 1] = baseVertex + 2;
                triangles[baseTriangle + 2] = baseVertex + 1;
                triangles[baseTriangle + 3] = baseVertex;
                triangles[baseTriangle + 4] = baseVertex + 3;
                triangles[baseTriangle + 5] = baseVertex + 2;
            }

            Mesh mesh = new Mesh
            {
                name = $"meshDeathwingRock{seed}",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Random.state = state;
            return mesh;
        }

        /// <summary>
        /// Scatters rocks over a circle of ground, each dropped onto whatever surface is beneath it and
        /// sunk part-way in so it looks like it was pushed up rather than dropped on top. The rocks are
        /// left unparented and destroyed on a timer: the zone they belong to scales itself as it grows,
        /// which would stretch anything hanging off it.
        /// </summary>
        internal static void Scatter(Vector3 origin, float radius, int count, float size, float lifetime)
        {
            Material material = RockMaterial();
            if (!material)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 position = origin + new Vector3(offset.x, 1f, offset.y);

                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 6f, LayerIndex.world.mask))
                {
                    position = hit.point;
                }
                else
                {
                    position.y = origin.y;
                }

                float scale = size * Random.Range(0.6f, 1.4f);
                position.y -= scale * 0.35f;

                GameObject rock = new GameObject($"DeathwingRock{i}");
                rock.transform.position = position;
                rock.transform.rotation = Quaternion.Euler(
                    Random.Range(-12f, 12f),
                    Random.Range(0f, 360f),
                    Random.Range(-12f, 12f));
                rock.transform.localScale = Vector3.one * scale;

                rock.AddComponent<MeshFilter>().sharedMesh = RockMesh(Random.Range(0, 1000));
                MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                UnityEngine.Object.Destroy(rock, lifetime);
            }
        }
    }
}
