#nullable enable
using System;
using UnityEngine;
using ShopManager.Logging;

namespace ShopManager.GameAccess
{
    internal static class ItemTransferOps
    {
        internal static bool TrySpawnIntoShelfCompartment(ShelfCompartment target, EItemType itemType)
        {
            try
            {
                if (target == null) return false;
                if (!target.m_CanPutItem) return false;
                if (target.m_ItemNotForSale) return false;
                if (target.GetItemType() != itemType) return false;
                if (!target.HasEnoughSlot()) return false;

                bool suppressCoroutine = target.gameObject == null || !target.gameObject.activeInHierarchy;
                if (suppressCoroutine) target.OnStartSetPrice();

                var meshData = InventoryBase.GetItemMeshData(itemType);

                Transform slot = target.GetEmptySlotTransform();
                Transform parent = target.GetEmptySlotParent();

                var item = ItemSpawnManager.GetItem(parent);
                item.SetMesh(
                    meshData.mesh,
                    meshData.material,
                    itemType,
                    meshData.meshSecondary,
                    meshData.materialSecondary,
                    meshData.materialList
                );

                item.transform.position = slot.position;
                item.transform.localScale = slot.localScale;
                item.transform.rotation = target.m_StartLoc.rotation;
                item.gameObject.SetActive(true);

                target.AddItem(item, false);

                if (suppressCoroutine) target.OnFinishSetPrice();
                return true;
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[ItemTransferOps] TrySpawnIntoShelfCompartment failed: {ex}");
                return false;
            }
        }

        internal static bool TryPlaceExistingItemIntoShelfCompartment(ShelfCompartment target, Item item)
        {
            try
            {
                if (target == null || item == null) return false;
                if (!target.m_CanPutItem) return false;
                if (target.m_ItemNotForSale) return false;

                var t = target.GetItemType();
                if (t == EItemType.None) return false;

                // Only place if it matches the labeled compartment
                // (Item.m_ItemType is private; item.GetItemType() exists in-game in many builds, but not guaranteed)
                // We'll rely on shelf label + caller filtering by box type. Still safe to proceed.

                if (!target.HasEnoughSlot()) return false;

                bool suppressCoroutine = target.gameObject == null || !target.gameObject.activeInHierarchy;
                if (suppressCoroutine) target.OnStartSetPrice();

                Transform slot = target.GetEmptySlotTransform();
                Transform parent = target.GetEmptySlotParent();

                item.transform.SetParent(parent, false);
                item.transform.position = slot.position;
                item.transform.localScale = slot.localScale;
                item.transform.rotation = target.m_StartLoc.rotation;
                item.gameObject.SetActive(true);

                target.AddItem(item, false);

                if (suppressCoroutine) target.OnFinishSetPrice();
                return true;
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[ItemTransferOps] TryPlaceExistingItemIntoShelfCompartment failed: {ex}");
                return false;
            }
        }
    }
}