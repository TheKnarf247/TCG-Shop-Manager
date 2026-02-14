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

                // only compartments already labeled
                if (target.GetItemType() != itemType) return false;

                if (!target.HasEnoughSlot()) return false;

                // If target compartment GameObject is inactive, AddItem() will try to StartCoroutine and Unity will yell.
                // Suppress the coroutine path by toggling m_IsSettingPrice via provided methods.
                bool suppressCoroutine = target.gameObject == null || !target.gameObject.activeInHierarchy;

                if (suppressCoroutine)
                    target.OnStartSetPrice();

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

                if (suppressCoroutine)
                    target.OnFinishSetPrice();

                return true;
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[ItemTransferOps] TrySpawnIntoShelfCompartment failed: {ex}");
                return false;
            }
        }
    }
}
