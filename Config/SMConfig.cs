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

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("00 - General", "Enabled", true, "Enable/disable Shop Manager.");
            VerboseLogging = cfg.Bind("00 - General", "VerboseLogging", false, "Enable extra logging for troubleshooting.");

            RefillHotkey = cfg.Bind("01 - Hotkeys", "RefillHotkey", new KeyboardShortcut(KeyCode.F7), "Hotkey to run a refill pass.");
        }
    }
}