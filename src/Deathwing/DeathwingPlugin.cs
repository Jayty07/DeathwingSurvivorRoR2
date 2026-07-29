using BepInEx;
using Deathwing.Modules;
using R2API;
using R2API.Utils;

namespace Deathwing
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(contentManagementPluginGuid)]
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

        /// <summary>R2API.ContentManagement exposes no GUID constant, unlike the other submodules.</summary>
        private const string contentManagementPluginGuid = "com.bepis.r2api.content_management";

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
