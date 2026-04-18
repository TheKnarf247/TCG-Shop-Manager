#nullable enable
using System;
using System.Collections.Generic;
using ShopManager.Config;
using ShopManager.Logging;
using TMPro;
using UnityEngine;

namespace ShopManager.Patches
{
    internal static class PackOpeningController
    {
        internal static bool FeatureEnabled =>
            SMConfig.Enabled.Value && SMConfig.EnablePackOpeningInteractions.Value;

        internal static float SpeedMultiplierValue =>
            SMConfig.PackOpeningSpeedMultiplier.Value;

        internal static float PackResultsTimerValue =>
            SMConfig.PackResultsTimerSeconds.Value;

        internal static bool DisableSoundsValue =>
            SMConfig.DisablePackOpeningSounds.Value;

        internal static bool SkipPackEndScreenValue =>
            SMConfig.SpeedUpPackResultsScreen.Value;

        internal static bool EnableMaxHoldPacksValue =>
            SMConfig.EnableMaxHoldPacks.Value;

        internal static int MaxHoldPacksValue =>
            SMConfig.MaxHoldPacks.Value;

        internal static bool EnableHeldItemPositionsValue =>
            SMConfig.EnableHeldPackRepositioning.Value;

        internal static void Tick()
        {
            if (!FeatureEnabled)
                return;

            try
            {
                if (!CSingleton<CGameManager>.Instance.m_IsGameLevel)
                {
                    PackOpeningState.AreBoxesOpen = false;
                    return;
                }

                if (SMConfig.AutoOpenToggleKey.Value.IsDown())
                {
                    ToggleAutoOpen();
                }

                if (SMConfig.RemoveEmptyBoxesKey.Value.IsDown())
                {
                    RemoveEmptyBoxes();
                }

                if (SMConfig.OpenCloseAllBoxesKey.Value.IsDown())
                {
                    OpenCloseAllBoxes(PackOpeningState.AreBoxesOpen);
                }
            }
            catch (Exception ex)
            {
                SMLog.Warn("[PackOpening] Tick failed: " + ex);
            }
        }

        internal static void ToggleAutoOpen()
        {
            if (PackOpeningState.IsAutoOpen || PackOpeningState.DelayAutoOpen)
            {
                PackOpeningState.IsAutoOpen = false;
                PackOpeningState.DelayAutoOpen = false;

                if (PackOpeningState.AutoOpenStatusText != null)
                {
                    PackOpeningState.AutoOpenStatusText.text = PackOpeningState.AutoOpenBaseText + "Disabled";
                    PackOpeningState.AutoOpenStatusText.color = Color.red;
                }

                return;
            }

            PackOpeningState.IsAutoOpen = true;

            if (PackOpeningState.AutoOpenStatusText != null)
            {
                PackOpeningState.AutoOpenStatusText.text = PackOpeningState.AutoOpenBaseText + "Enabled";
                PackOpeningState.AutoOpenStatusText.color = Color.green;
            }
        }

        internal static void DuplicateUIComponents(GameUIScreen gameUIScreen, ref TextMeshProUGUI? tmpro, string name)
        {
            PackOpeningState.LastGeneratedUiObject = new GameObject(name);

            Transform parent = gameUIScreen.m_DayText.transform.GetComponentInParent<RectTransform>().transform.parent;
            PackOpeningState.LastGeneratedUiObject.transform.SetParent(parent, false);

            RectTransform rectTransform = PackOpeningState.LastGeneratedUiObject.AddComponent<RectTransform>();
            RectTransform sourceRect = gameUIScreen.m_DayText.transform.GetComponentInParent<RectTransform>();

            rectTransform.anchorMin = sourceRect.anchorMin;
            rectTransform.anchorMax = sourceRect.anchorMax;
            rectTransform.pivot = sourceRect.pivot;
            rectTransform.anchoredPosition = sourceRect.anchoredPosition;
            rectTransform.sizeDelta = sourceRect.sizeDelta;

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 0f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(200f, 100f);

            tmpro = UnityEngine.Object.Instantiate(gameUIScreen.m_DayText, rectTransform);
            tmpro.text = "";
            tmpro.fontSizeMax = SMConfig.HeldPackCountTextSize.Value;
            tmpro.fontSize = SMConfig.HeldPackCountTextSize.Value;
            tmpro.fontSizeMin = 1f;
            tmpro.autoSizeTextContainer = false;
            tmpro.alignment = TextAlignmentOptions.Left;
        }

