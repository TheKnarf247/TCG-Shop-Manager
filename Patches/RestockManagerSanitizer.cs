#nullable enable
using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.Patches
{
    [HarmonyPatch(typeof(RestockManager))]
    internal static class RestockManagerSanitizer
    {
        private static FieldInfo[]? _listFields;
        private static float _nextCleanup;

        private static FieldInfo[] GetListFields(RestockManager inst)
        {
            if (_listFields != null) return _listFields;

            var t = inst.GetType();
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Only keep IList fields (List<T>, etc.) to avoid touching unrelated stuff
            var tmp = new System.Collections.Generic.List<FieldInfo>();
            foreach (var fi in fields)
            {
                if (typeof(IList).IsAssignableFrom(fi.FieldType))
                    tmp.Add(fi);
            }

            _listFields = tmp.ToArray();
            return _listFields;
        }

        /// <summary>
        /// Safety net: periodically prune any Unity-null entries from RestockManager lists.
        /// </summary>
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        private static void UpdatePrefix(RestockManager __instance)
        {
            // Don't do heavy reflection every frame
            if (Time.unscaledTime < _nextCleanup) return;
            _nextCleanup = Time.unscaledTime + 0.50f;

            try { PruneLists(__instance); }
            catch (Exception ex)
            {
                SMLog.Debug($"[ShopManager] RestockManager sanitizer failed: {ex.Message}");
            }
        }

        internal static void PruneLists(RestockManager inst)
        {
            int removed = 0;

            foreach (var fi in GetListFields(inst))
            {
                var list = fi.GetValue(inst) as IList;
                if (list == null || list.Count == 0) continue;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var obj = list[i];

                    // Remove true null
                    if (obj == null)
                    {
                        list.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    // Remove Unity "fake null" (destroyed objects)
                    if (obj is UnityEngine.Object uo && uo == null)
                    {
                        list.RemoveAt(i);
                        removed++;
                        continue;
                    }
                }
            }

            if (removed > 0)
                SMLog.Debug($"[ShopManager] RestockManager sanitizer removed {removed} stale refs.");
        }
    }
}