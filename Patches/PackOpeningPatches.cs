#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;
using TMPro;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch]
    internal static class PackOpeningPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InteractionPlayerController), "EvaluateOpenCardPack")]
        private static bool InteractionPlayerController_EvaluateOpenCardPack_Prefix(ref InteractionPlayerController __instance)
        {
            if (!PackOpeningController.FeatureEnabled)
                return true;

            if (__instance.CanOpenPack())
            {
                Item item = __instance.m_HoldItemList[0];
                __instance.RemoveHoldItem(item);
                CSingleton<CardOpeningSequence>.Instance.ReadyingCardPack(item);
                __instance.m_IsHoldingMouseDown = false;
                __instance.m_IsHoldingRightMouseDown = false;
                return false;
            }

            if (__instance.CanOpenCardBox())
            {
                PackOpeningController.MovePackPositions();

                __instance.m_IsOpeningCardBox = true;
                Item boxItem = __instance.m_HoldItemList[0];
                EItemType packType = __instance.CardBoxToCardPack(boxItem.GetItemType());

                if (packType == EItemType.None)
                    return false;

                ItemMeshData boxMesh = InventoryBase.GetItemMeshData(boxItem.GetItemType());
                __instance.m_OpenCardBoxMeshFilter.mesh = boxMesh.mesh;
                __instance.m_OpenCardBoxMesh.material = boxMesh.material;

                boxItem.gameObject.SetActive(false);
                __instance.m_HoldItemList.Clear();
                CPlayerData.m_HoldItemTypeList.Clear();
                InteractionPlayerController.RemoveToolTip(EGameAction.OpenCardBox);

                SoundManager.PlayAudio("SFX_OpenCardBox", 0.6f, 1f);

                __instance.m_OpenCardBoxInnerMesh.gameObject.SetActive(true);
                __instance.m_OpenCardBoxInnerMesh.Rewind();
                __instance.m_OpenCardBoxInnerMesh.Play();
                __instance.m_OpenCardBoxInnerMesh["OpenCardBoxAnim"].speed = PackOpeningController.SpeedMultiplierValue;

                for (int i = 0; i < 8; i++)
                {
                    ItemMeshData packMesh = InventoryBase.GetItemMeshData(packType);
                    Item spawnedPack = ItemSpawnManager.GetItem(__instance.m_OpenCardBoxSpawnCardPackPosList[i]);

                    spawnedPack.SetMesh(
                        packMesh.mesh,
                        packMesh.material,
                        packType,
                        packMesh.meshSecondary,
                        packMesh.materialSecondary,
                        null);

                    spawnedPack.transform.position = __instance.m_OpenCardBoxSpawnCardPackPosList[i].position;
                    spawnedPack.transform.rotation = __instance.m_OpenCardBoxSpawnCardPackPosList[i].rotation;
                    spawnedPack.transform.parent = __instance.m_OpenCardBoxSpawnCardPackPosList[i];
                    spawnedPack.transform.localScale = __instance.m_OpenCardBoxSpawnCardPackPosList[i].localScale;
                    spawnedPack.gameObject.SetActive(true);

                    __instance.m_HoldItemList.Add(spawnedPack);
                    CPlayerData.m_HoldItemTypeList.Add(spawnedPack.GetItemType());

                    __instance.StartCoroutine(
                        __instance.DelayLerpSpawnedCardPackToHand(
                            i,
                            (1.25f + 0.05f * i) / PackOpeningController.SpeedMultiplierValue,
                            spawnedPack,
                            __instance.m_HoldCardPackPosList[i],
                            boxItem));
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InteractionPlayerController), "OnGameDataFinishLoaded")]
        private static bool InteractionPlayerController_OnGameDataFinishLoaded_Prefix(ref InteractionPlayerController __instance)
        {
            if (PackOpeningState.IsFirstLoad)
            {
                __instance.m_HoldCardPackPosList = PackOpeningController.MovePackPositions();
                PackOpeningState.IsFirstLoad = false;
            }
            else
            {
                PackOpeningController.ReorderPackPositions();
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InteractionPlayerController), "OnGameDataFinishLoaded")]
        private static void InteractionPlayerController_OnGameDataFinishLoaded_Postfix(ref InteractionPlayerController __instance)
        {
            AchievementManager.OnCheckAlbumCardCount(CPlayerData.GetTotalCardCollectedAmount());

            CSingleton<InteractionPlayerController>.Instance.m_CameraController.SetRotationAngles(0f, -35f);
            CSingleton<InteractionPlayerController>.Instance.m_CameraController.enabled = false;

            if (CPlayerData.m_HoldItemTypeList.Count > 7 &&
                !CPlayerData.m_HoldItemTypeList[0].ToString().Contains("Pack"))
            {
                for (int i = CPlayerData.m_HoldItemTypeList.Count - 1; i >= 8; i--)
                    CPlayerData.m_HoldItemTypeList.RemoveAt(i);
            }

            if (CPlayerData.m_HoldCardDataList.Count > 7)
            {
                for (int i = CPlayerData.m_HoldCardDataList.Count - 1; i >= 8; i--)
                    CPlayerData.m_HoldCardDataList.RemoveAt(i);
            }

            if (CPlayerData.m_HoldCardDataList.Count > 0)
            {
                List<InteractableCard3d> spawnedCards = new List<InteractableCard3d>();

                for (int i = 0; i < CPlayerData.m_HoldCardDataList.Count; i++)
                {
                    Card3dUIGroup cardUI = CSingleton<Card3dUISpawner>.Instance.GetCardUI();
                    InteractableCard3d card = ShelfManager.SpawnInteractableObject(EObjectType.Card3d).GetComponent<InteractableCard3d>();

                    cardUI.m_CardUI.SetCardUI(CPlayerData.m_HoldCardDataList[i]);
                    cardUI.transform.position = CSingleton<InteractionPlayerController>.Instance.m_HoldCardPosList[i].position;
                    cardUI.transform.rotation = CSingleton<InteractionPlayerController>.Instance.m_HoldCardPosList[i].rotation;

                    card.transform.position = CSingleton<InteractionPlayerController>.Instance.m_HoldCardPosList[i].position;
                    card.transform.rotation = CSingleton<InteractionPlayerController>.Instance.m_HoldCardPosList[i].rotation;
                    card.SetCardUIFollow(cardUI);
                    card.SetEnableCollision(false);

                    spawnedCards.Add(card);
                }

                CPlayerData.m_HoldCardDataList.Clear();

                for (int i = 0; i < spawnedCards.Count; i++)
                    InteractionPlayerController.AddHoldCard(spawnedCards[i]);

                __instance.EnterHoldCardMode();
                return;
            }

            if (CPlayerData.m_HoldItemTypeList.Count > 0)
            {
                List<Item> spawnedItems = new List<Item>();

                for (int i = 0; i < CPlayerData.m_HoldItemTypeList.Count; i++)
                {
                    ItemMeshData meshData = InventoryBase.GetItemMeshData(CPlayerData.m_HoldItemTypeList[i]);
                    Item item = ItemSpawnManager.GetItem(__instance.m_HoldCardPackPosList[i]);

                    item.SetMesh(
                        meshData.mesh,
                        meshData.material,
                        CPlayerData.m_HoldItemTypeList[i],
                        meshData.meshSecondary,
                        meshData.materialSecondary,
                        null);

                    item.transform.position = __instance.m_HoldCardPackPosList[i].position;
                    item.transform.rotation = __instance.m_HoldCardPackPosList[i].rotation;
                    item.SmoothLerpToTransform(__instance.m_HoldCardPackPosList[i], __instance.m_HoldCardPackPosList[i], false);
                    item.gameObject.SetActive(true);

                    spawnedItems.Add(item);
                }

                for (int i = 0; i < spawnedItems.Count; i++)
                    __instance.m_HoldItemList.Add(spawnedItems[i]);

                __instance.SetCurrentGameState(EGameState.HoldingItemState);
                __instance.m_IsHoldItemMode = true;
                PackOpeningController.ReorderPackPositions();
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(InteractionPlayerController), "EvaluateTakeItemFromShelf")]
        private static IEnumerable<CodeInstruction> InteractionPlayerController_EvaluateTakeItemFromShelf_Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);

            MethodInfo? featureEnabledGetter = AccessTools.PropertyGetter(typeof(PackOpeningController), nameof(PackOpeningController.FeatureEnabled));
            MethodInfo? enableMaxHoldGetter = AccessTools.PropertyGetter(typeof(PackOpeningController), nameof(PackOpeningController.EnableMaxHoldPacksValue));
            MethodInfo? maxHoldGetter = AccessTools.PropertyGetter(typeof(PackOpeningController), nameof(PackOpeningController.MaxHoldPacksValue));

            Label label1 = generator.DefineLabel();
            Label label2 = generator.DefineLabel();
            Label label3 = generator.DefineLabel();
            Label label4 = generator.DefineLabel();

            List<CodeInstruction> injectA = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Call, featureEnabledGetter),
                new CodeInstruction(OpCodes.Call, enableMaxHoldGetter),
                new CodeInstruction(OpCodes.And),
                new CodeInstruction(OpCodes.Brfalse_S, label1),
                new CodeInstruction(OpCodes.Call, maxHoldGetter),
                new CodeInstruction(OpCodes.Br_S, label2)
            };

            List<CodeInstruction> injectB = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Call, featureEnabledGetter),
                new CodeInstruction(OpCodes.Call, enableMaxHoldGetter),
                new CodeInstruction(OpCodes.And),
                new CodeInstruction(OpCodes.Brfalse_S, label3),
                new CodeInstruction(OpCodes.Call, maxHoldGetter),
                new CodeInstruction(OpCodes.Br_S, label4)
            };

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode != OpCodes.Ldc_I4_8)
                    continue;

                if (i + 1 < list.Count && list[i + 1].opcode == OpCodes.Stloc_1)
                {
                    list[i].labels.Add(label1);
                    list[i + 1].labels.Add(label2);
                    list.InsertRange(i, injectA);
                    i += injectA.Count + 1;
                }

                if (i + 1 < list.Count && list[i + 1].opcode == OpCodes.Stloc_S)
                {
                    list[i].labels.Add(label3);
                    list[i + 1].labels.Add(label4);
                    list.InsertRange(i, injectB);
                    i += injectB.Count + 1;
                }
            }

            return list.AsEnumerable();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardOpeningSequenceUI), "Update")]
        private static bool CardOpeningSequenceUI_Update_Prefix(ref CardOpeningSequenceUI __instance)
        {
            if (!PackOpeningController.FeatureEnabled)
                return true;

            if (__instance.m_IsShowingTotalValue)
            {
                __instance.m_TotalValueLerpTimer += Time.deltaTime * 0.5f * PackOpeningController.SpeedMultiplierValue;
                __instance.m_CurrentTotalCardValueLerp = Mathf.Lerp(
                    0f,
                    __instance.m_TargetTotalCardValueLerp,
                    __instance.m_TotalValueLerpTimer);

                __instance.m_TotalCardValueText.text =
                    GameInstance.GetPriceString(__instance.m_CurrentTotalCardValueLerp, false, true, false, "F2");

                if (__instance.m_TotalValueLerpTimer >= 1f)
                {
                    __instance.m_IsShowingTotalValue = false;
                    SoundManager.SetEnableSound_ExpIncrease(false);
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardOpeningSequenceUI), "ShowSingleCardValue")]
        private static bool CardOpeningSequenceUI_ShowSingleCardValue_Prefix(ref CardOpeningSequenceUI __instance, ref float cardValue)
        {
            if (!PackOpeningController.FeatureEnabled)
                return true;

            if (!__instance.m_ScreenGrp.activeSelf)
                __instance.m_ScreenGrp.SetActive(true);

            __instance.m_CardValueText.text = GameInstance.GetPriceString(cardValue, false, true, false, "F2");
            __instance.m_CardValueTextGrp.SetActive(true);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardOpeningSequence), "ReadyingCardPack")]
        private static bool CardOpeningSequence_ReadyingCardPack_Prefix(ref CardOpeningSequence __instance, Item item)
        {
            if (!PackOpeningController.FeatureEnabled || !PackOpeningController.DisableSoundsValue)
                return true;

            if (!__instance.m_IsReadyingToOpen)
            {
                __instance.m_IsScreenActive = true;
                CSingleton<InteractionPlayerController>.Instance.EnterLockMoveMode();
                CSingleton<InteractionPlayerController>.Instance.OnEnterOpenPackState();
                __instance.m_IsReadyingToOpen = true;
                __instance.m_IsReadyToOpen = false;
                __instance.m_LerpPosTimer = 0f;
                __instance.m_CurrentItem = item;

                __instance.m_CardPackAnimator.transform.position = __instance.m_StartLerpTransform.position;
                __instance.m_CardPackAnimator.transform.rotation = __instance.m_StartLerpTransform.rotation;
                __instance.m_CardPackAnimator.transform.localScale = __instance.m_StartLerpTransform.localScale;

                __instance.m_CardPackMesh.material = __instance.m_CurrentItem.m_Mesh.sharedMaterial;
                __instance.m_CardPackAnimator.gameObject.SetActive(true);
                __instance.m_CurrentItem.gameObject.SetActive(false);
                __instance.m_CardOpeningUIGroup.SetActive(false);
                __instance.m_CardPackAnimator.Play("PackOpenAnim", -1, 0f);

                CSingleton<InteractionPlayerController>.Instance.m_BlackBGWorldUIFade.SetFadeIn(3f, 0f);
                TutorialManager.SetGameUIVisible(false);
                CenterDot.SetVisibility(false);
                GameUIScreen.HideEnterGoNextDayIndicatorVisible();
                InteractionPlayerController.TempHideToolTip();
                InteractionPlayerController.AddToolTip(EGameAction.OpenPack, true, false);
                InteractionPlayerController.AddToolTip(EGameAction.CancelOpenPack, false, false);
                InteractionPlayerController.SetAllHoldItemVisibility(false);
                CSingleton<InteractionPlayerController>.Instance.m_CameraFOVController.StartLerpToFOV(40f);
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CardOpeningSequence), "ReadyingCardPack")]
        private static void CardOpeningSequence_ReadyingCardPack_Postfix(ref CardOpeningSequence __instance)
        {
            if (!PackOpeningController.FeatureEnabled)
                return;

            __instance.m_MultiplierStateTimer =
                (1f + 2.5f * CSingleton<CGameManager>.Instance.m_OpenPackSpeedSlider) *
                PackOpeningController.SpeedMultiplierValue;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SoundManager), "SetEnableSound_ExpIncrease")]
        private static void SoundManager_SetEnableSound_ExpIncrease_Postfix()
        {
            if (!PackOpeningController.FeatureEnabled || !PackOpeningController.DisableSoundsValue)
                return;

            CSingleton<SoundManager>.Instance.m_ExpIncrease.volume = 0f;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CardOpeningSequenceUI), "StartShowTotalValue")]
        private static void CardOpeningSequenceUI_StartShowTotalValue_Postfix(ref CardOpeningSequenceUI __instance)
        {
            if (!PackOpeningController.FeatureEnabled || SMConfig.PackResultsTimerSeconds.Value >= 1f)
                return;

            __instance.m_FoilRainbowGlowingBG.SetActive(false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardOpeningSequence), "Update")]
        private static bool CardOpeningSequence_Update_Prefix(ref CardOpeningSequence __instance)
        {
            if (!PackOpeningController.FeatureEnabled)
            {
                __instance.m_HighValueCardThreshold = 10f;
                __instance.m_CardOpeningRotateToFrontAnim["CardOpenSeq1_RotateToFront"].speed = 1f;
                __instance.m_CardOpeningRotateToFrontAnim["CardOpenSeq0_Idle"].speed = 1f;

                foreach (Animation animation in __instance.m_CardAnimList)
                {
                    animation["OpenCardNewCard"].speed = 1f;
                    animation["OpenCardFinalReveal"].speed = 1f;
                }

                return true;
            }

            if (InputManager.GetKeyDownAction(EGameAction.CancelOpenPack) &&
                __instance.m_StateIndex >= 2 &&
                __instance.m_StateIndex < 10)
            {
                PackOpeningState.IsCancelPackQueued = true;
            }

            __instance.m_HighValueCardThreshold = SMConfig.HighValueThreshold.Value;
            __instance.m_CardOpeningRotateToFrontAnim["CardOpenSeq1_RotateToFront"].speed = PackOpeningController.SpeedMultiplierValue;
            __instance.m_CardOpeningRotateToFrontAnim["CardOpenSeq0_Idle"].speed = PackOpeningController.SpeedMultiplierValue;

            foreach (Animation animation in __instance.m_CardAnimList)
            {
                animation["OpenCardNewCard"].speed = PackOpeningController.SpeedMultiplierValue;
                animation["OpenCardFinalReveal"].speed = PackOpeningController.SpeedMultiplierValue;
            }

            if (PackOpeningState.PackSpeedMultiplier != PackOpeningController.SpeedMultiplierValue - __instance.m_MultiplierStateTimer)
            {
                PackOpeningState.PackSpeedMultiplier =
                    Mathf.Clamp(PackOpeningController.SpeedMultiplierValue - __instance.m_MultiplierStateTimer, 0f, PackOpeningController.SpeedMultiplierValue);
            }

            __instance.m_IsAutoFire = PackOpeningState.IsAutoOpen;

            if (!__instance.m_IsScreenActive)
                return false;

            if (InputManager.GetKeyDownAction(EGameAction.OpenPack))
                __instance.m_IsAutoFireKeydown = true;

            if (InputManager.GetKeyUpAction(EGameAction.OpenPack))
                __instance.m_IsAutoFireKeydown = false;

            if (__instance.m_IsAutoFireKeydown)
            {
                __instance.m_AutoFireTimer += Time.deltaTime;
                if (__instance.m_AutoFireTimer >= 0.05f)
                {
                    __instance.m_AutoFireTimer = 0f;
                    __instance.m_IsAutoFire = true;
                }
            }
            else if (__instance.m_AutoFireTimer > 0f)
            {
                __instance.m_AutoFireTimer = 0f;
                __instance.m_IsAutoFire = true;
            }

            if (__instance.m_IsReadyingToOpen)
            {
                if (!__instance.m_IsReadyToOpen)
                {
                    if (__instance.m_IsCanceling)
                    {
                        __instance.m_LerpPosTimer -= Time.deltaTime * __instance.m_LerpPosSpeed;

                        if (__instance.m_LerpPosTimer < 0f)
                        {
                            __instance.m_LerpPosTimer = 0f;
                            __instance.m_IsReadyToOpen = false;
                            __instance.m_IsReadyingToOpen = false;
                            __instance.m_IsCanceling = false;
                            __instance.m_IsScreenActive = false;
                            __instance.m_CardPackAnimator.gameObject.SetActive(false);

                            CSingleton<InteractionPlayerController>.Instance.ExitLockMoveMode();
                            CSingleton<InteractionPlayerController>.Instance.OnExitOpenPackState();
                            InteractionPlayerController.RestoreHiddenToolTip();

                            __instance.m_CurrentItem.gameObject.SetActive(true);
                            InteractionPlayerController.SetAllHoldItemVisibility(true);
                            __instance.m_CurrentItem = null;

                            TutorialManager.SetGameUIVisible(true);
                            CenterDot.SetVisibility(true);
                            GameUIScreen.ResetEnterGoNextDayIndicatorVisible();
                        }
                    }
                    else
                    {
                        __instance.m_LerpPosTimer += Time.deltaTime * __instance.m_LerpPosSpeed * PackOpeningController.SpeedMultiplierValue;
                        if (__instance.m_LerpPosTimer > 1f)
                        {
                            __instance.m_LerpPosTimer = 1f;
                            __instance.m_IsReadyToOpen = true;
                        }
                    }

                    __instance.m_CardPackAnimator.transform.localPosition =
                        Vector3.Lerp(__instance.m_StartLerpTransform.localPosition, Vector3.zero, __instance.m_LerpPosTimer);
                    __instance.m_CardPackAnimator.transform.localRotation =
                        Quaternion.Lerp(__instance.m_StartLerpTransform.localRotation, Quaternion.identity, __instance.m_LerpPosTimer);
                    __instance.m_CardPackAnimator.transform.localScale =
                        Vector3.Lerp(__instance.m_StartLerpTransform.localScale, Vector3.one, __instance.m_LerpPosTimer);

                    return false;
                }

                if (__instance.m_IsAutoFire && !PackOpeningState.IsCancelPackQueued)
                {
                    __instance.m_IsReadyingToOpen = false;
                    __instance.OpenScreen(InventoryBase.ItemTypeToCollectionPackType(__instance.m_CurrentItem.GetItemType()), false, false);
                    return false;
                }

                if ((InputManager.GetKeyDownAction(EGameAction.CancelOpenPack) && !__instance.m_IsCanceling) ||
                    PackOpeningState.IsCancelPackQueued)
                {
                    CSingleton<InteractionPlayerController>.Instance.AddHoldItemToFront(__instance.m_CurrentItem);
                    __instance.m_IsCanceling = true;
                    __instance.m_IsReadyToOpen = false;

                    CSingleton<InteractionPlayerController>.Instance.m_BlackBGWorldUIFade.SetFadeOut(3f, 0f);
                    InteractionPlayerController.RestoreHiddenToolTip();
                    CSingleton<InteractionPlayerController>.Instance.m_CameraFOVController.StopLerpFOV();
                    SoundManager.GenericPop(1f, 0.9f);

                    PackOpeningState.IsCancelPackQueued = false;
                }

                return false;
            }

            if (!__instance.m_IsScreenActive)
                return false;

            if (__instance.m_StateIndex == 0)
            {
                __instance.InitOpenSequence();
                __instance.m_StateIndex++;
                return false;
            }

            if (__instance.m_StateIndex == 1)
            {
                __instance.m_StateTimer += Time.deltaTime * (__instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier);

                if (__instance.m_StateTimer > 0.05f)
                {
                    __instance.m_StateTimer = 0f;
                    if (__instance.m_TempIndex < __instance.m_Card3dUIList.Count)
                    {
                        __instance.m_Card3dUIList[__instance.m_TempIndex].gameObject.SetActive(true);
                        __instance.m_TempIndex++;
                    }
                }

                if (__instance.m_IsAutoFire || __instance.m_IsAutoFireKeydown || CSingleton<CGameManager>.Instance.m_OpenPacAutoNextCard)
                {
                    __instance.m_Slider += 0.0065f * (__instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier);
                    __instance.m_CardPackAnimator.Play("PackOpenAnim", -1, __instance.m_Slider);

                    if (__instance.m_Slider >= 0.3f)
                    {
                        __instance.m_OpenPackVFX.Play();

                        if (!PackOpeningController.DisableSoundsValue)
                        {
                            SoundManager.PlayAudio("SFX_OpenPack", 0.6f, 1f);
                            SoundManager.PlayAudio("SFX_BoxOpen", 0.5f, 1f);
                        }

                        __instance.m_StateIndex++;
                        return false;
                    }
                }
            }
            else if (__instance.m_StateIndex == 2)
            {
                __instance.m_Slider += Time.deltaTime * (__instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier);
                __instance.m_CardPackAnimator.Play("PackOpenAnim", -1, __instance.m_Slider);
                __instance.m_StateTimer += Time.deltaTime * PackOpeningController.SpeedMultiplierValue;

                if (__instance.m_StateTimer > 0.05f)
                {
                    __instance.m_StateTimer = 0f;
                    if (__instance.m_TempIndex < __instance.m_Card3dUIList.Count)
                    {
                        __instance.m_Card3dUIList[__instance.m_TempIndex].gameObject.SetActive(true);
                        __instance.m_TempIndex++;
                    }
                }

                if (__instance.m_Slider >= 1f)
                {
                    InteractionPlayerController.RemoveToolTip(EGameAction.OpenPack);
                    __instance.m_TempIndex = 0;
                    __instance.m_StateTimer = 0f;
                    __instance.m_Slider = 0f;
                    __instance.m_StateIndex++;

                    for (int i = 0; i < __instance.m_Card3dUIList.Count; i++)
                        __instance.m_Card3dUIList[i].gameObject.SetActive(true);

                    return false;
                }
            }
            else if (__instance.m_StateIndex == 3)
            {
                __instance.m_Slider += Time.deltaTime * __instance.m_MultiplierStateTimer;

                if (__instance.m_Slider >= 0.15f)
                {
                    __instance.m_Slider = 0f;
                    __instance.m_CardOpeningRotateToFrontAnim.Play("CardOpenSeq1_RotateToFront");
                    __instance.m_StateIndex++;
                }

                if (__instance.m_IsAutoFire || CSingleton<CGameManager>.Instance.m_OpenPacAutoNextCard)
                {
                    float pitchDelta = 0.002f * __instance.m_CurrentOpenedCardIndex;
                    float volumeDelta = 0.001f * __instance.m_CurrentOpenedCardIndex;

                    if (!PackOpeningController.DisableSoundsValue)
                        SoundManager.PlayAudio("SFX_CardReveal1", 0.6f + volumeDelta, 1f + pitchDelta);

                    __instance.m_CardOpeningRotateToFrontAnim.Play("CardOpenSeq1_RotateToFront");
                    __instance.m_StateTimer = 0f;
                    __instance.m_StateIndex++;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 4)
            {
                __instance.m_Slider += Time.deltaTime * (__instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier);

                if (!__instance.m_CardOpeningSequenceUI.m_CardValueTextGrp.activeSelf &&
                    __instance.m_CurrentOpenedCardIndex < 6 &&
                    __instance.m_Slider >= 0.45f &&
                    !__instance.m_IsNewlList[__instance.m_CurrentOpenedCardIndex] &&
                    __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex] < __instance.m_HighValueCardThreshold)
                {
                    __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex];
                    __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex]);
                }

                if (__instance.m_Slider >= 0.8f)
                {
                    __instance.m_Slider = 0f;

                    if (__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex] >= __instance.m_HighValueCardThreshold)
                    {
                        if (SMConfig.StopAutoOpenOnHighValue.Value && PackOpeningState.IsAutoOpen)
                        {
                            PackOpeningState.IsAutoOpen = false;
                            PackOpeningState.DelayAutoOpen = true;
                            __instance.m_IsAutoFireKeydown = false;
                            PackOpeningState.IsGetNewOrHighValueCard = true;
                        }

                        if ((PackOpeningController.DisableSoundsValue && SMConfig.StopAutoOpenOnHighValue.Value) || !PackOpeningController.DisableSoundsValue)
                            SoundManager.PlayAudio("SFX_FinalizeCard", 0.6f, 1.2f);

                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].Play("OpenCardNewCard");
                        __instance.m_HighValueCardIcon.SetActive(true);
                        __instance.StartCoroutine(__instance.DelayToState(5, 0.9f / PackOpeningController.SpeedMultiplierValue));
                        __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex];
                        __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex]);
                        __instance.m_IsGetHighValueCard = true;
                        return false;
                    }

                    if (__instance.m_IsNewlList[__instance.m_CurrentOpenedCardIndex])
                    {
                        if (SMConfig.StopAutoOpenOnNewCard.Value && PackOpeningState.IsAutoOpen)
                        {
                            PackOpeningState.IsAutoOpen = false;
                            PackOpeningState.DelayAutoOpen = true;
                            __instance.m_IsAutoFireKeydown = false;
                            PackOpeningState.IsGetNewOrHighValueCard = true;
                        }

                        if ((PackOpeningController.DisableSoundsValue && SMConfig.StopAutoOpenOnNewCard.Value) || !PackOpeningController.DisableSoundsValue)
                            SoundManager.PlayAudio("SFX_CardReveal0", 0.6f, 1f);

                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].Play("OpenCardNewCard");
                        __instance.m_NewCardIcon.SetActive(true);
                        __instance.StartCoroutine(__instance.DelayToState(5, 0.9f / PackOpeningController.SpeedMultiplierValue));
                        __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex];
                        __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex]);
                        return false;
                    }

                    __instance.m_StateIndex++;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 5)
            {
                if (__instance.m_IsAutoFire || (!PackOpeningState.IsGetNewOrHighValueCard && CSingleton<CGameManager>.Instance.m_OpenPacAutoNextCard))
                {
                    int which = Random.Range(0, 3);
                    float pitchDelta = 0.002f * __instance.m_CurrentOpenedCardIndex;
                    float volumeDelta = 0.001f * __instance.m_CurrentOpenedCardIndex;

                    if (!PackOpeningController.DisableSoundsValue)
                    {
                        if (which == 0) SoundManager.PlayAudio("SFX_CardReveal1", 0.6f + volumeDelta, 1f + pitchDelta);
                        else if (which == 1) SoundManager.PlayAudio("SFX_CardReveal2", 0.6f + volumeDelta, 1f + pitchDelta);
                        else SoundManager.PlayAudio("SFX_CardReveal3", 0.6f + volumeDelta, 1f + pitchDelta);
                    }

                    if (__instance.m_CurrentOpenedCardIndex >= 7)
                    {
                        __instance.m_StateIndex = 7;
                    }
                    else
                    {
                        __instance.m_StateIndex++;
                        __instance.m_NewCardIcon.SetActive(false);
                        __instance.m_HighValueCardIcon.SetActive(false);
                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].Play("OpenCardSlideExit");
                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex]["OpenCardSlideExit"].speed =
                            __instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier;
                        __instance.m_CardOpeningSequenceUI.HideSingleCardValue();
                    }

                    __instance.m_IsGetHighValueCard = false;

                    if (PackOpeningState.DelayAutoOpen && !PackOpeningState.IsAutoOpen)
                    {
                        PackOpeningState.IsAutoOpen = true;
                        PackOpeningState.DelayAutoOpen = false;
                    }

                    PackOpeningState.IsGetNewOrHighValueCard = false;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 6)
            {
                __instance.m_Slider += Time.deltaTime * (__instance.m_MultiplierStateTimer + PackOpeningState.PackSpeedMultiplier);

                if (!__instance.m_CardOpeningSequenceUI.m_CardValueTextGrp.activeSelf &&
                    __instance.m_CurrentOpenedCardIndex < 6 &&
                    __instance.m_Slider >= 0.3f &&
                    !__instance.m_IsNewlList[__instance.m_CurrentOpenedCardIndex + 1] &&
                    __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex + 1] < __instance.m_HighValueCardThreshold)
                {
                    __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex + 1];
                    __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex + 1]);
                }

                if (__instance.m_Slider >= 0.5f)
                {
                    __instance.m_Slider = 0f;

                    if (__instance.m_Card3dUIList.Count > __instance.m_CurrentOpenedCardIndex)
                    {
                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].transform.localPosition = Vector3.zero;
                        __instance.m_Card3dUIList[__instance.m_CurrentOpenedCardIndex].gameObject.SetActive(false);
                    }

                    __instance.m_CurrentOpenedCardIndex++;

                    if (__instance.m_CurrentOpenedCardIndex >= 7)
                    {
                        __instance.m_IsGetHighValueCard = false;
                        __instance.m_StateIndex = 7;
                        return false;
                    }

                    if (__instance.m_Card3dUIList.Count > __instance.m_CurrentOpenedCardIndex + 1)
                        __instance.m_Card3dUIList[__instance.m_CurrentOpenedCardIndex + 1].gameObject.SetActive(true);

                    if (__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex] >= __instance.m_HighValueCardThreshold)
                    {
                        if (SMConfig.StopAutoOpenOnHighValue.Value && PackOpeningState.IsAutoOpen)
                        {
                            PackOpeningState.IsAutoOpen = false;
                            PackOpeningState.DelayAutoOpen = true;
                            __instance.m_IsAutoFireKeydown = false;
                            PackOpeningState.IsGetNewOrHighValueCard = true;
                        }

                        if ((PackOpeningController.DisableSoundsValue && SMConfig.StopAutoOpenOnHighValue.Value) || !PackOpeningController.DisableSoundsValue)
                            SoundManager.PlayAudio("SFX_FinalizeCard", 0.6f, 1.2f);

                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].Play("OpenCardNewCard");
                        __instance.m_HighValueCardIcon.SetActive(true);
                        __instance.StartCoroutine(__instance.DelayToState(5, 0.9f / PackOpeningController.SpeedMultiplierValue));
                        __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex];
                        __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex]);
                        __instance.m_IsGetHighValueCard = true;
                        return false;
                    }

                    if (__instance.m_IsNewlList[__instance.m_CurrentOpenedCardIndex])
                    {
                        if (SMConfig.StopAutoOpenOnNewCard.Value && PackOpeningState.IsAutoOpen)
                        {
                            PackOpeningState.IsAutoOpen = false;
                            PackOpeningState.DelayAutoOpen = true;
                            __instance.m_IsAutoFireKeydown = false;
                            PackOpeningState.IsGetNewOrHighValueCard = true;
                        }

                        if ((PackOpeningController.DisableSoundsValue && SMConfig.StopAutoOpenOnNewCard.Value) || !PackOpeningController.DisableSoundsValue)
                            SoundManager.PlayAudio("SFX_CardReveal0", 0.6f, 1f);

                        __instance.m_CardAnimList[__instance.m_CurrentOpenedCardIndex].Play("OpenCardNewCard");
                        __instance.m_NewCardIcon.SetActive(true);
                        __instance.StartCoroutine(__instance.DelayToState(5, 0.9f / PackOpeningController.SpeedMultiplierValue));
                        __instance.m_TotalCardValue += __instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex];
                        __instance.m_CardOpeningSequenceUI.ShowSingleCardValue(__instance.m_CardValueList[__instance.m_CurrentOpenedCardIndex]);
                        return false;
                    }

                    __instance.m_StateIndex = 5;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 7)
            {
                if (PackOpeningState.DelayAutoOpen && !PackOpeningState.IsAutoOpen)
                    PackOpeningState.IsAutoOpen = true;

                PackOpeningState.DelayAutoOpen = false;

                if (__instance.m_StateTimer == 0f && __instance.m_Slider == 0f && !PackOpeningController.DisableSoundsValue)
                {
                    SoundManager.PlayAudio("SFX_PercStarJingle3", 0.6f, 1f);
                    SoundManager.PlayAudio("SFX_Gift", 0.6f, 1f);
                }

                __instance.m_Slider += Time.deltaTime * (PackOpeningController.SkipPackEndScreenValue ? PackOpeningController.SpeedMultiplierValue : 1f);

                if (PackOpeningController.SkipPackEndScreenValue)
                {
                    for (int i = 0; i < __instance.m_CardAnimList.Count; i++)
                    {
                        __instance.m_CardAnimList[i].transform.position = __instance.m_ShowAllCardPosList[i].position;
                        __instance.m_CardAnimList[i].transform.rotation = __instance.m_ShowAllCardPosList[i].rotation;
                        __instance.m_Card3dUIList[i].gameObject.SetActive(true);
                    }

                    __instance.m_StateTimer = 0f;
                    __instance.m_StateIndex++;
                    __instance.m_CardOpeningSequenceUI.StartShowTotalValue(__instance.m_TotalCardValue, __instance.m_HasFoilCard);
                    return false;
                }

                if (__instance.m_Slider >= 0.05f)
                {
                    __instance.m_Slider = 0f;
                    __instance.m_CardAnimList[(int)__instance.m_StateTimer].transform.position = __instance.m_ShowAllCardPosList[(int)__instance.m_StateTimer].position;
                    __instance.m_CardAnimList[(int)__instance.m_StateTimer].transform.rotation = __instance.m_ShowAllCardPosList[(int)__instance.m_StateTimer].rotation;
                    __instance.m_Card3dUIList[(int)__instance.m_StateTimer].gameObject.SetActive(true);
                    __instance.m_CardAnimList[(int)__instance.m_StateTimer].Play("OpenCardFinalReveal");
                    __instance.m_StateTimer += 1f;

                    if (__instance.m_StateTimer >= __instance.m_Card3dUIList.Count)
                    {
                        __instance.m_StateTimer = 0f;
                        __instance.m_StateIndex++;
                        __instance.m_CardOpeningSequenceUI.StartShowTotalValue(__instance.m_TotalCardValue, __instance.m_HasFoilCard);
                        return false;
                    }
                }
            }
            else if (__instance.m_StateIndex == 8)
            {
                __instance.m_StateTimer += Time.deltaTime * (PackOpeningController.SkipPackEndScreenValue ? PackOpeningController.SpeedMultiplierValue : 1f);

                if (__instance.m_StateTimer >= 0.02f)
                {
                    __instance.m_Slider = 0f;

                    if (PackOpeningController.SkipPackEndScreenValue)
                    {
                        for (int i = 0; i < __instance.m_Card3dUIList.Count; i++)
                            __instance.m_Card3dUIList[i].m_NewCardIndicator.gameObject.SetActive(__instance.m_ManualOpenCardDataList[i].isNew);
                    }
                    else
                    {
                        __instance.m_Card3dUIList[(int)__instance.m_StateTimer].m_NewCardIndicator.gameObject.SetActive(
                            __instance.m_ManualOpenCardDataList[(int)__instance.m_StateTimer].isNew);
                    }

                    __instance.m_StateTimer += 1f;

                    if (__instance.m_StateTimer >= __instance.m_Card3dUIList.Count)
                    {
                        __instance.m_StateIndex++;
                        return false;
                    }
                }
            }
            else if (__instance.m_StateIndex == 9)
            {
                __instance.m_Slider += Time.deltaTime * (PackOpeningController.SkipPackEndScreenValue ? PackOpeningController.SpeedMultiplierValue : 1f);

                float gate = PackOpeningController.SkipPackEndScreenValue
                    ? PackOpeningController.PackResultsTimerValue * PackOpeningController.SpeedMultiplierValue
                    : PackOpeningController.PackResultsTimerValue;

                if (__instance.m_Slider >= gate)
                {
                    __instance.m_Slider = 0f;
                    __instance.m_StateIndex++;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 10)
            {
                if (__instance.m_IsAutoFire)
                {
                    __instance.m_StateIndex++;
                    return false;
                }
            }
            else if (__instance.m_StateIndex == 11)
            {
                __instance.m_StateTimer += Time.deltaTime;

                if (__instance.m_StateTimer >= 0.01f)
                {
                    __instance.m_Slider = 0f;
                    __instance.m_IsScreenActive = false;
                    __instance.m_IsReadyToOpen = false;
                    __instance.m_CardPackAnimator.gameObject.SetActive(false);
                    __instance.m_CardOpeningUIGroup.SetActive(false);
                    __instance.m_CardOpeningSequenceUI.HideTotalValue();

                    CSingleton<InteractionPlayerController>.Instance.ExitLockMoveMode();
                    CSingleton<InteractionPlayerController>.Instance.OnExitOpenPackState();

                    if (__instance.m_CurrentItem)
                        __instance.m_CurrentItem.DisableItem();

                    __instance.m_CurrentItem = null;

                    int newCardCount = 0;
                    __instance.m_TotalCardValue = 0f;
                    __instance.m_TotalExpGained = 0;

                    bool gotFullArtFoil = false;
                    bool gotGhostFullArtFoil = false;

                    for (int i = 0; i < __instance.m_ManualOpenCardDataList.Count; i++)
                    {
                        int borderTypeValue = (int)__instance.m_ManualOpenCardDataList[i].borderType;
                        int getCardBorderTypeValue = (int)__instance.m_ManualOpenCardDataList[i].GetCardBorderType();

                        int exp = (getCardBorderTypeValue + 1) * Mathf.CeilToInt((borderTypeValue + 1) / 2f);
                        
                        if (__instance.m_ManualOpenCardDataList[i].isFoil)
                            exp *= 8;

                        __instance.m_TotalExpGained += exp;

                        if (__instance.m_ManualOpenCardDataList[i].GetCardBorderType() == ECardBorderType.FullArt &&
                            __instance.m_ManualOpenCardDataList[i].isFoil)
                        {
                            gotFullArtFoil = true;
                            if (__instance.m_ManualOpenCardDataList[i].expansionType == ECardExpansionType.Ghost)
                                gotGhostFullArtFoil = true;
                        }

                        if (__instance.m_ManualOpenCardDataList[i].isNew)
                            newCardCount++;
                    }

                    if (__instance.m_TotalExpGained > 0)
                        CEventManager.QueueEvent(new CEventPlayer_AddShopExp(__instance.m_TotalExpGained, false));

                    for (int i = 0; i < __instance.m_CardAnimList.Count; i++)
                    {
                        __instance.m_CardAnimList[i].transform.localPosition = Vector3.zero;
                        __instance.m_CardAnimList[i].transform.localRotation = Quaternion.identity;
                        __instance.m_Card3dUIList[i].m_NewCardIndicator.gameObject.SetActive(false);
                        __instance.m_CardAnimList[i].Play("OpenCardDefaultPos");
                    }

                    if (CSingleton<InteractionPlayerController>.Instance.GetHoldItemCount() <= 0)
                    {
                        TutorialManager.SetGameUIVisible(true);
                        CenterDot.SetVisibility(true);
                        GameUIScreen.ResetEnterGoNextDayIndicatorVisible();
                        CSingleton<InteractionPlayerController>.Instance.m_BlackBGWorldUIFade.SetFadeOut(3f, 0f);
                        CSingleton<InteractionPlayerController>.Instance.m_CameraFOVController.StopLerpFOV();
                        __instance.m_IsAutoFireKeydown = false;
                        __instance.m_AutoFireTimer = 0f;
                    }

                    CSingleton<InteractionPlayerController>.Instance.EvaluateOpenCardPack();
                    TutorialManager.AddTaskValue(ETutorialTaskCondition.OpenPack, 1f);

                    CPlayerData.m_GameReportDataCollect.cardPackOpened += 1;
                    CPlayerData.m_GameReportDataCollectPermanent.cardPackOpened += 1;

                    AchievementManager.OnCardPackOpened(CPlayerData.m_GameReportDataCollectPermanent.cardPackOpened);
                    AchievementManager.OnGetFullArtFoil(gotFullArtFoil);
                    AchievementManager.OnGetFullArtGhostFoil(gotGhostFullArtFoil);

                    if (newCardCount > 0)
                        AchievementManager.OnCheckAlbumCardCount(CPlayerData.GetTotalCardCollectedAmount());

                    UnityAnalytic.OpenPack();
                    return false;
                }
            }
            else
            {
                if (__instance.m_StateIndex == 12)
                {
                    __instance.m_IsScreenActive = false;
                    return false;
                }

                if (__instance.m_StateIndex == 101)
                {
                    __instance.m_StateTimer += Time.deltaTime;

                    if (__instance.m_StateTimer >= 0.05f)
                    {
                        int which = Random.Range(0, 3);
                        float pitchDelta = 0.002f * __instance.m_CurrentOpenedCardIndex;
                        float volumeDelta = 0.001f * __instance.m_CurrentOpenedCardIndex;

                        if (!PackOpeningController.DisableSoundsValue)
                        {
                            if (which == 0) SoundManager.PlayAudio("SFX_CardReveal1", 0.6f + volumeDelta, 1f + pitchDelta);
                            else if (which == 1) SoundManager.PlayAudio("SFX_CardReveal2", 0.6f + volumeDelta, 1f + pitchDelta);
                            else SoundManager.PlayAudio("SFX_CardReveal3", 0.6f + volumeDelta, 1f + pitchDelta);
                        }

                        __instance.m_CurrentOpenedCardIndex++;
                        return false;
                    }
                }
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameUIScreen), "Update")]
        private static void GameUIScreen_Update_Postfix(ref GameUIScreen __instance)
        {
            if (!PackOpeningController.FeatureEnabled)
                return;

            if (PackOpeningState.HoldItemCountText == null && __instance.m_DayText != null)
            {
                PackOpeningController.DuplicateUIComponents(__instance, ref PackOpeningState.HoldItemCountText, "HoldItemCountUI");
                PackOpeningState.HoldItemCountText!.transform.localPosition =
                    new Vector3(SMConfig.HeldPackCountTextX.Value, SMConfig.HeldPackCountTextY.Value, 0f);
                PackOpeningState.HoldItemCountText.fontSizeMax = SMConfig.HeldPackCountTextSize.Value;
                PackOpeningState.HoldItemCountText.fontSize = SMConfig.HeldPackCountTextSize.Value;
                PackOpeningState.HoldItemCountText.fontSizeMin = 1f;
                PackOpeningState.HoldItemCountText.gameObject.SetActive(true);
            }

            if (PackOpeningState.AutoOpenStatusText == null && __instance.m_DayText != null)
            {
                PackOpeningController.DuplicateUIComponents(__instance, ref PackOpeningState.AutoOpenStatusText, "AutoOpenStatusUI");
                PackOpeningState.AutoOpenStatusText!.transform.localPosition =
                    new Vector3(SMConfig.AutoOpenStatusTextX.Value, SMConfig.AutoOpenStatusTextY.Value, 0f);
                PackOpeningState.AutoOpenStatusText.text = "Auto Open: OFF";
                PackOpeningState.AutoOpenStatusText.richText = true;
                PackOpeningState.AutoOpenStatusText.fontSizeMax = SMConfig.AutoOpenStatusTextSize.Value;
                PackOpeningState.AutoOpenStatusText.fontSize = SMConfig.AutoOpenStatusTextSize.Value;
                PackOpeningState.AutoOpenStatusText.fontSizeMin = 1f;
                PackOpeningState.AutoOpenStatusText.gameObject.SetActive(true);
            }

            if (PackOpeningState.HoldItemCountText != null)
            {
                int packCount = 0;

                if (CPlayerData.m_HoldItemTypeList != null && CPlayerData.m_HoldItemTypeList.Count > 0)
                {
                    for (int i = 0; i < CPlayerData.m_HoldItemTypeList.Count; i++)
                    {
                        if (CPlayerData.m_HoldItemTypeList[i].ToString().Contains("Pack"))
                        {
                            packCount++;
                        }
                    }
                }
                else
                {
                    var player = CSingleton<InteractionPlayerController>.instance;
                    var list = player != null ? player.m_HoldItemList : null;

                    if (list != null && list.Count > 0)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var item = list[i];
                            if (item != null && item.m_ItemType.ToString().Contains("Pack"))
                            {
                                packCount++;
                            }
                        }
                    }
                }

                if (packCount <= 0)
                {
                    PackOpeningState.HoldItemCountText.text = "";
                }
                else
                {
                    PackOpeningState.HoldItemCountText.text = $"Packs Left: {packCount}";

                    if (packCount > 50)
                    {
                        PackOpeningState.HoldItemCountText.color = Color.green;
                    }
                    else if (packCount >= 10)
                    {
                        PackOpeningState.HoldItemCountText.color = Color.yellow;
                    }
                    else
                    {
                        PackOpeningState.HoldItemCountText.color = new Color32(255, 140, 0, 255);
                    }
                }
            }

            if (PackOpeningState.AutoOpenStatusText != null)
            {
                bool show = SMConfig.ShowAutoOpenStatusText.Value;
                PackOpeningState.AutoOpenStatusText.gameObject.SetActive(show);

                if (show)
                {
                    bool isOn = PackOpeningState.IsAutoOpen;

                    PackOpeningState.AutoOpenStatusText.text =
                        $"Auto Open: {(isOn ? "<color=green>ON</color>" : "<color=red>OFF</color>")}";
                }
            }
        }
    }
}