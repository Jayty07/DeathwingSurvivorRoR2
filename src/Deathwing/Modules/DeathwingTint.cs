using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Recolours the model after it has spawned. The tint cannot be baked into the body prefab because
    /// the skin system assigns the borrowed survivor's materials during spawn and would overwrite it;
    /// waiting a frame lets the skin apply first and then tints whatever it produced. Copying each
    /// renderer's own live material (rather than building one from scratch) keeps its shader, textures
    /// and vertex setup intact, so the mesh stays visible whatever chassis it came from.
    /// </summary>
    public class DeathwingTint : MonoBehaviour
    {
        private static readonly Color moltenAlbedo = new Color(0.30f, 0.06f, 0.04f);
        private static readonly Color moltenEmission = new Color(3.2f, 0.7f, 0.1f);
        private static readonly Color rockAlbedo = new Color(0.18f, 0.15f, 0.14f);
        private static readonly Color rockEmission = new Color(0.9f, 0.25f, 0.05f);

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

            // Nothing left to do after the one pass, so stop being ticked at all.
            enabled = false;

            if (!Tuning.tintModel.Value)
            {
                return;
            }

            CharacterModel characterModel = GetComponent<CharacterModel>();
            if (!characterModel || characterModel.baseRendererInfos == null)
            {
                return;
            }

            CharacterModel.RendererInfo[] rendererInfos = characterModel.baseRendererInfos;
            for (int i = 0; i < rendererInfos.Length; i++)
            {
                Renderer renderer = rendererInfos[i].renderer;
                if (!renderer)
                {
                    continue;
                }

                Material source = renderer.sharedMaterial ? renderer.sharedMaterial : rendererInfos[i].defaultMaterial;
                bool molten = i % 2 == 0;
                Material tinted = DeathwingAssets.TintedCopy(
                    source,
                    molten ? "matDeathwingMolten" : "matDeathwingElementium",
                    molten ? moltenAlbedo : rockAlbedo,
                    molten ? moltenEmission : rockEmission,
                    molten ? 3.2f : 1.1f);

                if (!tinted)
                {
                    continue;
                }

                renderer.sharedMaterial = tinted;
                rendererInfos[i].defaultMaterial = tinted;
                rendererInfos[i].defaultMaterialAddress = null;
            }

            characterModel.baseRendererInfos = rendererInfos;
        }
    }
}
