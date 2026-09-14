using R2API;
using RoR2;
using UnityEngine;

namespace Deathwing.Modules
{
    internal static class DeathwingSurvivor
    {
        internal static SurvivorDef survivorDef;

        internal static BodyIndex bodyIndex = BodyIndex.None;

        internal static void Init()
        {
            DeathwingBody.Init();
            if (!DeathwingBody.bodyPrefab)
            {
                return;
            }

            Skills.Init(DeathwingBody.bodyPrefab);

            survivorDef = ScriptableObject.CreateInstance<SurvivorDef>();
            ((ScriptableObject)survivorDef).name = "DeathwingSurvivor";
            survivorDef.cachedName = "Deathwing";
            survivorDef.bodyPrefab = DeathwingBody.bodyPrefab;
            survivorDef.displayPrefab = DeathwingBody.displayPrefab;
            survivorDef.displayNameToken = Tokens.bodyName;
            survivorDef.descriptionToken = Tokens.bodyDescription;
            survivorDef.outroFlavorToken = Tokens.bodyOutro;
            survivorDef.mainEndingEscapeFailureFlavorToken = Tokens.bodyFailure;
            survivorDef.primaryColor = new Color(0.78f, 0.22f, 0.06f);
            survivorDef.desiredSortPosition = 100f;

            ContentAddition.AddSurvivorDef(survivorDef);

            RecalculateStatsAPI.GetStatCoefficients += ApplyAspectOfDeath;
            BodyCatalog.availability.CallWhenAvailable(CacheBodyIndex);
        }

        private static void CacheBodyIndex()
        {
            bodyIndex = BodyCatalog.FindBodyIndex(DeathwingBody.bodyPrefabName);
        }

        /// <summary>
        /// Aspect of Death: his armor is the plates he still wears, so it steps down as he is wounded
        /// rather than sliding, and it does not come back on its own. Plate bookkeeping lives on
        /// <see cref="AspectOfDeath"/>; this only turns the count into armor.
        /// </summary>
        private static void ApplyAspectOfDeath(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || body.bodyIndex != bodyIndex)
            {
                return;
            }

            if (body.HasBuff(Buffs.elementiumPlating))
            {
                args.armorAdd += Buffs.elementiumPlatingArmor;
            }

            AspectOfDeath aspect = body.GetComponent<AspectOfDeath>();
            if (aspect)
            {
                args.armorAdd += Tuning.platingArmorPerPlate.Value * aspect.platesRemaining;
            }
        }
    }
}
