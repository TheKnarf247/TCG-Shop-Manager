#nullable enable

namespace ShopManager.Patches
{
    internal static class InteractionSpeedState
    {
        internal static object? CurrentIPC;
        internal static float OriginalMouseRate = -1f;
        internal static float CurrentAppliedMultiplier = -1f;
        internal static float LastAppliedRate = -1f;
        internal static bool CheckoutOpen = false;
        internal static bool CheckoutGivingChange = false;
        internal static bool ForceNextApply = false;
    }
}