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
        // Private field access (ShelfCompartment.m_ItemAmount)
        private static readonly Func<ShelfCompartment, int>? _getItemAmount;
        private static readonly Action<ShelfCompartment, int>? _setItemAmount;

        private static bool _loggedBackingField;

        static WarehouseBoxOps()
        {
            try
            {
                var fi = AccessTools.Field(typeof(ShelfCompartment), "m_ItemAmount");
                if (fi != null)
                {
                    _getItemAmount = (ShelfCompartment sc) => (int)(fi.GetValue(sc) ?? 0);
                    _setItemAmount = (ShelfCompartment sc, int v) => fi.SetValue(sc, v);

                    if (!_loggedBackingField)
                    {
                        _loggedBackingField = true;
                        SMLog.Info("[WarehouseBoxOps] Discovered backing count field: ShelfCompartment.m_ItemAmount");
                    }
                }
            }
            catch
            {
                _getItemAmount = null;
                _setItemAmount = null;
            }
        }

        /// <summary>
        /// New: Build cache using the same list the game uses (RestockManager),
        /// including stored boxes, small boxes, and inactive compartments.
        /// </summary>
        internal static Dictionary<EItemType, List<InteractablePackagingBox_Item>> BuildBigBoxCache()
        {
            // Keep the old method name so you don't have to touch other files.
            return BuildBoxCache(includeStored: true);
        }

        internal static Dictionary<EItemType, List<InteractablePackagingBox_Item>> BuildBoxCache(bool includeStored)
        {
            var dict = new Dictionary<EItemType, List<InteractablePackagingBox_Item>>();

            List<InteractablePackagingBox_Item>? boxes = null;

            try
            {
                // Best source: the game's centralized tracking list
                boxes = RestockManager.GetItemPackagingBoxListWithItem(includeStored);
            }
            catch (Exception ex)
            {
                SMLog.Warn($"[WarehouseBoxOps] RestockManager box list not available yet: {ex.Message}");
            }

            if (boxes == null || boxes.Count == 0)
                return dict;

            foreach (var box in boxes)
            {
                if (box == null) continue;
                if (!box.IsValidObject()) continue;

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

            // Optional: prefer boxes with more stock first (reduces box switching)
            foreach (var kv in dict)
                kv.Value.Sort((a, b) => GetAvailableUnits(b).CompareTo(GetAvailableUnits(a)));

            return dict;
        }

        /// <summary>
        /// Returns item count from a packaging box, even when the internal compartment is inactive.
        /// </summary>
        internal static int GetAvailableUnits(InteractablePackagingBox_Item box)
        {
            if (box == null) return 0;

            try
            {
                // If the item compartment exists and reports a count, use it
                if (box.m_ItemCompartment != null)
                {
                    int c = box.m_ItemCompartment.GetItemCount();
                    if (c > 0) return c;

                    // If inactive, GetItemCount() may lie (0). Use backing field if possible.
                    if (_getItemAmount != null)
                    {
                        int raw = _getItemAmount(box.m_ItemCompartment);
                        return raw;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return 0;
        }

        /// <summary>
        /// Worker-like move: ask the box to dispense into the shelf compartment.
        /// This is the most "legit" transfer path.
        /// </summary>
        internal static bool TryMoveOneItemToShelf(
            InteractablePackagingBox_Item box,
            ShelfCompartment shelfComp)
        {
            if (box == null || shelfComp == null)
                return false;

            if (!box.IsValidObject())
                return false;

            if (!shelfComp.HasEnoughSlot())
                return false;

            var boxType = box.GetItemType();
            var shelfType = shelfComp.GetItemType();
            if (boxType == EItemType.None || shelfType == EItemType.None || boxType != shelfType)
                return false;

            int shelfBefore = shelfComp.GetItemCount();
            int boxBefore = GetAvailableUnits(box);

            try
            {
                // Your build requires (bool isPlayer, ShelfCompartment target)
                box.DispenseItem(false, shelfComp);
            }
            catch (Exception ex)
            {
                SMLog.Debug($"[WarehouseBoxOps] Dispense exception: {ex.Message}");
                return false;
            }

            int shelfAfter = shelfComp.GetItemCount();
            int boxAfter = GetAvailableUnits(box);

            bool shelfChanged = shelfAfter > shelfBefore;
            bool boxChanged = boxAfter < boxBefore;

            // If neither changed, treat as failure so caller can try next box
            return (shelfChanged || boxChanged);
        }

        /// <summary>
        /// Best-effort rollback: add N units back into a box by adjusting count.
        /// (Rarely needed when using DispenseItem, but kept for safety.)
        /// </summary>
        internal static void AddUnits(InteractablePackagingBox_Item box, int units)
        {
            if (box == null || units <= 0) return;
            if (box.m_ItemCompartment == null) return;

            if (_setItemAmount == null) return;

            try
            {
                int cur = (_getItemAmount != null) ? _getItemAmount(box.m_ItemCompartment) : box.m_ItemCompartment.GetItemCount();
                int next = cur + units;

                _setItemAmount(box.m_ItemCompartment, next);
                box.m_ItemCompartment.PreSpawnItemUpdate(next);
            }
            catch
            {
                // ignore
            }
        }
    }
}
