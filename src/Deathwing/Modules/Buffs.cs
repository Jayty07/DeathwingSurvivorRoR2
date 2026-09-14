using R2API;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    internal static class Buffs
    {
        /// <summary>Heavy armor granted while Deathwing is committed to a charge or to Cataclysm.</summary>
        internal static BuffDef elementiumPlating;

        internal const float elementiumPlatingArmor = 200f;

        internal static void Init()
        {
            elementiumPlating = ScriptableObject.CreateInstance<BuffDef>();
            elementiumPlating.name = "DeathwingElementiumPlating";
            elementiumPlating.buffColor = new Color(0.85f, 0.35f, 0.1f);
            elementiumPlating.canStack = false;
            elementiumPlating.isDebuff = false;
            elementiumPlating.iconSprite = DeathwingAssets.Load<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif", "RoR2/Base/Bear/texBuffFullCritIcon.tif");

            ContentAddition.AddBuffDef(elementiumPlating);
        }
    }
}
