using BepInEx;
using Deathwing.Modules;
using R2API;
using R2API.Utils;

namespace Deathwing
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class DeathwingPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "com.jayty07.deathwing";
        public const string pluginName = "Deathwing";
        public const string pluginVersion = "1.0.0";

        private void Awake()
        {
            Log.Init(Logger);

            Tuning.Init(Config);
            Tokens.Init();
            DeathwingAssets.Init();
            Buffs.Init();
            Projectiles.Init();
            DeathwingSurvivor.Init();

            Log.Info($"{pluginName} {pluginVersion} loaded.");
        }
    }
}
