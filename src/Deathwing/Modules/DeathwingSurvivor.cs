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

            RecalculateStatsAPI.GetStatCoefficients += ApplyMoltenBlood;
            BodyCatalog.availability.CallWhenAvailable(CacheBodyIndex);
        }

        private static void CacheBodyIndex()
        {
            bodyIndex = BodyCatalog.FindBodyIndex(DeathwingBody.bodyPrefabName);
        }

        /// <summary>
        /// Molten Blood: armor and damage scale with missing health, so being brought low makes
        /// Deathwing more dangerous rather than less.
        /// </summary>
        private static void ApplyMoltenBlood(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || body.bodyIndex != bodyIndex)
            {
                return;
            }

            if (body.HasBuff(Buffs.elementiumPlating))
            {
                args.armorAdd += Buffs.elementiumPlatingArmor;
            }

            HealthComponent healthComponent = body.healthComponent;
            if (!healthComponent || healthComponent.fullCombinedHealth <= 0f)
            {
                return;
            }

            float missingHealthFraction = Mathf.Clamp01(1f - healthComponent.combinedHealth / healthComponent.fullCombinedHealth);
            args.armorAdd += Tuning.moltenBloodMaxArmor.Value * missingHealthFraction;
            args.damageMultAdd += Tuning.moltenBloodMaxDamageMult.Value * missingHealthFraction;
        }
    }
}
