#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using ShopManager.Config;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.Debugging
{
    internal static class ShelfScanDebug
    {
        private static readonly Func<ShelfCompartment, int>? _getItemAmount;

        static ShelfScanDebug()
        {
            try
            {
                var fi = AccessTools.Field(typeof(ShelfCompartment), "m_ItemAmount");
                if (fi != null)
                    _getItemAmount = (ShelfCompartment sc) => (int)(fi.GetValue(sc) ?? 0);
            }
            catch { _getItemAmount = null; }
        }

        internal static void LogNullCandidate(int index)
        {
            if (!SMConfig.VerboseScanLogging.Value) return;
            SMLog.Info($"[ShelfScanDebug] idx={index} SKIP:null_candidate");
        }

        // NOTE: parameter names are max + cur so your named args compile
        internal static void LogScanDecision(
            ShelfCompartment? sc,
            string decision,
            EItemType type,
            int max,
            int cur,
            bool includePath = false)
        {
            if (!SMConfig.VerboseScanLogging.Value) return;

            if (sc == null)
            {
                SMLog.Info($"[ShelfScanDebug] {decision} type={type} max={max} cur={cur} (sc=null)");
                return;
            }

            int getItemCount = -1;
            int backingAmount = -1;

            try { getItemCount = sc.GetItemCount(); } catch { }
            try { if (_getItemAmount != null) backingAmount = _getItemAmount(sc); } catch { }

            bool sceneValid = false;
            bool active = false;
            string name = "null";
            int id = 0;
            string path = "";

            try
            {
                name = sc.name;
                id = sc.GetInstanceID();

                if (sc.gameObject != null)
                {
                    sceneValid = sc.gameObject.scene.IsValid();
                    active = sc.gameObject.activeInHierarchy;

                    if (includePath)
                        path = " path=" + GetTransformPath(sc.transform);
                }
            }
            catch { }

            SMLog.Info(
                $"[ShelfScanDebug] {decision} " +
                $"name={name} id={id} sceneValid={sceneValid} active={active} " +
                $"type={type} max={max} cur={cur} GetItemCount={getItemCount} m_ItemAmount={backingAmount}" +
                path
            );
        }

        private static string GetTransformPath(Transform t)
        {
            try
            {
                var parts = new List<string>(16);
                Transform? cur = t;
                while (cur != null)
                {
                    parts.Add(cur.name);
                    cur = cur.parent;
                }
                parts.Reverse();
                return string.Join("/", parts);
            }
            catch
            {
                return "(path_err)";
            }
        }
    }
}