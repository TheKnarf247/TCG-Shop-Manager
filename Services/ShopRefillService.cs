#nullable enable
using System;
using System.Collections.Generic;
using ShopManager.Config;
using ShopManager.GameAccess;
using ShopManager.Logging;

namespace ShopManager.Services
{
    internal static class ShopRefillService
    {
        internal static void RunOnce()
        {
            if (!SMConfig.Enabled.Value) return;

            // 1) Find shop compartments
            var shopComps = Finders.FindAllShopItemCompartments();
            if (shopComps.Count == 0)
            {
                SMLog.Warn("[ShopManager] No shop compartments found.");
                return;
            }

            // 2) Build targets grouped by type
            var targetsByType = new Dictionary<EItemType, List<ShelfCompartment>>();
            int targetCount = 0;

            foreach (var c in shopComps)
            {
                var t = c.GetItemType();
                if (t == EItemType.None) continue;

                int cur = c.GetItemCount();
                int max = c.GetMaxItemCount();
                if (max <= 0) continue;
                if (cur >= max) continue;

                if (!targetsByType.TryGetValue(t, out var list))
                {
                    list = new List<ShelfCompartment>();
                    targetsByType[t] = list;
                }

                list.Add(c);
                targetCount++;
            }

            if (targetCount == 0)
            {
                SMLog.Info("[ShopManager] No labeled shop compartments need refilling.");
                return;
            }

            // 3) Build box cache (include stored)
            var boxCache = WarehouseBoxOps.BuildBoxCache(includeStored: true);

            int totalPlaced = 0;
            int totalConsumed = 0;

            // 4) For each item type, fill compartments using boxes of that type
            foreach (var kv in targetsByType)
            {
                var type = kv.Key;
                var comps = kv.Value;

                if (!boxCache.TryGetValue(type, out var boxes) || boxes.Count == 0)
                {
                    SMLog.Debug($"[ShopManager] No warehouse boxes for {type}.");
                    continue;
                }

                int boxIndex = 0;

                foreach (var comp in comps)
                {
                    int cur = comp.GetItemCount();
                    int max = comp.GetMaxItemCount();
                    int need = max - cur;
                    if (need <= 0) continue;

                    int filled = 0;

                    while (filled < need && comp.HasEnoughSlot())
                    {
                        // advance to next box with stock
                        while (boxIndex < boxes.Count && WarehouseBoxOps.GetAvailableUnits(boxes[boxIndex]) <= 0)
                            boxIndex++;

                        if (boxIndex >= boxes.Count)
                            break;

                        var box = boxes[boxIndex];
                        if (box == null) { boxIndex++; continue; }

                        // OPENED BOX: real item move
                        if (box.IsBoxOpened())
                        {
                            if (!WarehouseBoxOps.TryConsumeOneAndGetItemFromOpenedBox(box, out var item) || item == null)
                            {
                                boxIndex++; // box not usable right now
                                continue;
                            }

                            bool placed = ItemTransferOps.TryPlaceExistingItemIntoShelfCompartment(comp, item);
                            if (!placed)
                            {
                                try { item.transform.SetParent(box.transform, true); } catch { }
                                SMLog.Debug($"[ShopManager] Place failed after consume (opened). type={type}");
                                break;
                            }

                            totalConsumed++;
                            totalPlaced++;
                            filled++;
                            continue;
                        }

                        // UNOPENED BOX: decrement count, then spawn onto shelf
                        bool consumed = WarehouseBoxOps.TryConsumeOneFromUnopenedBox(box);
                        if (!consumed)
                        {
                            boxIndex++; // not usable
                            continue;
                        }

                        bool spawned = ItemTransferOps.TrySpawnIntoShelfCompartment(comp, type);
                        if (!spawned)
                        {
                            SMLog.Warn($"[ShopManager] Spawn failed after consume (unopened). type={type} (inventory may desync by 1)");
                            break;
                        }

                        totalConsumed++;
                        totalPlaced++;
                        filled++;
                    }

                    if (filled > 0)
                        SMLog.Info($"[ShopManager] Filled {filled}/{need} for {type}.");
                }
            }

            // OPTION A: brute-refresh rack box-slot UI after all moves
            WarehouseBoxOps.RefreshAllWarehouseBoxSlotLabels();

            SMLog.Info($"[ShopManager] Refill complete. Placed={totalPlaced} Consumed={totalConsumed}.");
        }
    }
}