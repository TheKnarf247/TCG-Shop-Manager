#nullable enable
using System.Reflection;
using HarmonyLib;
using ShopManager.Config;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch]
    internal static class CheckoutInteractionPatch
    {
        private static FieldInfo? s_currentCashierCounterField;
        private static bool s_leftDidAutoFire;
        private static bool s_rightDidAutoFire;

        private static bool IsPlayerCounter(InteractableCashierCounter instance)
        {
            if (InteractionSpeedState.CurrentIPC == null)
                return false;

            if (s_currentCashierCounterField == null)
            {
                s_currentCashierCounterField = typeof(InteractionPlayerController)
                    .GetField("m_CurrentCashierCounter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_currentCashierCounterField == null)
                return false;

            return ReferenceEquals(s_currentCashierCounterField.GetValue(InteractionSpeedState.CurrentIPC), instance);
        }

        internal static void TryApplyToOpenCheckout()
        {
            InteractionSpeedState.ForceNextApply = true;
            FastInteractionsPatch.TryApplyCached();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractableCashierCounter), "OnMouseButtonUp")]
        private static void Cashier_OnMouseButtonUp_Postfix(InteractableCashierCounter __instance)
        {
            InteractionSpeedState.CheckoutOpen = true;
            InteractionSpeedState.CheckoutGivingChange = __instance.IsGivingChange();
            FastInteractionsPatch.TryApplyCached();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractableCashierCounter), "OnPressEsc")]
        private static void Cashier_OnPressEsc_Postfix()
        {
            InteractionSpeedState.CheckoutOpen = false;
            InteractionSpeedState.CheckoutGivingChange = false;
            FastInteractionsPatch.TryApplyCached();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractableCashierCounter), "UpdateCashierCounterState")]
        private static void Cashier_UpdateCashierCounterState_Postfix(InteractableCashierCounter __instance, ECashierCounterState state)
        {
            if (!InteractionSpeedState.CheckoutOpen || !IsPlayerCounter(__instance))
                return;

            bool wasGivingChange = InteractionSpeedState.CheckoutGivingChange;
            InteractionSpeedState.CheckoutGivingChange = state == ECashierCounterState.GivingChange;

            if (wasGivingChange != InteractionSpeedState.CheckoutGivingChange)
            {
                InteractionSpeedState.ForceNextApply = true;
                FastInteractionsPatch.TryApplyCached();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractableCashierCounter), "StartGivingChange")]
        private static void Cashier_StartGivingChange_Postfix(InteractableCashierCounter __instance)
        {
            if (!InteractionSpeedState.CheckoutOpen || !IsPlayerCounter(__instance))
                return;

            if (!InteractionSpeedState.CheckoutGivingChange)
            {
                InteractionSpeedState.CheckoutGivingChange = true;
                InteractionSpeedState.ForceNextApply = true;
                FastInteractionsPatch.TryApplyCached();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractableCashierCounter), "OnDestroyed")]
        private static void Cashier_OnDestroyed_Postfix()
        {
            InteractionSpeedState.CheckoutOpen = false;
            InteractionSpeedState.CheckoutGivingChange = false;
            FastInteractionsPatch.TryApplyCached();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InteractionPlayerController), "RaycastCashCounterState")]
        private static void RaycastCashCounterState_Prefix(
            ref float ___m_MouseDownTime,
            ref float ___m_RightMouseDownTime,
            bool ___m_IsHoldingMouseDown,
            bool ___m_IsHoldingRightMouseDown,
            float ___m_MouseHoldAutoFireRate)
        {
            if (!SMConfig.Enabled.Value || !SMConfig.EnableFastInteractions.Value)
                return;

            if (___m_IsHoldingMouseDown)
            {
                if (___m_MouseDownTime + Time.deltaTime >= ___m_MouseHoldAutoFireRate)
                {
                    s_leftDidAutoFire = true;
                }
            }
            else
            {
                if (s_leftDidAutoFire && ___m_MouseDownTime > 0f)
                {
                    ___m_MouseDownTime = 0f;
                }

                s_leftDidAutoFire = false;
            }

            if (___m_IsHoldingRightMouseDown)
            {
                if (___m_RightMouseDownTime + Time.deltaTime >= ___m_MouseHoldAutoFireRate)
                {
                    s_rightDidAutoFire = true;
                }
            }
            else
            {
                if (s_rightDidAutoFire && ___m_RightMouseDownTime > 0f)
                {
                    ___m_RightMouseDownTime = 0f;
                }

                s_rightDidAutoFire = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SoundManager), "PlayAudio")]
        private static void SoundManager_PlayAudio_Prefix(string audioName, ref float volume)
        {
            if (!SMConfig.Enabled.Value || !SMConfig.EnableFastInteractions.Value)
                return;

            if (audioName == "SFX_CheckoutScan")
            {
                volume *= SMConfig.CheckoutScanVolumeMultiplier.Value;
            }
        }
    }
}