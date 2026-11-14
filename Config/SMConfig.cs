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

        // Scheduling controls
        internal static ConfigEntry<bool> EnableScheduledRuns = null!;
        internal static ConfigEntry<float> ScheduledRunIntervalSeconds = null!;

        // Lighting
        internal static ConfigEntry<bool> EnableAutoLights = null!;
        internal static ConfigEntry<string> AutoLightOnTime = null!;

        // Shop Open Automation
        internal static ConfigEntry<bool> EnableShopOpenAutomation = null!;
        internal static ConfigEntry<int> ShopOpenAutomationDelayMinutes = null!;

        // Fast Interactions
        internal static ConfigEntry<bool> EnableFastInteractions = null!;
        internal static ConfigEntry<float> PickupSpeedMultiplier = null!;
        internal static ConfigEntry<float> CheckoutScanMultiplier = null!;
        internal static ConfigEntry<float> CheckoutChangeMultiplier = null!;
        internal static ConfigEntry<float> CheckoutScanVolumeMultiplier = null!;
        internal static ConfigEntry<bool> FastInteractionsDebugLogging = null!;
        internal static ConfigEntry<bool> EnableQuickScan = null!;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("00 - General", "Enabled", true, "Enable/disable Shop Manager.");

            RefillHotkey = cfg.Bind(
                "01 - Hotkeys",
                "RefillHotkey",
                new KeyboardShortcut(KeyCode.F7),
                "Hotkey to run a refill pass."
            );

            ScanCompartmentsPerFrame = cfg.Bind(
                "02 - Performance",
                "ScanCompartmentsPerFrame",
                200,
                "How many shop compartments to inspect per frame during the scan phase."
            );

            PlacementsPerFrame = cfg.Bind(
                "02 - Performance",
                "PlacementsPerFrame",
                25,
                "How many item placements to perform per frame during the fill phase."
            );

            LogProgressEveryN = cfg.Bind(
                "02 - Performance",
                "LogProgressEveryN",
                500,
                "Log progress every N scanned compartments (0 disables progress logging)."
            );

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

            ShopOpenAutomationDelayMinutes = cfg.Bind(
                "05 - Shop Open Automation",
                "ShopOpenAutomationDelayMinutes",
                1,
                new ConfigDescription(
                    "Delay in minutes before automatically opening the shop after a new day starts. A value of 0 uses a short fallback delay.",
                    new AcceptableValueRange<int>(0, 10))
            );

            EnableFastInteractions = cfg.Bind(
                "06 - Fast Interactions",
                "EnableFastInteractions",
                false,
                "Speeds up held-click interactions and checkout actions."
            );

            PickupSpeedMultiplier = cfg.Bind(
                "06 - Fast Interactions",
                "PickupSpeedMultiplier",
                4f,
                new ConfigDescription(
                    "Higher is faster for general held-click interactions.",
                    new AcceptableValueRange<float>(0.1f, 20f))
            );

            CheckoutScanMultiplier = cfg.Bind(
                "06 - Fast Interactions",
                "CheckoutScanMultiplier",
                2f,
                new ConfigDescription(
                    "Higher is faster for scanning items at checkout.",
                    new AcceptableValueRange<float>(0.1f, 5f))
            );

            CheckoutChangeMultiplier = cfg.Bind(
                "06 - Fast Interactions",
                "CheckoutChangeMultiplier",
                1f,
                new ConfigDescription(
                    "Higher is faster for giving change at checkout.",
                    new AcceptableValueRange<float>(0.1f, 5f))
            );

            CheckoutScanVolumeMultiplier = cfg.Bind(
                "06 - Fast Interactions",
                "CheckoutScanVolumeMultiplier",
                1f,
                new ConfigDescription(
                    "Volume modifier for the checkout scanner beep. 0 = silent, 1 = normal, 2 = double.",
                    new AcceptableValueRange<float>(0f, 2f))
            );

            FastInteractionsDebugLogging = cfg.Bind(
                "06 - Fast Interactions",
                "FastInteractionsDebugLogging",
                false,
                "Logs mode switches and applied interaction rates for troubleshooting."
            );

            EnableQuickScan = cfg.Bind(
                "06 - Fast Interactions",
                "EnableQuickScan",
                false,
                "When one item or card is scanned at checkout, instantly scans the rest of that customer's bag."
            );

            // Keep all noisy stuff together
            VerboseLogging = cfg.Bind(
                "99 - Debug",
                "VerboseLogging",
                false,
                "Enable extra logging for troubleshooting."
            );

            ScanDebugLogging = cfg.Bind(
                "99 - Debug",
                "ScanDebugLogging",
                false,
                "Logs detailed shelf scan decisions (VERY spammy)."
            );
        }
    }
}