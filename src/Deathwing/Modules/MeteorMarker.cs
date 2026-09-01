using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// The burning circle drawn on the ground where his next meteor will land. It exists only on the
    /// screen of the player flying, and only while he is flying: it is a sight, not an effect, so it is
    /// never networked and never spawned for anyone else.
    /// </summary>
    internal class MeteorMarker : MonoBehaviour
    {
        private const int segments = 48;

        private MeshRenderer meshRenderer;
        private Material material;
        private float radius = 1f;

        /// <summary>
        /// Builds the marker. Its disc is generated rather than borrowed because the vanilla area
        /// indicators are authored for their own abilities' shapes, and its material is the additive,
        /// unlit one the game burns enemies with, which glows on the ground instead of being lit as
        /// though it were geometry.
        /// </summary>
        internal static MeteorMarker Create()
        {
            Material template = DeathwingAssets.FlameParticleMaterial();
            if (!template)
            {
                return null;
            }

            GameObject instance = new GameObject("DeathwingMeteorMarker");
            MeteorMarker marker = instance.AddComponent<MeteorMarker>();

            MeshFilter filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc();

            marker.material = Instantiate(template);
            marker.material.name = "matDeathwingMeteorMarker";
            marker.material.SetColor("_TintColor", DeathwingAssets.fireCore);

            marker.meshRenderer = instance.AddComponent<MeshRenderer>();
            marker.meshRenderer.material = marker.material;
            marker.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker.meshRenderer.receiveShadows = false;

            instance.SetActive(false);
            return marker;
        }

        /// <summary>
        /// A ring lying in the XZ plane, its outer edge faded out through the vertex colours so it
        /// reads as a scorch mark rather than a solid plate.
        /// </summary>
        private static Mesh BuildDisc()
        {
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[i * 2] = direction * 0.72f;
                vertices[i * 2 + 1] = direction;
                colors[i * 2] = new Color(1f, 1f, 1f, 0f);
                colors[i * 2 + 1] = Color.white;
                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "meshDeathwingMeteorMarker",
                vertices = vertices,
                colors = colors,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Lays the marker flat on the surface it is aimed at, at the meteor's blast size.</summary>
        internal void Show(Vector3 point, Vector3 normal, float blastRadius)
        {
            radius = blastRadius;
            transform.position = point + normal * 0.2f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            gameObject.SetActive(true);
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // Turns and breathes, so it is visibly a live target rather than a decal left on the floor.
            transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.Self);
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.06f;
            transform.localScale = Vector3.one * (radius * pulse);
        }

        private void OnDestroy()
        {
            if (material)
            {
                Destroy(material);
            }
        }
    }
}
