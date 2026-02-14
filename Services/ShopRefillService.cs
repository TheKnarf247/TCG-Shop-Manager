#nullable enable
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

            int totalMoved = 0;

            // 1) Grab ALL shop item compartments directly (no Shelf.Init dependency)
            var shopComps = Finders.FindAllShopItemCompartments();
            if (shopComps.Count == 0)
            {
                SMLog.Warn("[ShopManager] No shop compartments found (Shop shelves not detected).");
                return;
            }

            // 2) “Labeled only” = has a concrete assigned item type
            var targets = new List<ShelfCompartment>();
            foreach (var c in shopComps)
            {
                var t = c.GetItemType();
                if (t == EItemType.None) continue; // unlabeled

                int cur = c.GetItemCount();
                int max = c.GetMaxItemCount();
                if (max <= 0) continue;

                if (cur < max)
                    targets.Add(c);

                SMLog.Debug($"[ShopManager] Comp idx={c.GetIndex()} type={t} cur={cur} max={max} target={(cur < max)}");
            }

            if (targets.Count == 0)
            {
                SMLog.Info("[ShopManager] No labeled shop compartments need refilling.");
                return;
            }

            SMLog.Info($"[ShopManager] Refill targets: {targets.Count} compartments.");

            // 3) Build a cache of warehouse big boxes ONCE
            var boxCache = WarehouseBoxOps.BuildBigBoxCache();

            // 4) Refill each target from cached warehouse boxes
            foreach (var comp in targets)
            {
                var itemType = comp.GetItemType();
                if (itemType == EItemType.None) continue;

                int shelfBeforePass = comp.GetItemCount();
                int deficit = comp.GetMaxItemCount() - shelfBeforePass;
                if (deficit <= 0) continue;

                if (!boxCache.TryGetValue(itemType, out var boxes) || boxes.Count == 0)
                {
                    SMLog.Debug($"[ShopManager] Could not fill {itemType} (no matching warehouse stock).");
                    continue;
                }

                int movedForThisComp = 0;

                // Iterate boxes for this item type
                for (int i = 0; i < boxes.Count && movedForThisComp < deficit; i++)
                {
                    var box = boxes[i];
                    if (box == null) continue;

                    // Keep trying this box until either:
                    // - we filled the deficit
                    // - the box is empty
                    // - a move attempt doesn't actually change counts (means our move path isn't valid for that box right now)
                    while (movedForThisComp < deficit && comp.HasEnoughSlot())
                    {
                        int boxBefore = WarehouseBoxOps.GetAvailableUnits(box);
                        if (boxBefore <= 0) break;

                        int shelfBefore = comp.GetItemCount();

                        // Attempt one move
                        if (!WarehouseBoxOps.TryMoveOneItemToShelf(box, comp))
                            break;

                        int boxAfter = WarehouseBoxOps.GetAvailableUnits(box);
                        int shelfAfter = comp.GetItemCount();

                        bool boxChanged = boxAfter < boxBefore;
                        bool shelfChanged = shelfAfter > shelfBefore;

                        if (!boxChanged && !shelfChanged)
                        {
                            // Important: DO NOT count this as a move.
                            // Also: stop using this box, because we're not actually affecting inventory/state.
                            SMLog.Info($"[ShopManager] Move did not change counts. type={itemType} boxBefore={boxBefore} boxAfter={boxAfter} shelfBefore={shelfBefore} shelfAfter={shelfAfter}");
                            break;
                        }

                        movedForThisComp++;
                        totalMoved++;
                    }
                }

                if (movedForThisComp > 0)
                    SMLog.Info($"[ShopManager] Filled {movedForThisComp}/{deficit} for {itemType}.");
                else
                    SMLog.Debug($"[ShopManager] Could not fill {itemType} (no matching warehouse stock or move failed).");
            }

            SMLog.Info($"[ShopManager] Refill complete. Items moved: {totalMoved}.");
        }
    }
}
