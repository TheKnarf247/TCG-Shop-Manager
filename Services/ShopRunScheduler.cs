#nullable enable
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.Services
{
    internal static class ShopRunScheduler
    {
        private static float _elapsed;
        private static bool _loggedWaiting;

        internal static void Tick()
        {
            if (!SMConfig.Enabled.Value)
            {
                _elapsed = 0f;
                _loggedWaiting = false;
                return;
            }

            if (!SMConfig.EnableScheduledRuns.Value)
            {
                _elapsed = 0f;
                _loggedWaiting = false;
                return;
            }

            if (ShopRefillService.IsRunning)
            {
                _loggedWaiting = false;
                return;
            }

            float interval = Mathf.Max(1f, SMConfig.ScheduledRunIntervalSeconds.Value);
            _elapsed += Time.unscaledDeltaTime;

            if (!_loggedWaiting && SMConfig.VerboseLogging.Value)
            {
                SMLog.Debug($"[ShopManager] Scheduler active. Next run in {interval - _elapsed:0.0}s");
                _loggedWaiting = true;
            }

            if (_elapsed < interval)
                return;

            _elapsed = 0f;
            _loggedWaiting = false;

            SMLog.Info("[ShopManager] Scheduler triggered automatic run.");
            ShopRefillService.Start();
        }

        internal static void Reset()
        {
            _elapsed = 0f;
            _loggedWaiting = false;
        }
    }
}