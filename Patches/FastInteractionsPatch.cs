#nullable enable
using System;
using System.Reflection;
using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch]
    internal static class FastInteractionsPatch
    {
        private static FieldInfo? s_autoFireRateField;
        private static FieldInfo? s_currentTrashBinField;
        private static FieldInfo? s_currentAutoCleanserField;
        private static FieldInfo? s_currentRaycastObjectField;
        private static bool s_updateErrorLogged;

        private static void LogFastDebug(string msg)
        {
            if (!SMConfig.FastInteractionsDebugLogging.Value) return;
            SMLog.Info("[FastInteractions] " + msg);
        }

        private static void InitReflection(object ipc)
        {
            if (s_autoFireRateField != null) return;

            Type type = ipc.GetType();
            s_autoFireRateField = type.GetField("m_MouseHoldAutoFireRate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            s_currentTrashBinField = type.GetField("m_CurrentTrashBin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            s_currentAutoCleanserField = type.GetField("m_CurrentAutoCleanser", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            s_currentRaycastObjectField = type.GetField("m_CurrentRaycastObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static void Apply(object? ipc)
        {
            if (ipc == null) return;

            InteractionSpeedState.CurrentIPC = ipc;
            InitReflection(ipc);

            if (s_autoFireRateField == null)
            {
                SMLog.Warn("[FastInteractions] m_MouseHoldAutoFireRate not found on InteractionPlayerController.");
                return;
            }

            float currentRate = (float)s_autoFireRateField.GetValue(ipc);

            if (InteractionSpeedState.OriginalMouseRate < 0f)
            {
                InteractionSpeedState.OriginalMouseRate = currentRate;
            }
            else if (InteractionSpeedState.LastAppliedRate >= 0f &&
                     Mathf.Abs(currentRate - InteractionSpeedState.LastAppliedRate) > 0.0001f)
            {
                InteractionSpeedState.OriginalMouseRate = currentRate;
            }

            float multiplier = 1f;
            bool forceNextApply = InteractionSpeedState.ForceNextApply;
            InteractionSpeedState.ForceNextApply = false;

            bool featureEnabled = SMConfig.Enabled.Value && SMConfig.EnableFastInteractions.Value;

            if (featureEnabled)
            {
                if (InteractionSpeedState.CheckoutOpen)
                {
                    if (InteractionSpeedState.CheckoutGivingChange)
                    {
                        multiplier = Mathf.Clamp(SMConfig.CheckoutChangeMultiplier.Value, 0.1f, 5f);
                    }
                    else
                    {
                        multiplier = Mathf.Clamp(SMConfig.CheckoutScanMultiplier.Value, 0.1f, 5f);
                    }
                }
                else
                {
                    bool excluded = false;
                    InteractionPlayerController? typedIpc = ipc as InteractionPlayerController;

                    if (typedIpc != null)
                    {
                        if (typedIpc.m_CurrentGameState.ToString() == "HoldSprayState")
                        {
                            excluded = true;
                        }

                        if (!excluded)
                        {
                            object? currentTrashBin = s_currentTrashBinField?.GetValue(ipc);
                            object? currentAutoCleanser = s_currentAutoCleanserField?.GetValue(ipc);
                            object? currentRaycastObject = s_currentRaycastObjectField?.GetValue(ipc);

                            if (currentTrashBin != null || currentAutoCleanser != null)
                            {
                                excluded = true;
                            }
                            else if (currentRaycastObject != null)
                            {
                                string name = currentRaycastObject.GetType().Name;
                                if (name.Contains("TrashBin") || name.Contains("Cleanser") || name.Contains("Customer"))
                                {
                                    excluded = true;
                                }
                            }
                        }
                    }

                    if (!excluded)
                    {
                        multiplier = Mathf.Clamp(SMConfig.PickupSpeedMultiplier.Value, 0.1f, 5f);
                    }
                }
            }

            float newRate = Mathf.Max(0.01f, InteractionSpeedState.OriginalMouseRate / multiplier);

            if (forceNextApply || Mathf.Abs(currentRate - newRate) > 0.0001f)
            {
                s_autoFireRateField.SetValue(ipc, newRate);
                InteractionSpeedState.LastAppliedRate = newRate;
                InteractionSpeedState.CurrentAppliedMultiplier = multiplier;

                string mode =
                    InteractionSpeedState.CheckoutOpen
                        ? (InteractionSpeedState.CheckoutGivingChange ? "CheckoutChange" : "CheckoutScan")
                        : (Mathf.Abs(multiplier - 1f) < 0.0001f ? "Vanilla/Excluded" : "Pickup");

                LogFastDebug($"{mode} rate applied: base={InteractionSpeedState.OriginalMouseRate:0.000}s mult={multiplier:0.00} new={newRate:0.000}s");
            }
            else
            {
                InteractionSpeedState.LastAppliedRate = currentRate;
                InteractionSpeedState.CurrentAppliedMultiplier = multiplier;
            }
        }

        internal static void TryApplyCached()
        {
            if (InteractionSpeedState.CurrentIPC != null)
            {
                Apply(InteractionSpeedState.CurrentIPC);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionPlayerController), "Update")]
        private static void Update_Postfix(object __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception ex)
            {
                if (!s_updateErrorLogged)
                {
                    SMLog.Warn("[FastInteractions] Update postfix failed: " + ex);
                    s_updateErrorLogged = true;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionPlayerController), "Start")]
        private static void Start_Postfix(object __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception ex)
            {
                SMLog.Warn("[FastInteractions] Start postfix failed: " + ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionPlayerController), "OnGameDataFinishLoaded")]
        private static void OnGameDataFinishLoaded_Postfix(object __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception ex)
            {
                SMLog.Warn("[FastInteractions] OnGameDataFinishLoaded postfix failed: " + ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionPlayerController), "OnFinishHideLoadingScreen")]
        private static void OnFinishHideLoadingScreen_Postfix(object __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception ex)
            {
                SMLog.Warn("[FastInteractions] OnFinishHideLoadingScreen postfix failed: " + ex);
            }
        }
    }
}