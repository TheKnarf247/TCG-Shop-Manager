using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;
using System.Collections;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch(typeof(InteractableOpenCloseSign))]
    internal static class InteractableOpenCloseSignPatches
    {
        [HarmonyPatch("OnDayStarted")]
        [HarmonyPostfix]
        private static void OnDayStartedPostfix(InteractableOpenCloseSign __instance)
        {
            if (!SMConfig.EnableShopOpenAutomation.Value)
                return;

            __instance.StartCoroutine(OpenShopAfterDelay(__instance));
        }

        private static IEnumerator OpenShopAfterDelay(InteractableOpenCloseSign sign)
        {
            float delaySeconds = SMConfig.ShopOpenAutomationDelayMinutes.Value == 0
                ? 5f
                : SMConfig.ShopOpenAutomationDelayMinutes.Value * 60f;

            yield return new WaitForSeconds(delaySeconds);

            if (!CPlayerData.m_IsShopOpen)
            {
                SMLog.Debug("[ShopOpenAutomation] Opening shop automatically.");
                sign.OnMouseButtonUp();
            }
        }
    }
}