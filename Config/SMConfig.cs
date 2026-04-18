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

        // Pack Opening Interactions
        internal static ConfigEntry<bool> EnablePackOpeningInteractions = null!;
        internal static ConfigEntry<bool> DisablePackOpeningSounds = null!;
        internal static ConfigEntry<KeyboardShortcut> RemoveEmptyBoxesKey = null!;
        internal static ConfigEntry<KeyboardShortcut> OpenCloseAllBoxesKey = null!;
        internal static ConfigEntry<bool> SpeedUpPackResultsScreen = null!;
        internal static ConfigEntry<int> HighValueThreshold = null!;
        internal static ConfigEntry<float> PackOpeningSpeedMultiplier = null!;
        internal static ConfigEntry<KeyboardShortcut> AutoOpenToggleKey = null!;
        internal static ConfigEntry<bool> StopAutoOpenOnHighValue = null!;
        internal static ConfigEntry<bool> StopAutoOpenOnNewCard = null!;
        internal static ConfigEntry<float> PackResultsTimerSeconds = null!;
        internal static ConfigEntry<bool> ShowAutoOpenStatusText = null!;
        internal static ConfigEntry<float> AutoOpenStatusTextX = null!;
        internal static ConfigEntry<float> AutoOpenStatusTextY = null!;
        internal static ConfigEntry<int> AutoOpenStatusTextSize = null!;
        internal static ConfigEntry<bool> EnableMaxHoldPacks = null!;
        internal static ConfigEntry<int> MaxHoldPacks = null!;
        internal static ConfigEntry<bool> EnableHeldPackRepositioning = null!;
        internal static ConfigEntry<float> HeldPackCountTextX = null!;
        internal static ConfigEntry<float> HeldPackCountTextY = null!;
        internal static ConfigEntry<int> HeldPackCountTextSize = null!;

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

            EnablePackOpeningInteractions = cfg.Bind(
                "07 - Pack Opening Interactions",
                "EnablePackOpeningInteractions",
                true,
                "Enable the pack opening interactions module."
);

            DisablePackOpeningSounds = cfg.Bind(
                "07 - Pack Opening Interactions",
                "DisablePackOpeningSounds",
                false,
                "Disables most pack opening sounds except high value/new card cues."
            );

            RemoveEmptyBoxesKey = cfg.Bind(
                "07 - Pack Opening Interactions",
                "RemoveEmptyBoxesKey",
                new KeyboardShortcut(KeyCode.Delete),
                "Removes all empty non-stored boxes on the floor/shelves, excluding the box currently in your hands."
            );

            OpenCloseAllBoxesKey = cfg.Bind(
                "07 - Pack Opening Interactions",
                "OpenCloseAllBoxesKey",
                new KeyboardShortcut(KeyCode.O),
                "Toggles open/close for all non-stored boxes except the one currently in your hands."
            );

            SpeedUpPackResultsScreen = cfg.Bind(
                "07 - Pack Opening Interactions",
                "SpeedUpPackResultsScreen",
                false,
                "Speeds up the animation at the end of opening a pack."
            );

            HighValueThreshold = cfg.Bind(
                "07 - Pack Opening Interactions",
                "HighValueThreshold",
                10,
                new ConfigDescription(
                    "Cards at or above this value are treated as high value.",
                    new AcceptableValueRange<int>(1, 10000))
            );

            PackOpeningSpeedMultiplier = cfg.Bind(
                "07 - Pack Opening Interactions",
                "PackOpeningSpeedMultiplier",
                1f,
                new ConfigDescription(
                    "Speed multiplier for pack opening flow.",
                    new AcceptableValueRange<float>(1f, 100f))
            );

            AutoOpenToggleKey = cfg.Bind(
                "07 - Pack Opening Interactions",
                "AutoOpenToggleKey",
                new KeyboardShortcut(KeyCode.BackQuote),
                "Toggle automatic opening of packs."
            );

            StopAutoOpenOnHighValue = cfg.Bind(
                "07 - Pack Opening Interactions",
                "StopAutoOpenOnHighValue",
                true,
                "Stops automatic opening when a high value card is pulled."
            );

            StopAutoOpenOnNewCard = cfg.Bind(
                "07 - Pack Opening Interactions",
                "StopAutoOpenOnNewCard",
                true,
                "Stops automatic opening when a new card is pulled."
            );

            PackResultsTimerSeconds = cfg.Bind(
                "07 - Pack Opening Interactions",
                "PackResultsTimerSeconds",
                1f,
                new ConfigDescription(
                    "How long the pack results screen stays visible before auto-open continues.",
                    new AcceptableValueRange<float>(0f, 10f))
            );

            ShowAutoOpenStatusText = cfg.Bind(
                "07 - Pack Opening Interactions",
                "ShowAutoOpenStatusText",
                true,
                "Shows on-screen text indicating whether auto-open is enabled."
            );

            AutoOpenStatusTextX = cfg.Bind(
                "07 - Pack Opening Interactions",
                "AutoOpenStatusTextX",
                120f,
                new ConfigDescription(
                    "X position for auto-open status text.",
                    new AcceptableValueRange<float>(0f, Screen.currentResolution.width))
            );

            AutoOpenStatusTextY = cfg.Bind(
                "07 - Pack Opening Interactions",
                "AutoOpenStatusTextY",
                985f,
                new ConfigDescription(
                    "Y position for auto-open status text.",
                    new AcceptableValueRange<float>(0f, Screen.currentResolution.height))
            );

            AutoOpenStatusTextSize = cfg.Bind(
                "07 - Pack Opening Interactions",
                "AutoOpenStatusTextSize",
                16,
                new ConfigDescription(
                    "Font size for auto-open status text.",
                    new AcceptableValueRange<int>(1, 32))
            );

            EnableMaxHoldPacks = cfg.Bind(
                "07 - Pack Opening Interactions",
                "EnableMaxHoldPacks",
                true,
                "Allow holding more than 8 packs."
            );

            MaxHoldPacks = cfg.Bind(
                "07 - Pack Opening Interactions",
                "MaxHoldPacks",
                8,
                new ConfigDescription(
                    "Maximum number of packs that can be held.",
                    new AcceptableValueRange<int>(1, 1024))
            );

            EnableHeldPackRepositioning = cfg.Bind(
                "07 - Pack Opening Interactions",
                "EnableHeldPackRepositioning",
                false,
                "Repositions held packs into multiple rows instead of one long line."
            );

            HeldPackCountTextX = cfg.Bind(
                "07 - Pack Opening Interactions",
                "HeldPackCountTextX",
                120f,
                new ConfigDescription(
                    "X position for held pack count text.",
                    new AcceptableValueRange<float>(0f, Screen.currentResolution.width))
            );

            HeldPackCountTextY = cfg.Bind(
                "07 - Pack Opening Interactions",
                "HeldPackCountTextY",
                960f,
                new ConfigDescription(
                    "Y position for held pack count text.",
                    new AcceptableValueRange<float>(0f, Screen.currentResolution.height))
            );

            HeldPackCountTextSize = cfg.Bind(
                "07 - Pack Opening Interactions",
                "HeldPackCountTextSize",
                26,
                new ConfigDescription(
                    "Font size for held pack count text.",
                    new AcceptableValueRange<int>(1, 32))
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