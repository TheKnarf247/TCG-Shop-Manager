#nullable enable
using BepInEx.Configuration;
using UnityEngine;

namespace ShopManager.Config
{
    internal static class SMConfig
    {
        internal static ConfigEntry<bool> Enabled = null!;

        // Debug toggles
        internal static ConfigEntry<bool> VerboseLogging = null!;
        internal static ConfigEntry<bool> ScanDebugLogging = null!;

        internal static ConfigEntry<KeyboardShortcut> RefillHotkey = null!;

        // Chunking / performance controls
        internal static ConfigEntry<int> ScanCompartmentsPerFrame = null!;
        internal static ConfigEntry<int> PlacementsPerFrame = null!;
        internal static ConfigEntry<int> LogProgressEveryN = null!;

        //Scheduling controls
        internal static ConfigEntry<bool> EnableScheduledRuns = null!;
        internal static ConfigEntry<float> ScheduledRunIntervalSeconds = null!;

        //Lighting
        internal static ConfigEntry<bool> EnableAutoLights = null!;
        internal static ConfigEntry<string> AutoLightOnTime = null!;
                
        //Shop Open Automation
        internal static ConfigEntry<bool> EnableShopOpenAutomation = null!;
        internal static ConfigEntry<int> ShopOpenAutomationDelayMinutes = null!;
               
        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("00 - General", "Enabled", true, "Enable/disable Shop Manager.");

            RefillHotkey = cfg.Bind("01 - Hotkeys", "RefillHotkey",
                new KeyboardShortcut(KeyCode.F7), "Hotkey to run a refill pass.");

            ScanCompartmentsPerFrame = cfg.Bind("02 - Performance", "ScanCompartmentsPerFrame", 200,
                "How many shop compartments to inspect per frame during the scan phase.");

            PlacementsPerFrame = cfg.Bind("02 - Performance", "PlacementsPerFrame", 25,
                "How many item placements to perform per frame during the fill phase.");

            LogProgressEveryN = cfg.Bind("02 - Performance", "LogProgressEveryN", 500,
                "Log progress every N scanned compartments (0 disables progress logging).");

            EnableScheduledRuns = cfg.Bind(
                "03 - Scheduler",
                "EnableScheduledRuns",
                false,
                "Automatically run Shop Manager on a timer."
            );

            ScheduledRunIntervalSeconds = cfg.Bind(
                "03 - Scheduler",
                "ScheduledRunIntervalSeconds",
                300f,
                new ConfigDescription(
                    "How often Shop Manager should run automatically, in seconds.",
                    new AcceptableValueRange<float>(5f, 86400f))
            );

            EnableAutoLights = cfg.Bind(
                "04 - Lighting",
                "EnableAutoLights",
                true,
                "Automatically turns on shop lights at the configured time."
            );

            AutoLightOnTime = cfg.Bind(
                "04 - Lighting",
                "AutoLightOnTime",
                "16:00",
                "Time to automatically turn on shop lights in 24-hour format (HH:mm). Example: 16:00 = 4:00 PM."
            );

            EnableShopOpenAutomation = cfg.Bind(
                "05 - Shop Open Automation",
                "EnableShopOpenAutomation",
                true,
                "Automatically opens the shop after a new day starts."
            );
                       
            // Keep all noisy stuff together
            VerboseLogging = cfg.Bind("99 - Debug", "VerboseLogging", false,
                "Enable extra logging for troubleshooting.");

            ScanDebugLogging = cfg.Bind("99 - Debug", "ScanDebugLogging", false,
                "Logs detailed shelf scan decisions (VERY spammy).");
        }
    }
}