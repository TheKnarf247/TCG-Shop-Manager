#nullable enable
using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ShopManager.Config
{
    internal static class SMConfig
    {
        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<bool> VerboseLogging = null!;

        internal static ConfigEntry<KeyboardShortcut> RefillHotkey = null!;

        // NEW: chunking controls
        internal static ConfigEntry<int> ScanCompartmentsPerFrame = null!;
        internal static ConfigEntry<int> PlacementsPerFrame = null!;
        internal static ConfigEntry<int> LogProgressEveryN = null!;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("00 - General", "Enabled", true, "Enable/disable Shop Manager.");
            VerboseLogging = cfg.Bind("00 - General", "VerboseLogging", false, "Enable extra logging for troubleshooting.");

            RefillHotkey = cfg.Bind("01 - Hotkeys", "RefillHotkey", new KeyboardShortcut(KeyCode.F7), "Hotkey to run a refill pass.");

            ScanCompartmentsPerFrame = cfg.Bind("02 - Performance", "ScanCompartmentsPerFrame", 200,
                "How many shop compartments to inspect per frame during the scan phase.");

            PlacementsPerFrame = cfg.Bind("02 - Performance", "PlacementsPerFrame", 25,
                "How many item placements to perform per frame during the fill phase.");

            LogProgressEveryN = cfg.Bind("02 - Performance", "LogProgressEveryN", 500,
                "Log progress every N scanned compartments (0 disables progress logging).");
        }
    }
}