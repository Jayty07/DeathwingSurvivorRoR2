using System.Collections.Generic;
using System.Text;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Hands rendering over from the borrowed chassis mesh to the real Deathwing model once the body
    /// has spawned. It cannot be done on the prefab: the skin system assigns the chassis' renderer
    /// list during spawn and would put the borrowed mesh straight back, so the handover is asserted
    /// for a moment after spawn instead.
    ///
    /// The dragon is drawn as plain renderers by default rather than being registered with
    /// <see cref="CharacterModel"/>. Registering would earn it the game's elite, burn and cloak
    /// overlays, but it also hands it to <see cref="PrintController"/>, whose print bounds are
    /// authored for the chassis mesh and can clip the model away entirely.
    /// </summary>
    public class DeathwingCustomModel : MonoBehaviour
    {
        /// <summary>How long to keep re-claiming the renderer list after the body spawns.</summary>
        private const float assertDuration = 3f;

        private static bool reported;

        private readonly List<SkinnedMeshRenderer> ours = new List<SkinnedMeshRenderer>();
        private readonly List<SkinnedMeshRenderer> chassis = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Material> materials =
            new Dictionary<SkinnedMeshRenderer, Material>();

        private CharacterModel characterModel;
        private float age;
        private bool aligned;

        private void OnEnable()
        {
            age = 0f;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (Claimed())
            {
                Reassert();
            }
            else
            {
                Apply();
            }

            Align();

            if (age > assertDuration)
            {
                Report();
                enabled = false;
            }
        }

        private void Apply()
        {
            if (!characterModel)
            {
                characterModel = GetComponent<CharacterModel>();
                if (!characterModel)
                {
                    enabled = false;
                    return;
                }
            }

            ours.Clear();
            chassis.Clear();
            int chassisLayer = gameObject.layer;
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<DeathwingAnimator>())
                {
                    ours.Add(renderer);
                    continue;
                }

                chassis.Add(renderer);
                chassisLayer = renderer.gameObject.layer;
            }

            if (ours.Count == 0)
            {
                return;
            }

            List<CharacterModel.RendererInfo> infos = new List<CharacterModel.RendererInfo>();
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                // Whatever layer the chassis draws its own mesh on is the one the game's cameras and
                // its culling masks expect a character to be on.
                renderer.gameObject.layer = chassisLayer;

                if (!materials.TryGetValue(renderer, out Material material) || !material)
                {
                    // The material the model was built with, not whatever the character model may have
                    // instanced from it since, so re-claiming cannot instance an instance.
                    material = renderer.sharedMaterial;
                    materials[renderer] = material;
                }

                renderer.sharedMaterial = material;
                infos.Add(new CharacterModel.RendererInfo
                {
                    renderer = renderer,
                    defaultMaterial = material,
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false,
                    hideOnDeath = false
                });
            }

            if (Tuning.registerModel.Value)
            {
                characterModel.baseRendererInfos = infos.ToArray();
                characterModel.mainSkinnedMeshRenderer = ours[0];
            }

            Reassert();
        }

        /// <summary>
        /// Stands the dragon on the ground. The chassis' model base is offset for the borrowed mesh,
        /// whose origin is at its hips rather than its feet, so the dragon - whose origin is between
        /// his claws - is planted below the floor by that offset. Rather than guess at it, the model is
        /// raised by however far its lowest drawn point sits under the capsule's base.
        /// </summary>
        private void Align()
        {
            if (aligned || ours.Count == 0 || age < 0.2f)
            {
                return;
            }

            CharacterBody body = GetComponentInParent<CharacterBody>();
            Transform root = ours[0].transform.parent;
            if (!body || !root)
            {
                return;
            }

            float lowest = float.MaxValue;
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                if (renderer.sharedMesh)
                {
                    lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                }
            }

            if (lowest == float.MaxValue)
            {
                return;
            }

            aligned = true;
            float lift = body.footPosition.y - lowest + Tuning.realModelLift.Value;
            root.position += Vector3.up * lift;
            Log.Info($"Raised the model {lift:0.###}m to stand its claws on the ground.");
        }

        /// <summary>True once the handover has been made and nothing has undone it.</summary>
        private bool Claimed()
        {
            if (!characterModel || ours.Count == 0)
            {
                return false;
            }

            if (!Tuning.registerModel.Value)
            {
                return true;
            }

            if (characterModel.baseRendererInfos == null
                || characterModel.baseRendererInfos.Length != ours.Count)
            {
                return false;
            }

            for (int i = 0; i < ours.Count; i++)
            {
                if (characterModel.baseRendererInfos[i].renderer != ours[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Reassert()
        {
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
            }

            // The chassis mesh stays in the hierarchy - hitboxes, footsteps and the ragdoll hang off
            // its skeleton - it simply stops being drawn.
            foreach (SkinnedMeshRenderer renderer in chassis)
            {
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }
        }

        /// <summary>
        /// One report per session describing what the dragon's renderers actually ended up as. A model
        /// that fails to draw does so silently - no mesh, an unloaded material, a zero scale and a
        /// culled layer all look identical in game - so the state is written to the log once.
        /// </summary>
        private void Report()
        {
            if (reported)
            {
                return;
            }

            reported = true;

            if (ours.Count == 0)
            {
                Log.Warning("The real model is not in the spawned body's hierarchy; the chassis mesh is "
                    + "drawing instead.");
                return;
            }

            StringBuilder report = new StringBuilder("Deathwing model state:");
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                Bounds bounds = renderer.bounds;
                Material shared = renderer.sharedMaterial;
                report.Append($"\n  {renderer.name}: mesh={(renderer.sharedMesh ? renderer.sharedMesh.vertexCount + " verts" : "MISSING")}"
                    + $", material={(shared ? shared.name + " (" + (shared.shader ? shared.shader.name : "no shader") + ")" : "MISSING")}"
                    + $", enabled={renderer.enabled}, forceOff={renderer.forceRenderingOff}, visible={renderer.isVisible}"
                    + $", layer={LayerMask.LayerToName(renderer.gameObject.layer)}"
                    + $", scale={renderer.transform.lossyScale.x:0.####}"
                    + $", bounds={bounds.center} size {bounds.size}");
            }

            Animation animation = GetComponentInChildren<Animation>();
            report.Append($"\n  animation: {(animation ? (animation.isPlaying ? "playing" : "idle") : "missing")}");
            report.Append($"\n  registered with the character model: {Tuning.registerModel.Value}"
                + $", chassis renderers hidden: {chassis.Count}");
            Log.Info(report.ToString());
        }
    }
}
