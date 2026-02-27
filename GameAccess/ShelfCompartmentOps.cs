#nullable enable
using System;
using HarmonyLib;
using ShopManager.Logging;

namespace ShopManager.GameAccess
{
    internal static class ShelfCompartmentOps
    {
        private static readonly Func<ShelfCompartment, int>? _getItemAmount;

        static ShelfCompartmentOps()
        {
            try
            {
                var fi = AccessTools.Field(typeof(ShelfCompartment), "m_ItemAmount");
                if (fi != null)
                {
                    _getItemAmount = (ShelfCompartment sc) => (int)(fi.GetValue(sc) ?? 0);
                    SMLog.Info("[ShelfCompartmentOps] Using ShelfCompartment.m_ItemAmount backing field.");
                }
            }
            catch
            {
                _getItemAmount = null;
            }
        }

        internal static int GetCurrentUnits(ShelfCompartment sc)
        {
            if (sc == null) return 0;

            try
            {
                int c = sc.GetItemCount();
                if (c > 0) return c;

                // If the shelf is “full” but items aren’t physically instantiated,
                // m_ItemAmount is the truth.
                if (_getItemAmount != null)
                    return _getItemAmount(sc);
            }
            catch { }

            return 0;
        }
    }
}