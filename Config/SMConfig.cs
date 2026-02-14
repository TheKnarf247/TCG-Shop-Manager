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

        // Hotkey
        internal static ConfigEntry<KeyboardShortcut> RefillHotkey = null!;
        internal static ConfigEntry<bool> RefillHotkeyRequiresShift = null!;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind(
                "00 - General",
                "Enabled",
                true,
                "Enable/disable Shop Manager."
            );

            VerboseLogging = cfg.Bind(
                "00 - General",
                "VerboseLogging",
                false,
                "Enable extra logging for troubleshooting."
            );

            RefillHotkey = cfg.Bind(
                "01 - Hotkeys",
                "RefillHotkey",
                new KeyboardShortcut(KeyCode.F7),
                "Hotkey to run a refill pass."
            );

            RefillHotkeyRequiresShift = cfg.Bind(
                "01 - Hotkeys",
                "RefillHotkeyRequiresShift",
                false,
                "If true, the refill hotkey requires holding Shift."
            );
        }
    }
}
