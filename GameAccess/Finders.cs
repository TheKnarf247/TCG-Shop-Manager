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

                    if (!c.gameObject.scene.IsValid())
                    {
                        sceneInvalid++;
                        continue;
                    }

                    if (c.m_ItemNotForSale)
                    {
                        notSale++;
                        continue;
                    }

                    if (!c.m_CanPutItem)
                    {
                        notItem++;
                        continue;
                    }

                    // Warehouse shelf compartments are often box slots
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
    }
}