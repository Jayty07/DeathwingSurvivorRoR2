using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Hands rendering over from the borrowed chassis mesh to the real Deathwing model once the body
    /// has spawned. It cannot be done on the prefab: the skin system assigns the chassis' renderer
    /// list during spawn and would put the borrowed mesh straight back. Registering the dragon's
    /// renderer in <see cref="CharacterModel"/> (rather than merely parenting it) is what lets the
    /// game light it, dither it and draw its burn, cloak and elite overlays like any other survivor.
    /// </summary>
    public class DeathwingCustomModel : MonoBehaviour
    {
        private bool applied;

        private void OnEnable()
        {
            applied = false;
        }

        private void LateUpdate()
        {
            if (applied)
            {
                return;
            }

            applied = true;
            enabled = false;

            CharacterModel characterModel = GetComponent<CharacterModel>();
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (!characterModel || renderers.Length == 0)
            {
                return;
            }

            List<CharacterModel.RendererInfo> infos = new List<CharacterModel.RendererInfo>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                bool ours = renderer.GetComponentInParent<DeathwingAnimator>();
                if (!ours)
                {
                    // The chassis mesh stays in the hierarchy - hitboxes, footsteps and the ragdoll
                    // hang off its skeleton - it simply stops being drawn.
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                    continue;
                }

                infos.Add(new CharacterModel.RendererInfo
                {
                    renderer = renderer,
                    defaultMaterial = renderer.sharedMaterial,
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false,
                    hideOnDeath = false
                });
            }

            if (infos.Count == 0)
            {
                return;
            }

            characterModel.baseRendererInfos = infos.ToArray();
            characterModel.mainSkinnedMeshRenderer = infos[0].renderer as SkinnedMeshRenderer;
        }
    }
}
