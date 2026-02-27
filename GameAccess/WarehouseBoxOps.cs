#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.GameAccess
{
    internal static class WarehouseBoxOps
    {
        // ShelfCompartment backing count field (used when compartment inactive/unspawned)
        private static readonly Func<ShelfCompartment, int>? _getItemAmount;
        private static readonly Action<ShelfCompartment, int>? _setItemAmount;

        static WarehouseBoxOps()
        {
            try
            {
                var fi = AccessTools.Field(typeof(ShelfCompartment), "m_ItemAmount");
                if (fi != null)
                {
                    _getItemAmount = (ShelfCompartment sc) => (int)(fi.GetValue(sc) ?? 0);
                    _setItemAmount = (ShelfCompartment sc, int v) => fi.SetValue(sc, v);
                    SMLog.Info("[WarehouseBoxOps] Using ShelfCompartment.m_ItemAmount backing field.");
                }
            }
            catch
            {
                _getItemAmount = null;
                _setItemAmount = null;
            }
        }

        internal static Dictionary<EItemType, List<InteractablePackagingBox_Item>> BuildBoxCache(bool includeStored)
        {
            var dict = new Dictionary<EItemType, List<InteractablePackagingBox_Item>>();

            List<InteractablePackagingBox_Item>? boxes = null;
            try
            {
                boxes = RestockManager.GetItemPackagingBoxListWithItem(includeStored);
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[WarehouseBoxOps] RestockManager box list not available: {ex.Message}");
            }

            if (boxes == null || boxes.Count == 0)
                return dict;

            foreach (var box in boxes)
            {
                if (box == null) continue;
                if (!box.IsValidObject()) continue;
                if (box.m_ItemCompartment == null) continue;

                var t = box.GetItemType();
                if (t == EItemType.None) continue;

                int count = GetAvailableUnits(box);
                if (count <= 0) continue;

                if (!dict.TryGetValue(t, out var list))
                {
                    list = new List<InteractablePackagingBox_Item>();
                    dict[t] = list;
                }

                list.Add(box);
            }

            foreach (var kv in dict)
                kv.Value.Sort((a, b) => GetAvailableUnits(b).CompareTo(GetAvailableUnits(a)));

            return dict;
        }

        internal static int GetAvailableUnits(InteractablePackagingBox_Item box)
        {
            if (box == null) return 0;

            try
            {
                if (box.m_ItemCompartment != null)
                {
                    int c = box.m_ItemCompartment.GetItemCount();
                    if (c > 0) return c;

                    if (_getItemAmount != null)
                        return _getItemAmount(box.m_ItemCompartment);
                }
            }
            catch { /* ignore */ }

            return 0;
        }

        internal static bool TryConsumeOneAndGetItemFromOpenedBox(
            InteractablePackagingBox_Item box,
            out Item? itemOut)
        {
            itemOut = null;

            if (box == null || !box.IsValidObject()) return false;
            if (box.m_ItemCompartment == null) return false;
            if (!box.IsBoxOpened()) return false;

            try
            {
                var item = box.m_ItemCompartment.GetFirstItem();
                if (item == null) return false;

                box.m_ItemCompartment.RemoveItem(item);

                // Keep the box’s own UI consistent
                try { box.m_ItemCompartment.SetPriceTagItemAmountText(); } catch { /* ignore */ }

                itemOut = item;
                return true;
            }
            catch (Exception ex)
            {
                SMLog.Debug($"[WarehouseBoxOps] Opened consume failed: {ex.Message}");
                return false;
            }
        }

        internal static bool TryConsumeOneFromUnopenedBox(InteractablePackagingBox_Item box)
        {
            if (box == null || !box.IsValidObject()) return false;
            if (box.m_ItemCompartment == null) return false;
            if (box.IsBoxOpened()) return false;

            int before = GetAvailableUnits(box);
            if (before <= 0) return false;

            int after = before - 1;

            try
            {
                // Keep backing field consistent when available
                if (_setItemAmount != null)
                    _setItemAmount(box.m_ItemCompartment, after);

                box.m_ItemCompartment.PreSpawnItemUpdate(after);

                if (after > 0)
                {
                    try { box.m_ItemCompartment.SetPriceTagItemAmountText(); } catch { /* ignore */ }
                }
                else
                {
                    DeferDestroy(box);
                }

                return true;
            }
            catch (Exception ex)
            {
                SMLog.Debug($"[WarehouseBoxOps] Unopened consume failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Option A: After refill, brute-refresh every warehouse BOX SLOT (ShelfCompartment.m_CanPutBox == true)
        /// so the rack UI catches up immediately.
        /// </summary>
        internal static void RefreshAllWarehouseBoxSlotLabels()
        {
            int refreshed = 0;

            try
            {
                var all = Resources.FindObjectsOfTypeAll<ShelfCompartment>();
                if (all == null || all.Length == 0) return;

                foreach (var sc in all)
                {
                    if (sc == null || sc.gameObject == null) continue;
                    if (!sc.gameObject.scene.IsValid()) continue;

                    if (!sc.m_CanPutBox) continue;

                    try
                    {
                        sc.SetPriceTagItemAmountText();
                        // Optional but can help icons update for some racks:
                        sc.SetPriceTagItemImage();
                        refreshed++;
                    }
                    catch { /* ignore per-compartment */ }
                }
            }
            catch (Exception ex)
            {
                SMLog.Debug($"[WarehouseBoxOps] RefreshAllWarehouseBoxSlotLabels failed: {ex.Message}");
            }

            SMLog.Info($"[WarehouseBoxOps] Refreshed {refreshed} warehouse box-slot compartments.");
        }

        /// <summary>
        /// When a box hits 0, destroying it without removing it from the warehouse slot list
        /// can leave stale UI/state. This tries to detach it first, then destroys.
        /// </summary>
        private static void SafelyDestroyBoxAndDetachFromWarehouseSlot(InteractablePackagingBox_Item box)
        {
            if (box == null) return;

            // Best-effort: find the warehouse slot compartment that still thinks it contains this box
            try
            {
                var all = Resources.FindObjectsOfTypeAll<ShelfCompartment>();
                if (all != null)
                {
                    foreach (var sc in all)
                    {
                        if (sc == null || sc.gameObject == null) continue;
                        if (!sc.gameObject.scene.IsValid()) continue;
                        if (!sc.m_CanPutBox) continue;

                        List<InteractablePackagingBox_Item>? list = null;
                        try { list = sc.GetInteractablePackagingBoxList(); } catch { }

                        if (list == null || list.Count == 0) continue;

                        // Unity destroyed-object weirdness: use ReferenceEquals and also Unity null checks.
                        bool contains = false;
                        for (int i = 0; i < list.Count; i++)
{
    var b = list[i];
    if (b == null) continue;
    if ((UnityEngine.Object)b == null) continue; // Unity fake-null

    if (ReferenceEquals(b, box))
    {
        contains = true;
        break;
    }
}

                        if (contains)
                        {
                            try { sc.RemoveBox(box); } catch { /* ignore */ }
                            break;
                        }
                    }
                }
            }
            catch { /* ignore */ }

            // Now kill the box
            try
            {
                if (box.m_ItemCompartment != null)
                {
                    try { box.m_ItemCompartment.SetPriceTagVisibility(false); } catch { }
                    try { box.m_ItemCompartment.PreSpawnItemUpdate(0); } catch { }
                }
            }
            catch { /* ignore */ }

            try { UnityEngine.Object.Destroy(box.gameObject); } catch { /* ignore */ }
        }
        private static readonly List<InteractablePackagingBox_Item> _destroyQueue = new();

        internal static void DeferDestroy(InteractablePackagingBox_Item box)
        {
            if (box == null) return;
            if (!_destroyQueue.Contains(box))
                _destroyQueue.Add(box);
        }

        internal static void FlushDeferredDestroys()
        {
            if (_destroyQueue.Count == 0) return;

            for (int i = 0; i < _destroyQueue.Count; i++)
            {
                var b = _destroyQueue[i];
                if (b == null) continue;
                if ((UnityEngine.Object)b == null) continue;
                SafelyDestroyBoxAndDetachFromWarehouseSlot(b);
            }
            _destroyQueue.Clear();
        }
    }
}