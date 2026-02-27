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
            if (!SMConfig.Enabled.Value) return;

            if (!SMConfig.RefillHotkey.Value.IsDown())
                return;

            SMLog.Info("[ShopManager] Hotkey pressed → running refill.");
            ShopRefillService.RunOnce();
        }
    }
}