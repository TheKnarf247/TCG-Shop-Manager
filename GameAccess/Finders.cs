#nullable enable
using System.Collections.Generic;
using UnityEngine;
using ShopManager.Logging;

namespace ShopManager.GameAccess
{
    internal static class Finders
    {
        internal static List<ShelfCompartment> FindAllShopItemCompartments()
        {
            var result = new List<ShelfCompartment>();

            try
            {
                var all = Resources.FindObjectsOfTypeAll<ShelfCompartment>();
                if (all == null || all.Length == 0) return result;

                int seen = 0, sceneInvalid = 0, notSale = 0, notItem = 0, isBoxSlot = 0;

                foreach (var c in all)
                {
                    if (c == null || c.gameObject == null) continue;
                    seen++;

                    // must be in an actual loaded scene (filters prefabs/resources)
                    if (!c.gameObject.scene.IsValid())
                    {
                        sceneInvalid++;
                        continue;
                    }

                    // ignore decorative / not-for-sale
                    if (c.m_ItemNotForSale)
                    {
                        notSale++;
                        continue;
                    }

                    // shop shelves are item compartments
                    if (!c.m_CanPutItem)
                    {
                        notItem++;
                        continue;
                    }

                    // warehouse compartments are box compartments
                    if (c.m_CanPutBox)
                    {
                        isBoxSlot++;
                        continue;
                    }

                    result.Add(c);
                }

                SMLog.Debug($"[Finders] ShelfCompartment scan: seen={seen} shopFound={result.Count} sceneInvalid={sceneInvalid} notForSale={notSale} notItem={notItem} boxSlotsSkipped={isBoxSlot}");
            }
            catch (System.Exception ex)
            {
                SMLog.Warn($"[Finders] FindAllShopItemCompartments failed: {ex}");
            }

            return result;
        }

        internal static List<WarehouseShelf> FindAllWarehouseShelves()
        {
            var result = new List<WarehouseShelf>();

            try
            {
                var all = Resources.FindObjectsOfTypeAll<WarehouseShelf>();
                if (all == null || all.Length == 0) return result;

                foreach (var ws in all)
                {
                    if (ws == null || ws.gameObject == null) continue;
                    if (!ws.gameObject.scene.IsValid()) continue;
                    result.Add(ws);
                }
            }
            catch (System.Exception ex)
            {
                SMLog.Warn($"[Finders] FindAllWarehouseShelves failed: {ex}");
            }

            return result;
        }
    }
}
