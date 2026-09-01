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

        /// <summary>
        /// How long after spawning his footing is corrected even without a reading from the world, so a
        /// stage whose surface the cast misses still leaves him standing somewhere sane.
        /// </summary>
        private const float alignDuration = 2.5f;

        /// <summary>How far above and below him the surface he stands on is looked for.</summary>
        private const float groundProbeRise = 4f;
        private const float groundProbeDrop = 60f;

        /// <summary>The chassis shader's fade, and the toggle that makes it dissolve rather than blend.</summary>
        private const string fadeProperty = "_Fade";
        private const string ditherProperty = "_DitherOn";

        private static bool reported;

        private readonly List<SkinnedMeshRenderer> ours = new List<SkinnedMeshRenderer>();
        private readonly List<Renderer> chassis = new List<Renderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Material> materials =
            new Dictionary<SkinnedMeshRenderer, Material>();

        private CharacterModel characterModel;
        private float age;
        private bool fitted;
        private float logged;
        private Vector3 rest;
        private float rise;
        private float footDrop;
        private bool settled;
        private bool hidden;
        private float fade = 1f;

        private void OnEnable()
        {
            age = 0f;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age <= assertDuration)
            {
                if (Claimed())
                {
                    Reassert();
                }
                else
                {
                    Apply();
                }
            }

            Fit();
            Dither();

            if (age > assertDuration)
            {
                Report();

                // Nothing left to do for a body drawing the chassis mesh; a body drawing the dragon
                // keeps running to hold him at the height the correction settled on.
                enabled = ours.Count > 0;
            }
        }

        /// <summary>
        /// Takes the dragon out of view entirely, as flight does once he has climbed out of the fight.
        /// Only his renderers stop drawing: his hitboxes, his motor and everything the chassis skeleton
        /// carries are untouched, so hiding him cannot change how he plays or what the server sees.
        /// </summary>
        internal void SetHidden(bool hide)
        {
            if (hidden == hide)
            {
                return;
            }

            hidden = hide;
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                if (renderer)
                {
                    renderer.forceRenderingOff = hide;
                }
            }
        }

        /// <summary>
        /// Fades him out as the camera comes into him. He is fifteen metres of dragon with the camera
        /// pivot at his middle, so any wall the camera is pushed against puts the view inside his body;
        /// this dissolves him while that lasts instead of filling the screen with his flank.
        ///
        /// The fade is driven on this client's own instance of his material, so it is a local view
        /// change only: nothing about his position, hitboxes or state is touched, and other players see
        /// him as normal.
        /// </summary>
        private void Dither()
        {
            // Left alone during the handover window: the renderer list is still being re-claimed then,
            // and instancing a material it is about to put back would leak one copy a frame.
            if (hidden || ours.Count == 0 || age <= assertDuration || !Tuning.cameraDither.Value)
            {
                return;
            }

            float far = Mathf.Max(0.1f, Tuning.cameraDitherDistance.Value);
            float near = far * 0.25f;
            float target = 1f;

            Camera camera = ViewingCamera();
            if (camera)
            {
                float distance = float.MaxValue;
                foreach (SkinnedMeshRenderer renderer in ours)
                {
                    if (renderer && renderer.sharedMesh)
                    {
                        distance = Mathf.Min(distance,
                            Mathf.Sqrt(renderer.bounds.SqrDistance(camera.transform.position)));
                    }
                }

                if (distance < far)
                {
                    target = Mathf.Clamp01((distance - near) / Mathf.Max(0.01f, far - near));
                }
            }

            float previous = fade;
            fade = Mathf.MoveTowards(fade, target, Time.deltaTime * 4f);
            if (fade < 1f || previous < 1f)
            {
                ApplyFade(fade);
            }
        }

        /// <summary>The local camera looking at this body, if it is the one being played or spectated.</summary>
        private Camera ViewingCamera()
        {
            CharacterBody body = characterModel ? characterModel.body : null;
            if (!body)
            {
                return null;
            }

            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
            {
                if (rig && rig.target == body.gameObject && rig.sceneCam)
                {
                    return rig.sceneCam;
                }
            }

            return null;
        }

        private void ApplyFade(float amount)
        {
            bool gone = amount <= 0.02f;
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                if (!renderer)
                {
                    continue;
                }

                // Reading the renderer's material instances it, which is what keeps the fade on this
                // client's copy rather than on the shared material every Deathwing draws with.
                Material material = renderer.material;
                if (material && material.HasProperty(fadeProperty))
                {
                    if (material.HasProperty(ditherProperty))
                    {
                        material.SetFloat(ditherProperty, 1f);
                    }

                    material.SetFloat(fadeProperty, amount);
                }
                else
                {
                    // Without a fade in the shader the only honest choice is drawing him or not, so he
                    // is dropped to a shadow once the camera is properly inside him.
                    gone = gone || amount < 0.5f;
                }

                renderer.forceRenderingOff = gone;
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

            // Every renderer is considered, not only the skinned ones: the borrowed survivor carries
            // its weapons as plain meshes, so hiding its body alone leaves a pair of pistols floating
            // inside the dragon. Particles and trails are left alone - those are its effects, not it.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer
                    || renderer is LineRenderer || renderer is BillboardRenderer)
                {
                    continue;
                }

                if (renderer is SkinnedMeshRenderer skinned && renderer.GetComponentInParent<DeathwingAnimator>())
                {
                    ours.Add(skinned);
                    continue;
                }

                chassis.Add(renderer);
                if (renderer is SkinnedMeshRenderer)
                {
                    chassisLayer = renderer.gameObject.layer;
                }
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
        /// The lowest and highest point of his posed skeleton, in world space. His size is taken from
        /// the skeleton rather than his bounds because bounds also enclose whatever his wings are doing,
        /// and a bone is a real transform: its world position needs no assumption about which space a
        /// measurement came back in, which is what sized him wrongly by a factor of hundreds twice.
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

        /// <summary>The lowest point of the geometry as it is actually drawn, in world space.</summary>
        private bool Drawn(out float bottom)
        {
            bottom = float.MaxValue;
            foreach (SkinnedMeshRenderer renderer in ours)
            {
                if (renderer.sharedMesh)
                {
                    bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                }
            }

            return bottom < float.MaxValue;
        }

        /// <summary>
        /// Sizes the dragon to the height asked for and stands him on the ground.
        ///
        /// The size comes from his skeleton, but the standing is done as a correction rather than a
        /// calculation: every attempt to work out where his feet ought to be from the rig has put him
        /// somewhere else, so instead his drawn bounds are compared against the ground for a moment
        /// after spawning and the difference is taken out. Whichever space the skinning resolves into,
        /// that converges.
        ///
        /// What that moment measures is how far his lowest drawn point sits below his model root, and
        /// from then on only the ground is read. Comparing his bounds against the ground every frame
        /// instead makes the correction chase his animation: a raised foot or a wingbeat moves the
        /// bottom of his geometry, and taking that difference out heaves the whole dragon up and down
        /// in time with the clip.
        /// </summary>
        private void Fit()
        {
            // Long enough for the animator to have posed him: measuring the bind pose would size him
            // by his outstretched wings instead of his standing height.
            if (ours.Count == 0 || age < 0.4f)
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

            if (!fitted)
            {
                fitted = true;
                Resize(root);
                rest = root.localPosition;
            }

            // Corrected for as long as he is stood on something, not only just after spawning: what he
            // is stood on changes as he walks, and a one-off correction taken while he is being carried
            // by anything else - a drop pod on the first stage, a lift, a jump - is held for the rest of
            // the run. Off the ground he is only held where he was, since there is nothing to read.
            bool grounded = !body.characterMotor || body.characterMotor.isGrounded;
            if (!grounded || !Ground(body, out float ground, out string from))
            {
                Place(root);
                return;
            }

            if (!settled)
            {
                if (!Drawn(out float bottom))
                {
                    Place(root);
                    return;
                }

                // The deepest reading of the settling window, so the correction ends up under his
                // lowest extremity rather than under whichever foot happened to be raised.
                footDrop = Mathf.Max(footDrop, root.position.y - bottom);
                settled = age > alignDuration;
            }

            float delta = ground + Tuning.realModelLift.Value + footDrop - root.position.y;
            rise += delta;
            Place(root);

            // Only reported while he is settling, and afterwards only if something moved him a long way:
            // the correction now runs for the whole run, and logging it every frame would drown the log.
            if (Time.time > logged + 0.75f && (age < alignDuration || Mathf.Abs(delta) > 0.5f))
            {
                logged = Time.time;
                Log.Info($"Raised the model {rise:0.###}m onto the ground read from {from} (that ground "
                    + $"at {ground:0.##}, his feet {footDrop:0.##}m under his model root, his foot "
                    + $"position at {body.footPosition.y:0.##}, model root now at {root.position.y:0.##}).");
            }
        }

        /// <summary>
        /// Puts the model at its own rest position within the character's model, raised straight up in
        /// world space. The rise is applied in world space rather than stored as a local offset because
        /// the model base pitches and rolls as he moves, and a tilted local offset of several metres
        /// slides him sideways instead of lifting him.
        /// </summary>
        private void Place(Transform root)
        {
            Transform parent = root.parent;
            Vector3 target = (parent ? parent.TransformPoint(rest) : rest) + Vector3.up * rise;
            if (root.position != target)
            {
                root.position = target;
            }
        }

        /// <summary>
        /// The height his feet should be drawn at. The surface under him is found by casting against the
        /// world, because everything the hierarchy offers as a ground - his foot position, the chassis
        /// mesh's own bottom - has turned out to sit below the surface that is actually drawn.
        /// </summary>
        private bool Ground(CharacterBody body, out float height, out string from)
        {
            Vector3 above = body.corePosition + Vector3.up * groundProbeRise;
            if (Physics.Raycast(above, Vector3.down, out RaycastHit hit,
                groundProbeRise + groundProbeDrop, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                from = "the surface under him";
                height = hit.point.y;
                return true;
            }

            float bottom = float.MaxValue;
            foreach (Renderer renderer in chassis)
            {
                if (renderer)
                {
                    bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                }
            }

            if (bottom < float.MaxValue)
            {
                from = "the chassis mesh";
                height = bottom;
                return false;
            }

            from = "his foot position";
            height = body.footPosition.y;
            return false;
        }

        /// <summary>Scales the model so his silhouette stands the configured number of metres tall.</summary>
        private void Resize(Transform root)
        {
            if (!Measure(out float lowest, out float highest))
            {
                Log.Warning("Could not measure the model; leaving its size alone.");
                return;
            }

            float height = (highest - lowest) / boneSpanShare;
            if (Tuning.realModelHeight.Value <= 0f || height <= 0.001f)
            {
                return;
            }

            // Clamped because being wrong about a scale is the difference between a dragon and a
            // map-sized one, and a refusal to resize is far easier to see and report than that.
            float factor = Mathf.Clamp(Tuning.realModelHeight.Value / height, 0.02f, 50f);
            root.localScale *= factor;
            Log.Info($"Scaled the model {factor:0.###}x: {height:0.##}m measured across its skeleton, "
                + $"{Tuning.realModelHeight.Value:0.##}m asked for.");
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
                renderer.forceRenderingOff = hidden;
            }

            // The chassis mesh stays in the hierarchy - hitboxes, footsteps and the ragdoll hang off
            // its skeleton - it simply stops being drawn.
            foreach (Renderer renderer in chassis)
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

            CharacterBody body = characterModel ? characterModel.body : null;
            if (Drawn(out float drawn))
            {
                report.Append($"\n  drawn bottom: {drawn:0.##}");
            }

            foreach (Renderer renderer in chassis)
            {
                if (renderer)
                {
                    report.Append($"\n  chassis {renderer.name} ({renderer.GetType().Name}): hidden"
                        + $"={!renderer.enabled}, bottom {renderer.bounds.min.y:0.##}, "
                        + $"top {renderer.bounds.max.y:0.##}");
                }
            }

            Transform root = ours[0].transform.parent;
            report.Append($"\n  body: {(body ? body.name + " at " + body.footPosition : "none - this is the menu's display model")}");
            report.Append($"\n  root: {(root ? root.name + " at " + root.position : "none")}");

            Animation animation = GetComponentInChildren<Animation>();
            report.Append($"\n  animation: {(animation ? (animation.isPlaying ? "playing" : "idle") : "missing")}");
            report.Append($"\n  registered with the character model: {Tuning.registerModel.Value}"
                + $", chassis renderers hidden: {chassis.Count}");
            Log.Info(report.ToString());
        }
    }
}
