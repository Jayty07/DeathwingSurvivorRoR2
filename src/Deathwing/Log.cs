using BepInEx.Logging;

namespace Deathwing
{
    internal static class Log
    {
        private static ManualLogSource _logger;

        internal static void Init(ManualLogSource logger) => _logger = logger;

        internal static void Debug(object data) => _logger?.LogDebug(data);

        internal static void Info(object data) => _logger?.LogInfo(data);

        internal static void Warning(object data) => _logger?.LogWarning(data);

        internal static void Error(object data) => _logger?.LogError(data);
    }
}
