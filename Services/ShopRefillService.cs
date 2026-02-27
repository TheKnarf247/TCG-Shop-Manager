#nullable enable
using ShopManager.Config;
using ShopManager.Logging;

namespace ShopManager.Services
{
    internal static class ShopRefillService
    {
        private static RefillSession? _session;

        internal static bool IsRunning => _session != null;

        internal static void Start()
        {
            if (!SMConfig.Enabled.Value) return;
            if (_session != null)
            {
                SMLog.Warn("[ShopManager] Start called but a session is already running.");
                return;
            }

            _session = new RefillSession();
        }

        internal static void Cancel()
        {
            if (_session == null) return;
            _session.Cancel();
            _session = null;
            SMLog.Warn("[ShopManager] Refill session cancelled.");
        }

        internal static void Tick()
        {
            if (!SMConfig.Enabled.Value) { _session = null; return; }
            if (_session == null) return;

            _session.Tick();
            if (_session.IsFinished) _session = null;

            // Clean up when done/cancelled
            // (RefillSession sets its phase, but we keep it simple here by letting it run to completion)
            // If you want, we can expose CurrentPhase and clear here.
            // For now: RefillSession logs completion and we clear when it reaches Done/Cancelled by checking IsRunning externally.
            // We'll clear when it has finished by observing that it no longer does work next tick.
            // Practical approach: clear when phase is Done/Cancelled (add a property if you want).
        }
    }
}