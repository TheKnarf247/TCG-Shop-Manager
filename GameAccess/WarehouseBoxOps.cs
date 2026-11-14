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
        private static int _consolidationStartIndex = 0;
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

            try
            {
                var all = Resources.FindObjectsOfTypeAll<ShelfCompartment>();
                if (all == null || all.Length == 0)
                    return dict;

                foreach (var sc in all)
                {
                    if (sc == null || sc.gameObject == null) continue;
                    if (!sc.gameObject.scene.IsValid()) continue;
                    if (!sc.m_CanPutBox) continue;

                    List<InteractablePackagingBox_Item>? boxes = null;
                    try { boxes = sc.GetInteractablePackagingBoxList(); } catch { }

                    if (boxes == null || boxes.Count == 0) continue;

                    for (int i = 0; i < boxes.Count; i++)
                    {
                        var box = boxes[i];
                        if (box == null) continue;
                        if ((UnityEngine.Object)box == null) continue;
                        if (!box.IsValidObject()) continue;
                        if (box.m_ItemCompartment == null) continue;
                        if (box.IsBoxOpened()) continue;

                        var t = box.GetItemType();
                        if (t == EItemType.None) continue;

                        int count = GetAvailableUnits(box);
                        if (count <= 0) continue;

                        if (!dict.TryGetValue(t, out var list))
                        {
                            list = new List<InteractablePackagingBox_Item>();
                            dict[t] = list;
                        }

                        // avoid duplicate refs if the game reports the same box more than once
                        if (!list.Contains(box))
                            list.Add(box);
                    }
                }
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[WarehouseBoxOps] BuildBoxCache failed: {ex.Message}");
            }

            return dict;
        }

        // New warehouse storage uses lightweight StoredBoxRecord entries instead of box GameObjects.
        // Cache compartments, not record indices: AddStoredBoxRecord reorders records.
        internal static Dictionary<EItemType, List<ShelfCompartment>> BuildStoredBoxCache()
        {
            var cache = new Dictionary<EItemType, List<ShelfCompartment>>();
            int slots = 0, records = 0;
            try
            {
                foreach (var sc in Resources.FindObjectsOfTypeAll<ShelfCompartment>())
                {
                    if (sc == null || sc.gameObject == null || !sc.gameObject.scene.IsValid()) continue;
                    if (sc.GetWarehouseShelf() == null) continue;
                    slots++;
                    int count = sc.GetStoredBoxRecordCount();
                    records += count;
                    // Pop API only supports the last record. Each shelf is normally one item type.
                    for (int i = 0; i < count; i++)
                    {
                        var record = sc.PeekStoredBoxRecord(i);
                        if (record.itemType == EItemType.None || record.amount <= 0) continue;
                        if (!cache.TryGetValue(record.itemType, out var list))
                            cache[record.itemType] = list = new List<ShelfCompartment>();
                        if (!list.Contains(sc)) list.Add(sc);
                    }
                }
                SMLog.Info($"[WarehouseBoxOps] Stored box cache: slots={slots} records={records} types={cache.Count}");
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[WarehouseBoxOps] BuildStoredBoxCache failed: {ex}");
            }
            return cache;
        }

        // Only consume from the LAST record, matching the game's supported pop operation.
        internal static bool TryConsumeStoredUnit(ShelfCompartment sc, EItemType type)
        {
            if (sc == null || sc.GetStoredBoxRecordCount() == 0) return false;
            int last = sc.GetStoredBoxRecordCount() - 1;
            var peek = sc.PeekStoredBoxRecord(last);
            if (peek.itemType != type || peek.amount <= 0) return false;
            if (!sc.TryPopLastStoredBoxRecord(out var record)) return false;
            record.amount--;
            if (record.amount > 0) sc.AddStoredBoxRecord(record);
            return true;
        }

        internal static void RestoreStoredUnit(ShelfCompartment sc, EItemType type, bool isBigBox)
        {
            if (sc == null) return;
            // Rollback is only used after a failed spawn. Restore as a separate 1-unit record
            // rather than risking modification of an unrelated record.
            sc.AddStoredBoxRecord(new StoredBoxRecord { itemType = type, amount = 1, isBigBox = isBigBox });
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
        internal static bool TryAddOneToUnopenedBox(InteractablePackagingBox_Item box)
        {
            if (box == null || !box.IsValidObject()) return false;
            if (box.m_ItemCompartment == null) return false;
            if (box.IsBoxOpened()) return false;

            try
            {
                int before = GetAvailableUnits(box);
                int max = box.m_ItemCompartment.GetMaxItemCount();
                if (before >= max) return false;

                int after = before + 1;

                if (_setItemAmount != null)
                    _setItemAmount(box.m_ItemCompartment, after);

                box.m_ItemCompartment.PreSpawnItemUpdate(after);
                return true;
            }
            catch (Exception ex)
            {
                SMLog.Debug($"[WarehouseBoxOps] Add to unopened box failed: {ex.Message}");
                return false;
            }
        }
        internal static bool ConsolidateBoxesStep(
            Dictionary<EItemType, List<InteractablePackagingBox_Item>> boxCache,
            int maxMovesPerCall)
        {
            if (boxCache == null || boxCache.Count == 0)
                return true;

            int moves = 0;

            var keys = boxCache.Keys.ToList();
            if (keys.Count == 0)
                return true;

            for (int k = 0; k < keys.Count; k++)
            {
                int keyIndex = (_consolidationStartIndex + k) % keys.Count;
                var key = keys[keyIndex];
                var boxes = boxCache[key];

                if (boxes == null || boxes.Count < 2)
                    continue;

                // Clean list first
                boxes.RemoveAll(b =>
                    b == null ||
                    (UnityEngine.Object)b == null ||
                    !b.IsValidObject() ||
                    b.m_ItemCompartment == null ||
                    b.IsBoxOpened() ||
                    GetAvailableUnits(b) <= 0);

                if (boxes.Count < 2)
                    continue;

                // fullest first so we fill one box up before moving to the next
                boxes.Sort((a, b) => GetAvailableUnits(b).CompareTo(GetAvailableUnits(a)));

                int targetIndex = 0;
                int sourceIndex = boxes.Count - 1;

                while (targetIndex < sourceIndex)
                {
                    var target = boxes[targetIndex];
                    var source = boxes[sourceIndex];

                    if (target == null || source == null)
                    {
                        sourceIndex--;
                        continue;
                    }

                    if (target == source)
                    {
                        sourceIndex--;
                        continue;
                    }

                    if (target.IsBoxOpened() || source.IsBoxOpened())
                    {
                        sourceIndex--;
                        continue;
                    }

                    int targetCount = GetAvailableUnits(target);
                    int sourceCount = GetAvailableUnits(source);
                    int targetMax = target.m_ItemCompartment?.GetMaxItemCount() ?? 0;

                    if (sourceCount <= 0)
                    {
                        sourceIndex--;
                        continue;
                    }

                    if (targetCount >= targetMax)
                    {
                        targetIndex++;
                        continue;
                    }

                    int movesPossible = Math.Min(targetMax - targetCount, sourceCount);
                    if (movesPossible <= 0)
                    {
                        SMLog.Debug($"[ShopManager] No consolidation possible for {key} (target full or source empty)");
                        sourceIndex--;
                        continue;
                    }

                    while (movesPossible > 0 && moves < maxMovesPerCall)
                    {
                        if (!TryConsumeOneFromUnopenedBox(source))
                            break;

                        if (!TryAddOneToUnopenedBox(target))
                        {
                            // best-effort rollback
                            TryAddOneToUnopenedBox(source);
                            break;
                        }

                        moves++;
                        SMLog.Debug($"[ShopManager] Consolidated 1 item for {key} (move {moves}/{maxMovesPerCall})");
                        movesPossible--;
                    }

                    if (GetAvailableUnits(source) <= 0)
                        sourceIndex--;

                    if (GetAvailableUnits(target) >= targetMax)
                        targetIndex++;

                    // Budget exhausted: stop now and continue next frame
                    if (moves >= maxMovesPerCall)
                    {
                        boxes.RemoveAll(b =>
                            b == null ||
                            (UnityEngine.Object)b == null ||
                            GetAvailableUnits(b) <= 0);

                        boxes.Sort((a, b) => GetAvailableUnits(a).CompareTo(GetAvailableUnits(b)));

                        _consolidationStartIndex = (_consolidationStartIndex + 1) % keys.Count;

                        SMLog.Debug($"[ShopManager] Consolidation tick complete. Moves={moves}");
                        return false; // not done yet
                    }
                }

                // Keep refill order as least-full first after consolidation work
                boxes.RemoveAll(b =>
                    b == null ||
                    (UnityEngine.Object)b == null ||
                    GetAvailableUnits(b) <= 0);

                boxes.Sort((a, b) => GetAvailableUnits(a).CompareTo(GetAvailableUnits(b)));
            }

            if (moves == 0)
                SMLog.Debug("[ShopManager] No consolidation moves performed this tick.");

            SMLog.Debug($"[ShopManager] Consolidation tick complete. Moves={moves}");

            if (keys.Count > 0)
                _consolidationStartIndex = (_consolidationStartIndex + 1) % keys.Count;

            return true; // all consolidation complete
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

                        // Labels work for both physical boxes and StoredBoxRecord-backed inventory.
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
                            try { sc.SetPriceTagItemAmountText(); } catch { }
                            // try { sc.SetPriceTagItemImage(); } catch { }
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
