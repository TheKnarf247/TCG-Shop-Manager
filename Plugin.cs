#nullable enable
using BepInEx;
using HarmonyLib;
using ShopManager.Automation;
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager
{
    [BepInPlugin(
        MyPluginInfo.PLUGIN_GUID,
        MyPluginInfo.PLUGIN_NAME,
        MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private Harmony _harmony = null!;

        private void Awake()
        {
            SMLog.Init(Logger);
            SMConfig.Bind(Config);

            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            HotkeyController.Init();
            
            SMLog.Info($"Plugin {MyPluginInfo.PLUGIN_NAME} v:{MyPluginInfo.PLUGIN_VERSION} by Knarf Hasn't Exploded Yet!");
        }

        private void Update()
        {
            HotkeyController.Tick();
            ShopManager.Services.ShopRefillService.Tick();
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { /* ignore */ }
        }
    }
}