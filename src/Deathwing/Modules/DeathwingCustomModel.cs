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

        /// <summary>
        /// How much of his drawn height his bones span. Bones sit inside the geometry, so the skeleton
        /// stops short of his claws and the crown of his head; the shortfall is allowed for here so
        /// that the configured height means his silhouette rather than his rig.
        /// </summary>
        private const float boneSpanShare = 0.85f;

        private static bool reported;

        private readonly List<SkinnedMeshRenderer> ours = new List<SkinnedMeshRenderer>();
        private readonly List<SkinnedMeshRenderer> chassis = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Material> materials =
            new Dictionary<SkinnedMeshRenderer, Material>();

        private CharacterModel characterModel;
        private float age;
        private bool fitted;

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

            Fit();

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
        /// The lowest and highest point of his posed skeleton, in world space. The skeleton is used
        /// rather than the mesh because a bone is a real transform: its world position needs no
        /// assumption about whose units a measurement came back in, which is what made two earlier
        /// attempts at this size him wrongly by a factor of hundreds. His renderer bounds are no use
        /// either - they are deliberately widened so his wings and tail cannot cull him.
        /// </summary>
        private bool Measure(out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;

            foreach (SkinnedMeshRenderer renderer in ours)
            {
                Transform[] bones = renderer.bones;
                if (bones == null)
                {
                    continue;
                }

                foreach (Transform bone in bones)
                {
                    if (!bone)
                    {
                        continue;
                    }

                    float y = bone.position.y;
                    lowest = Mathf.Min(lowest, y);
                    highest = Mathf.Max(highest, y);
                }
            }

            return highest > lowest;
        }

        /// <summary>
        /// Sizes the dragon to the height asked for and stands him on the ground, both measured from
        /// the model as it is actually posed rather than from numbers authored for the rig. Doing it
        /// here rather than in the config also makes it immune to a stale scale in an existing config.
        /// </summary>
        private void Fit()
        {
            // Long enough for the animator to have posed him: measuring the bind pose would size him
            // by his outstretched wings instead of his standing height.
            if (fitted || ours.Count == 0 || age < 0.4f)
            {
                return;
            }

            // The body is asked for through the character model, which the game points at its owner on
            // spawn: the model is not always a child of the body it belongs to.
            CharacterBody body = characterModel && characterModel.body
                ? characterModel.body
                : GetComponentInParent<CharacterBody>();

            // The menu's display clone is deliberately left alone: it is sized and framed by the
            // character select screen itself, and there is no ground under it to stand on.
            Transform root = ours[0].transform.parent;
            if (!body || !root)
            {
                return;
            }

            fitted = true;

            if (!Measure(out float lowest, out float highest))
            {
                Log.Warning("Could not measure the model; leaving its size and height alone.");
                return;
            }

            float height = (highest - lowest) / boneSpanShare;
            if (Tuning.realModelHeight.Value > 0f && height > 0.001f)
            {
                // Clamped because being wrong about a scale is the difference between a dragon and a
                // map-sized one, and a refusal to resize is far easier to see and report than that.
                float factor = Mathf.Clamp(Tuning.realModelHeight.Value / height, 0.02f, 50f);
                root.localScale *= factor;
                Log.Info($"Scaled the model {factor:0.###}x: {height:0.##}m measured across its "
                    + $"skeleton, {Tuning.realModelHeight.Value:0.##}m asked for.");

                // The bones have moved with the scale, so where his feet are has to be read again.
                if (!Measure(out lowest, out highest))
                {
                    return;
                }
            }

            float ground = body.footPosition.y;
            float lift = ground - lowest + Tuning.realModelLift.Value;
            root.position += Vector3.up * lift;
            Log.Info($"Raised the model {lift:0.###}m to stand it on the ground (its lowest bone was "
                + $"at {lowest:0.##}, ground at {ground:0.##}, top at {highest:0.##}).");
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
