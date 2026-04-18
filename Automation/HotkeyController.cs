#nullable enable
using UnityEngine;
using ShopManager.Config;
using ShopManager.Logging;
using ShopManager.Services;

namespace ShopManager.Automation
{
    internal static class HotkeyController
    {
        private static bool _init;

        internal static void Init()
        {
            if (_init) return;
            _init = true;
            SMLog.Info("[ShopManager] HotkeyController initialized.");
        }

        internal static void Tick()
        {
            ShopManager.Patches.PackOpeningController.Tick();
            if (!SMConfig.Enabled.Value) return;

            if (!SMConfig.RefillHotkey.Value.IsDown())
                return;

            if (!ShopRefillService.IsRunning)
            {
                SMLog.Info("[ShopManager] Hotkey pressed → starting refill session.");
                ShopRefillService.Start();
            }
            else
            {
                SMLog.Warn("[ShopManager] Hotkey pressed while running → cancelling refill session.");
                ShopRefillService.Cancel();
            }
        }
    }
}