        internal static void RemoveEmptyBoxes()
        {
            if (!FeatureEnabled || !CSingleton<CGameManager>.Instance.m_IsGameLevel)
                return;

            bool playedSound = false;
            List<InteractablePackagingBox_Item> boxes =
                new List<InteractablePackagingBox_Item>(UnityEngine.Object.FindObjectsOfType<InteractablePackagingBox_Item>());

            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (box == null)
                    continue;

                if (box.m_IsStored)
                    continue;

                if (box == CSingleton<InteractionPlayerController>.Instance.m_CurrentHoldingItemBox)
                    continue;

                if (box.m_ItemCompartment == null)
                    continue;

                if (box.m_ItemCompartment.m_ItemAmount != 0)
                    continue;

                box.OnDestroyed();

                if (!playedSound)
                {
                    SoundManager.PlayAudio("SFX_Dispose", 0.6f, 1f);
                    playedSound = true;
                }
            }
        }

        internal static void OpenCloseAllBoxes(bool isClose)
        {
            if (!FeatureEnabled || !CSingleton<CGameManager>.Instance.m_IsGameLevel)
                return;

            bool playedSound = false;
            List<InteractablePackagingBox_Item> boxes =
                new List<InteractablePackagingBox_Item>(UnityEngine.Object.FindObjectsOfType<InteractablePackagingBox_Item>());

            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (box == null)
                    continue;

                if (box == CSingleton<InteractionPlayerController>.Instance.m_CurrentHoldingItemBox)
                    continue;

                if (box.m_IsStored)
                    continue;

                if (box.m_IsTogglingOpenClose)
                    continue;

                if (!isClose && !box.IsBoxOpened())
                {
                    box.SetOpenCloseBox(true, false);

                    if (!playedSound)
                    {
                        SoundManager.PlayAudio("SFX_BoxOpen", 0.5f, 1f);
                        playedSound = true;
                    }

                    PackOpeningState.AreBoxesOpen = true;
                }

                if (isClose && box.IsBoxOpened())
                {
                    box.SetOpenCloseBox(false, false);

                    if (!playedSound)
                    {
                        SoundManager.PlayAudio("SFX_BoxClose", 0.5f, 1f);
                        playedSound = true;
                    }

                    PackOpeningState.AreBoxesOpen = false;
                }
            }
        }

        internal static List<Transform>? MovePackPositions()
        {
            if (CSingleton<InteractionPlayerController>.Instance.m_HoldCardPackPosList == null ||
                CSingleton<InteractionPlayerController>.Instance.m_HoldCardPackPosList.Count == 0)
            {
                return null;
            }

            if (!CSingleton<CGameManager>.Instance.m_IsGameLevel)
                return null;

            List<Transform> holdCardPackPosList = CSingleton<InteractionPlayerController>.Instance.m_HoldCardPackPosList;

            Transform parent = UnityEngine.Object.Instantiate(
                GameObject.Find("HoldPackPosition"),
                GameObject.Find("FOVAdjustedPositionLoc_Grp").transform,
                false).transform;
            parent.name = "HoldPackParentTransform";

            Transform base0 = UnityEngine.Object.Instantiate(
                GameObject.Find("HoldPackPositionLoc"),
                GameObject.Find("HoldPackPosition").transform,
                false).transform;
            base0.name = "HoldPackBaseTransform";

            Transform base1 = UnityEngine.Object.Instantiate(
                GameObject.Find("HoldPackPositionLoc (1)"),
                GameObject.Find("HoldPackPosition").transform,
                false).transform;
            base1.name = "HoldPackBaseTransform (1)";

            Vector3 localPosition = base0.localPosition;
            Vector3 localPosition2 = base1.localPosition;
            Quaternion rotation = base0.rotation;

            int existingCount = holdCardPackPosList.Count;
            int maxPositions = 1024;
            int packsPerColumn = 8;
            int columnsPerLayer = 8;
            float scaledHeldPackScale = 0.85f;
            float compressedXSpacing = 0.055f;

            float xSpacing = EnableHeldItemPositionsValue ? compressedXSpacing : (localPosition2.x - localPosition.x);
            float ySpacing = localPosition2.y - localPosition.y;
            float zSpacing = localPosition2.z - localPosition.z;

            if (holdCardPackPosList.Count < maxPositions)
            {
                for (int i = 0; i < maxPositions; i++)
                {
                    Transform t = UnityEngine.Object.Instantiate(base0, parent);
                    t.name = i == 0 ? "HoldPackPositionLoc" : $"HoldPackPositionLoc ({i})";
                    t.rotation = rotation;
                    t.localScale = base0.localScale;
                    t.localPosition = localPosition + new Vector3(xSpacing, ySpacing, zSpacing) * i;

                    if (i < existingCount)
                        holdCardPackPosList[i] = t;
                    else
                        holdCardPackPosList.Add(t);
                }
            }

            if (EnableHeldItemPositionsValue && !PackOpeningState.IsPackPositionsReordered)
            {
                for (int j = 0; j < maxPositions; j++)
                {
                    int columnIndex = j / packsPerColumn % columnsPerLayer;
                    int rowIndex = j % packsPerColumn;
                    int layerIndex = j / packsPerColumn / columnsPerLayer;

                    float xNudge = 0.0267f;
                    float layerZOffset = packsPerColumn * zSpacing;

                    float x;
                    if (columnIndex % 2 == 0)
                        x = localPosition.x - xSpacing * (columnIndex / 2f) - xNudge;
                    else
                        x = localPosition.x + xSpacing * ((columnIndex + 1) / 2f) - xNudge;

                    float y = localPosition.y + ySpacing * rowIndex - 0.1f;
                    float z = localPosition.z + zSpacing * rowIndex + layerIndex * layerZOffset;

                    holdCardPackPosList[j].localScale = base0.localScale * scaledHeldPackScale;
                    holdCardPackPosList[j].localPosition = new Vector3(x, y, z);
                    holdCardPackPosList[j].rotation = rotation;
                }

                PackOpeningState.IsPackPositionsReordered = true;
            }
            else if ((!EnableHeldItemPositionsValue && PackOpeningState.IsPackPositionsReordered) ||
                     !EnableMaxHoldPacksValue ||
                     !FeatureEnabled)
            {
                for (int k = 0; k < maxPositions; k++)
                {
                    int row = k % maxPositions;

                    float x = localPosition.x + xSpacing * row;
                    float y = localPosition.y + ySpacing * row;
                    float z = localPosition.z + zSpacing * row;

                    holdCardPackPosList[k].localScale = base0.localScale;
                    holdCardPackPosList[k].localPosition = new Vector3(x, y, z);
                    holdCardPackPosList[k].rotation = rotation;
                }

                PackOpeningState.IsPackPositionsReordered = false;
            }

            PackOpeningState.IsPackPositionsLoaded = true;
            return holdCardPackPosList;
        }

        internal static void ReorderPackPositions()
        {
            if (CSingleton<InteractionPlayerController>.Instance.m_HoldCardPackPosList == null ||
                CSingleton<InteractionPlayerController>.Instance.m_HoldCardPackPosList.Count == 0)
            {
                return;
            }

            PackOpeningState.IsPackPositionsReordered = false;
            MovePackPositions();
        }
    }
}