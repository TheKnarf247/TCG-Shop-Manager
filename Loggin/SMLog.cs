#nullable enable
using BepInEx.Logging;
using ShopManager.Config;

namespace ShopManager.Logging
{
    internal static class SMLog
    {
        private static ManualLogSource? _log;

        internal static void Init(ManualLogSource logger) => _log = logger;

        internal static void Info(string msg) => _log?.LogInfo(msg);
        internal static void Warn(string msg) => _log?.LogWarning(msg);
        internal static void Error(string msg) => _log?.LogError(msg);

        internal static void Debug(string msg)
        {
            if (!SMConfig.VerboseLogging.Value) return;
            _log?.LogInfo(msg);
        }
    }
}
