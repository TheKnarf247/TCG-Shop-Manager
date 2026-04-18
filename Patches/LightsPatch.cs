using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;

namespace ShopManager.Patches
{
    [HarmonyPatch]
    internal static class LightsPatch
    {
        private static bool triggered;
        private static bool loggedError;

        [HarmonyPatch(typeof(LightManager), "Update")]
        [HarmonyPostfix]
        private static void LightManagerUpdatePostfix(ref LightManager __instance, ref bool ___m_IsShopLightOn, ref int ___m_TimeHour, ref int ___m_TimeMin)
        {
            if (!SMConfig.EnableAutoLights.Value)
                return;

            if (CSingleton<PauseScreen>.Instance.m_ScreenGrp.activeSelf)
                return;

            string value = SMConfig.AutoLightOnTime.Value;

            if (string.IsNullOrWhiteSpace(value) ||
                value.Length != 5 ||
                value[2] != ':' ||
                !int.TryParse(value.Substring(0, 2), out int hour) ||
                !int.TryParse(value.Substring(3, 2), out int minute) ||
                hour < 0 || hour > 23 ||
                minute < 0 || minute > 59)
            {
                if (!loggedError)
                {
                    SMLog.Error("AutoLightOnTime is invalid. Use HH:mm format, e.g. 16:00.");
                    loggedError = true;
                }
                return;
            }

            loggedError = false;

            if (!___m_IsShopLightOn && ___m_TimeHour == hour && ___m_TimeMin == minute && !triggered)
            {
                triggered = true;
                __instance.ToggleShopLight();
            }
        }

        [HarmonyPatch(typeof(LightManager), "GoNextDay")]
        [HarmonyPostfix]
        private static void LightManagerGoNextDayPostfix()
        {
            triggered = false;
        }
    }
}