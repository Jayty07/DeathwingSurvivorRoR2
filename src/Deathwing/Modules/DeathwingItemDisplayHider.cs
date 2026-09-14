using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    /// <summary>
    /// Keeps collected items off his model. The rule set is cleared when the body is built, but the
    /// game can hand the model a rule set again later (skins carry one, and the catalog resolves them
    /// after the prefab is made), so anything it instantiates is torn down as soon as it appears.
    /// </summary>
    public class DeathwingItemDisplayHider : MonoBehaviour
    {
        private CharacterModel characterModel;

        private void Awake()
        {
            characterModel = GetComponent<CharacterModel>();
        }

        private void LateUpdate()
        {
            if (!characterModel)
            {
                return;
            }

            if (characterModel.itemDisplayRuleSet)
            {
                characterModel.itemDisplayRuleSet = null;
            }

            if (characterModel.parentedPrefabDisplays.Count > 0 || characterModel.limbMaskDisplays.Count > 0)
            {
                characterModel.DisableAllItemDisplays();
            }

            if (characterModel.currentEquipmentDisplayIndex != EquipmentIndex.None)
            {
                characterModel.SetEquipmentDisplay(EquipmentIndex.None);
            }
        }
    }
}
