using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Burning ground for a pool cloned from a prefab whose artwork is the wrong colour. Tinting a
    /// material cannot recolour green acid textures, so the borrowed renderers are switched off and the
    /// pool draws itself out of Deathwing's own fire effect instead.
    /// </summary>
    public class DeathwingLavaVisual : MonoBehaviour
    {
        public float interval = 0.45f;
        public float scale = 1.5f;
        public float radius = 4f;

        private float stopwatch;

        private void Start()
        {
            // Staggered so a field of pools does not pulse in unison, and so their spawns spread over the
            // frame budget instead of landing together.
            stopwatch = Random.Range(0f, interval);
        }

        private void Update()
        {
            stopwatch += Time.deltaTime;
            if (stopwatch < interval)
            {
                return;
            }

            stopwatch = 0f;

            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 position = transform.position + new Vector3(offset.x, 0.2f, offset.y);
            DeathwingAssets.SpawnEffectLocal(DeathwingAssets.fireImpactEffect, position, scale, interval * 3f);
        }
    }
}
