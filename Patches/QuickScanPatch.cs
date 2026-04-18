#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch]
    internal static class QuickScanPatch
    {
        private static FieldInfo? s_customerItemBagListField;
        private static FieldInfo? s_customerCardBagListField;
        private static FieldInfo? s_scanItemCurrentCustomerField;
        private static FieldInfo? s_scanItemLerpPosField;
        private static FieldInfo? s_cardCurrentCustomerField;
        private static FieldInfo? s_cardLerpPosField;
        private static MethodInfo? s_customerEvaluateFinishScanItemMethod;

        private static float s_lastScanSoundTime = float.MinValue;
        private static bool s_isBulkScanning;

        private static void InitReflection()
        {
            if (s_customerItemBagListField == null)
            {
                s_customerItemBagListField = typeof(Customer).GetField("m_ItemInBagList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_customerCardBagListField == null)
            {
                s_customerCardBagListField = typeof(Customer).GetField("m_CardInBagList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_scanItemCurrentCustomerField == null)
            {
                s_scanItemCurrentCustomerField = typeof(InteractableScanItem).GetField("m_CurrentCustomer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_scanItemLerpPosField == null)
            {
                s_scanItemLerpPosField = typeof(InteractableScanItem).GetField("m_ScannedItemLerpPos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_cardCurrentCustomerField == null)
            {
                s_cardCurrentCustomerField = typeof(InteractableCard3d).GetField("m_CurrentCustomer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_cardLerpPosField == null)
            {
                s_cardLerpPosField = typeof(InteractableCard3d).GetField("m_ScannedItemLerpPos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_customerEvaluateFinishScanItemMethod == null)
            {
                s_customerEvaluateFinishScanItemMethod = typeof(Customer).GetMethod("EvaluateFinishScanItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        private static bool FeatureEnabled =>
            SMConfig.Enabled.Value && SMConfig.EnableQuickScan.Value;

        private static void EvaluateFinish(Customer customer)
        {
            if (s_customerEvaluateFinishScanItemMethod == null)
                return;

            s_customerEvaluateFinishScanItemMethod.Invoke(customer, null);
        }

        private static void ScanAllItems(
            Customer customer,
            Item? scannedItem,
            ref int scannedCount,
            InteractableCashierCounter cashierCounter,
            ref float totalScannedItemCost)
        {
            InitReflection();

            if (s_customerItemBagListField == null ||
                s_scanItemCurrentCustomerField == null ||
                s_scanItemLerpPosField == null)
            {
                SMLog.Warn("[QuickScan] Failed to reflect required item scan fields.");
                return;
            }

            if (!(s_customerItemBagListField.GetValue(customer) is List<Item> bagItems))
                return;

            foreach (InteractableScanItem scanItem in Resources.FindObjectsOfTypeAll<InteractableScanItem>())
            {
                try
                {
                    if (scanItem == null || scanItem.m_Item == null)
                        continue;

                    if (scanItem.m_Item == scannedItem)
                        continue;

                    if (!bagItems.Contains(scanItem.m_Item))
                        continue;

                    scanItem.m_Item.m_Collider.enabled = false;
                    scanItem.m_Item.m_Rigidbody.isKinematic = true;

                    float currentPrice = scanItem.m_Item.GetCurrentPrice();
                    scannedCount++;

                    cashierCounter.AddScannedItemCostTotal((double)currentPrice, scanItem.m_Item.GetItemType());

                    totalScannedItemCost += currentPrice;
                    totalScannedItemCost =
                        (float)Mathf.RoundToInt(totalScannedItemCost * GameInstance.GetCurrencyRoundDivideAmount()) /
                        GameInstance.GetCurrencyRoundDivideAmount();

                    s_scanItemCurrentCustomerField.SetValue(scanItem, null);

                    if (s_scanItemLerpPosField.GetValue(scanItem) is Transform lerpPos)
                    {
                        scanItem.LerpToTransform(lerpPos, lerpPos, 3f);
                        scanItem.SetHideItemAfterFinishLerp();
                    }
                }
                catch (Exception ex)
                {
                    SMLog.Warn("[QuickScan] Error bulk-scanning item: " + ex);
                }
            }
        }

        private static void ScanAllCards(
            Customer customer,
            InteractableCard3d? scannedCard,
            ref int scannedCount,
            InteractableCashierCounter cashierCounter,
            ref float totalScannedItemCost)
        {
            InitReflection();

            if (s_customerCardBagListField == null ||
                s_cardCurrentCustomerField == null ||
                s_cardLerpPosField == null)
            {
                SMLog.Warn("[QuickScan] Failed to reflect required card scan fields.");
                return;
            }

            if (!(s_customerCardBagListField.GetValue(customer) is List<InteractableCard3d> bagCards))
                return;

            foreach (InteractableCard3d card in Resources.FindObjectsOfTypeAll<InteractableCard3d>())
            {
                try
                {
                    if (card == null)
                        continue;

                    if (card == scannedCard)
                        continue;

                    if (!bagCards.Contains(card))
                        continue;

                    card.m_Collider.enabled = false;
                    card.m_Rigidbody.isKinematic = true;

                    float currentPrice = card.GetCurrentPrice();
                    scannedCount++;

                    cashierCounter.AddScannedCardCostTotal(
                        (double)currentPrice,
                        card.m_Card3dUI.m_CardUI.GetCardData());

                    totalScannedItemCost += currentPrice;
                    totalScannedItemCost =
                        (float)Mathf.RoundToInt(totalScannedItemCost * GameInstance.GetCurrencyRoundDivideAmount()) /
                        GameInstance.GetCurrencyRoundDivideAmount();

                    s_cardCurrentCustomerField.SetValue(card, null);

                    if (s_cardLerpPosField.GetValue(card) is Transform lerpPos)
                    {
                        card.LerpToTransform(lerpPos, lerpPos, 3f);
                        card.SetHideItemAfterFinishLerp();
                    }
                }
                catch (Exception ex)
                {
                    SMLog.Warn("[QuickScan] Error bulk-scanning card: " + ex);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Customer), "OnItemScanned")]
        private static void Customer_OnItemScanned_Postfix(
            Customer __instance,
            Item item,
            ref int ___m_ItemScannedCount,
            InteractableCashierCounter ___m_CurrentQueueCashierCounter,
            ref float ___m_TotalScannedItemCost)
        {
            if (!FeatureEnabled || s_isBulkScanning)
                return;

            if (__instance == null || ___m_CurrentQueueCashierCounter == null)
                return;

            try
            {
                s_isBulkScanning = true;

                ScanAllItems(__instance, item, ref ___m_ItemScannedCount, ___m_CurrentQueueCashierCounter, ref ___m_TotalScannedItemCost);
                ScanAllCards(__instance, null, ref ___m_ItemScannedCount, ___m_CurrentQueueCashierCounter, ref ___m_TotalScannedItemCost);
                EvaluateFinish(__instance);
            }
            catch (Exception ex)
            {
                SMLog.Warn("[QuickScan] Customer.OnItemScanned postfix failed: " + ex);
            }
            finally
            {
                s_isBulkScanning = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Customer), "OnCardScanned")]
        private static void Customer_OnCardScanned_Postfix(
            Customer __instance,
            InteractableCard3d card,
            ref int ___m_ItemScannedCount,
            InteractableCashierCounter ___m_CurrentQueueCashierCounter,
            ref float ___m_TotalScannedItemCost)
        {
            if (!FeatureEnabled || s_isBulkScanning)
                return;

            if (__instance == null || ___m_CurrentQueueCashierCounter == null)
                return;

            try
            {
                s_isBulkScanning = true;

                ScanAllItems(__instance, null, ref ___m_ItemScannedCount, ___m_CurrentQueueCashierCounter, ref ___m_TotalScannedItemCost);
                ScanAllCards(__instance, card, ref ___m_ItemScannedCount, ___m_CurrentQueueCashierCounter, ref ___m_TotalScannedItemCost);
                EvaluateFinish(__instance);
            }
            catch (Exception ex)
            {
                SMLog.Warn("[QuickScan] Customer.OnCardScanned postfix failed: " + ex);
            }
            finally
            {
                s_isBulkScanning = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SoundManager), "PlayAudio")]
        private static bool SoundManager_PlayAudio_Prefix(string audioName)
        {
            if (!FeatureEnabled)
                return true;

            if (audioName != "SFX_CheckoutScan")
                return true;

            // Hardwired values
            const float cooldown = 0.5f;

            if (Time.time - s_lastScanSoundTime < cooldown)
                return false;

            s_lastScanSoundTime = Time.time;
            return true;
        }
    }
}