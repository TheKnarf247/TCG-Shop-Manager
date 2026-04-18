#nullable enable
using TMPro;
using UnityEngine;

namespace ShopManager.Patches
{
    internal static class PackOpeningState
    {
        internal static bool IsAutoOpen = false;
        internal static bool DelayAutoOpen = false;
        internal static bool IsGetNewOrHighValueCard = false;
        internal static bool IsCancelPackQueued = false;
        internal static bool IsFirstLoad = true;

        internal static bool IsPackPositionsLoaded = false;
        internal static bool IsPackPositionsReordered = false;
        internal static bool AreBoxesOpen = false;

        internal static float PackSpeedMultiplier = 0f;
        internal static readonly string AutoOpenBaseText = "Auto Open ";

        internal static TextMeshProUGUI? HoldItemCountText;
        internal static TextMeshProUGUI? AutoOpenStatusText;
        internal static GameObject? LastGeneratedUiObject;
    }
}