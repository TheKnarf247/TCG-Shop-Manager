#nullable enable
using BepInEx;
using HarmonyLib;
using ShopManager.Automation;
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.knarf.shopmanager";
        public const string PluginName = "Shop Manager";
        public const string PluginVersion = "1.0";

        private Harmony _harmony = null!;

        private void Awake()
        {
            SMLog.Init(Logger);
            SMConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            HotkeyController.Init();

            SMLog.Info($"[{PluginName}] Loaded v{PluginVersion}.");
        }

        private void Update()
        {
            HotkeyController.Tick();
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { /* ignore */ }
        }
    }
}