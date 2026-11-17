#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using Knarf.AwCompat;
using ShopManager.Logging;
using UnityEngine;

namespace ShopManager.GameAccess
{
    // One source cache per refill session. Neither the terminal nor the held-box UI
    // opens, and warehouse ownership never passes to Shop Manager.
    internal sealed class AutoWarehouseRefillSource
    {
        private static ConfigEntry<bool>? _enabled;
        private static string? _lastError;
        private readonly object _aw;
        private readonly IDictionary _stored;
        private readonly IDictionary _deposits;
        private readonly IDictionary _incoming;
        private readonly IDictionary _loading;
        private readonly IDictionary _leases;
        private readonly Dictionary<EItemType, List<InteractablePackagingBox_Item>> _boxes = new();
        private readonly Dictionary<EItemType, int> _cursor = new();
        private readonly HashSet<object> _validMachines = new();
        private HashSet<InteractablePackagingBox_Item>? _held;
        private int _checkedFrame = -1;
        private bool _available;
        private bool _faulted;

        internal static void Bind(ConfigFile cfg)
        {
            _enabled = cfg.Bind("Auto Warehouse", "RefillFromAutoWarehouse", false,
                "Use hidden Auto Warehouse 2.0.2 stock when regular warehouse shelf stock runs out.");
        }

        private AutoWarehouseRefillSource(object aw)
        {
            _aw = aw;
            _stored = AutoWarehouseAccess.Map(aw, "stored");
            _deposits = AutoWarehouseAccess.Map(aw, "deposits");
            _incoming = AutoWarehouseAccess.Map(aw, "incomingPurchases");
            _loading = AutoWarehouseAccess.Map(aw, "loadingBoxes");
            _leases = AutoWarehouseAccess.Map(aw, "coopLeases");

            // Resolve save/dashboard hooks before any stock can be changed.
            AutoWarehouseAccess.Field(aw.GetType(), "preparedOwnership");
            AutoWarehouseAccess.Field(aw.GetType(), "stockRefreshRequested");
            AutoWarehouseAccess.Method(aw.GetType(), "CoopChanged");
            AutoWarehouseAccess.Method(aw.GetType(), "RecycleEmptyBox");

            foreach (DictionaryEntry entry in _stored)
            {
                if (!(entry.Key is InteractablePackagingBox_Item box) || box == null ||
                    box.m_ItemCompartment == null || box.m_ItemCompartment.GetItemCount() <= 0)
                    continue;
                var type = box.GetItemType();
                if (type == EItemType.None) continue;
                if (!_boxes.TryGetValue(type, out var group))
                    _boxes.Add(type, group = new List<InteractablePackagingBox_Item>());
                group.Add(box);
            }
            // Drain partial cartons first so AW can free occupied storage slots.
            foreach (var group in _boxes.Values)
                group.Sort((a, b) => a.m_ItemCompartment.GetItemCount().CompareTo(b.m_ItemCompartment.GetItemCount()));
        }

        internal static AutoWarehouseRefillSource? Capture()
        {
            if (_enabled?.Value != true || !AutoWarehouseAccess.Installed)
                return null;
            try
            {
                var aw = AutoWarehouseAccess.Open();
                if (aw == null || AutoWarehouseAccess.Busy(aw))
                    return null;
                var result = new AutoWarehouseRefillSource(aw);
                _lastError = null;
                return result;
            }
            catch (Exception ex)
            {
                Warn(ex);
                return null;
            }
        }

        internal bool HasProduct(EItemType type) => !_faulted && _boxes.ContainsKey(type);

        internal bool TryRefillOne(ShelfCompartment target, EItemType type)
        {
            if (_enabled?.Value != true || _faulted || !_boxes.TryGetValue(type, out var group))
                return false;
            try
            {
                if (!CheckFrame()) return false;
                _cursor.TryGetValue(type, out int next);
                for (int i = next; i < group.Count; i++)
                {
                    if (!Eligible(group[i]))
                        continue;
                    if (!TryTransfer(group[i], target, type))
                        continue;
                    _cursor[type] = i;
                    return true;
                }
                _cursor[type] = group.Count;
            }
            catch (Exception ex)
            {
                _faulted = true;
                Warn(ex);
            }
            return false;
        }

        private bool CheckFrame()
        {
            if (_checkedFrame == Time.frameCount)
                return _available;
            _checkedFrame = Time.frameCount;
            _available = !AutoWarehouseAccess.Busy(_aw);
            _validMachines.Clear();
            if (_available)
            {
                _held = (HashSet<InteractablePackagingBox_Item>)AutoWarehouseAccess.Call(
                    _aw, "ReservedRecoveryBoxes")!;
                foreach (var machine in (IEnumerable)AutoWarehouseAccess.Read(_aw, "machines")!)
                    if (machine != null && AutoWarehouseAccess.ValidMachine(machine))
                        _validMachines.Add(machine);
            }
            _available = _available && _validMachines.Count != 0;
            return _available;
        }

        private bool Eligible(InteractablePackagingBox_Item box)
        {
            return box != null && box.m_ItemCompartment != null &&
                box.m_ItemCompartment.GetItemCount() > 0 &&
                _stored[box] is object terminal && _validMachines.Contains(terminal) &&
                !box.gameObject.activeSelf && !box.m_IsBeingHold && !box.m_IsStored &&
                !box.IsTogglingOpenClose() && box.CanPickup() && box.CanWorkerTakeBox() &&
                !_held!.Contains(box) &&
                !(bool)AutoWarehouseAccess.Call(_aw, "RecoveryPlayerHeld", box)! &&
                !_deposits.Contains(box) &&
                !_incoming.Contains(box) &&
                !_loading.Contains(box) &&
                !_leases.Contains(box);
        }

        private bool TryTransfer(InteractablePackagingBox_Item box, ShelfCompartment target, EItemType type)
        {
            if (target == null || !target.gameObject.activeInHierarchy || !target.m_CanPutItem ||
                target.m_ItemNotForSale || target.GetItemType() != type || !target.HasEnoughSlot())
                return false;
            var slot = target.GetEmptySlotTransform();
            var parent = target.GetEmptySlotParent();
            if (slot == null || parent == null || target.m_StartLoc == null)
                return false;

            var source = box.m_ItemCompartment;
            var from = (List<Item>)AutoWarehouseAccess.Read(source, "m_StoredItemList")!;
            var to = (List<Item>)AutoWarehouseAccess.Read(target, "m_StoredItemList")!;
            bool unopened = !AutoWarehouseAccess.Flag(box, "m_IsFirstOpenDone") && from.Count == 0;
            int count = source.GetItemCount();
            if (count <= 0 || (!unopened && from.Count != count))
                return false;

            // Resolve every stock field before creating or moving anything.
            var amountField = AutoWarehouseAccess.Field(source.GetType(), "m_ItemAmount");
            var countField = AutoWarehouseAccess.Field(box.GetType(), "m_ItemCount");
            var spawnField = AutoWarehouseAccess.Field(box.GetType(), "m_ItemAmountToSpawn");
            var targetAmountField = AutoWarehouseAccess.Field(target.GetType(), "m_ItemAmount");
            var originalAmount = amountField.GetValue(source);
            var originalCount = countField.GetValue(box);
            var originalSpawn = spawnField.GetValue(box);
            var originalTargetAmount = targetAmountField.GetValue(target);
            int beforeShelf = target.GetItemCount();

            Item? item = unopened ? null : from[from.Count - 1];
            if (!unopened && item == null)
                return false;

            var oldParent = item == null ? null : item.transform.parent;
            var oldPosition = item == null ? Vector3.zero : item.transform.localPosition;
            var oldRotation = item == null ? Quaternion.identity : item.transform.localRotation;
            var oldScale = item == null ? Vector3.one : item.transform.localScale;
            bool oldActive = item != null && item.gameObject.activeSelf;
            bool oldEnabled = item != null && item.enabled;
            System.Reflection.FieldInfo? lerping = null, smoothing = null, destination = null;
            object? oldLerp = null, oldSmooth = null, oldDestination = null;
            if (item != null)
            {
                lerping = AutoWarehouseAccess.Field(item.GetType(), "m_IsLerpingToPos");
                smoothing = AutoWarehouseAccess.Field(item.GetType(), "m_IsSmoothLerpingToPos");
                destination = AutoWarehouseAccess.Field(item.GetType(), "m_TargetTransform");
                oldLerp = lerping.GetValue(item);
                oldSmooth = smoothing.GetValue(item);
                oldDestination = destination.GetValue(item);
            }
            bool removed = false;
            bool committed = false;

            try
            {
                if (unopened)
                {
                    item = ItemSpawnManager.GetItem(parent);
                    if (item == null) return false;
                    SetProductMesh(item, type);
                }
                else
                {
                    from.RemoveAt(from.Count - 1);
                    removed = true;
                    item!.transform.SetParent(parent, false);
                    lerping!.SetValue(item, false);
                    smoothing!.SetValue(item, false);
                    destination!.SetValue(item, slot);
                }

                amountField.SetValue(source, count - 1);
                countField.SetValue(box, count - 1);
                spawnField.SetValue(box, count - 1);

                if (item == null) return false;
                item.transform.position = slot.position;
                item.transform.rotation = target.m_StartLoc.rotation;
                item.transform.localScale = slot.localScale;
                item.gameObject.SetActive(true);

                // Some game builds may throw after registering the item. Registration
                // plus a matching count is what commits the transfer.
                try { target.AddItem(item, false); }
                catch when (to.Contains(item) && target.GetItemCount() == beforeShelf + 1) { }
                committed = to.Contains(item) && target.GetItemCount() == beforeShelf + 1;
            }
            finally
            {
                if (!committed)
                {
                    amountField.SetValue(source, originalAmount);
                    countField.SetValue(box, originalCount);
                    spawnField.SetValue(box, originalSpawn);
                    targetAmountField.SetValue(target, originalTargetAmount);

                    if (item != null)
                    {
                        to.Remove(item);
                        if (unopened)
                        {
                            ItemSpawnManager.DisableItem(item);
                        }
                        else if (removed)
                        {
                            from.Add(item);
                            item.transform.SetParent(oldParent, false);
                            item.transform.localPosition = oldPosition;
                            item.transform.localRotation = oldRotation;
                            item.transform.localScale = oldScale;
                            lerping!.SetValue(item, oldLerp);
                            smoothing!.SetValue(item, oldSmooth);
                            destination!.SetValue(item, oldDestination);
                            item.enabled = oldEnabled;
                            item.gameObject.SetActive(oldActive);
                        }
                    }
                }
            }

            if (!committed) return false;
            try
            {
                // AW owns empty cartons and its save/stock refresh. Never destroy the box here.
                if (count == 1)
                    AutoWarehouseAccess.Call(_aw, "RecycleEmptyBox", box);
                else
                    source.SetPriceTagItemAmountText();
            }
            catch (Exception ex)
            {
                Warn(ex); // The stock already moved. Do not report another placement is needed.
            }
            finally
            {
                try
                {
                    AutoWarehouseAccess.Field(_aw.GetType(), "stockRefreshRequested").SetValue(_aw, true);
                    AutoWarehouseAccess.Field(_aw.GetType(), "preparedOwnership").SetValue(_aw, null);
                    AutoWarehouseAccess.Call(_aw, "CoopChanged");
                }
                catch (Exception ex) { Warn(ex); }
            }

            return true;
        }

        // Match the game path for normal meshes and shared texture arrays.
        private static void SetProductMesh(Item item, EItemType type)
        {
            var data = InventoryBase.GetItemMeshData(type);
            var mode = data.arrayMaterialType;
            if (mode == ItemMeshData.ItemShareTexture2DArrayMaterial.None)
            {
                item.SetMesh(data.mesh, data.material, type, data.meshSecondary,
                    data.materialSecondary, data.materialList);
                return;
            }

            item.SetItemType(type);
            switch (mode)
            {
                case ItemMeshData.ItemShareTexture2DArrayMaterial.FirstMesh:
                    item.SetSharedMeshForFirstSlot(ref data.mesh);
                    break;
                case ItemMeshData.ItemShareTexture2DArrayMaterial.SecondMesh:
                    item.SetSharedMeshForSecondSlot(ref data.mesh);
                    break;
                case ItemMeshData.ItemShareTexture2DArrayMaterial.BothMeshes:
                    item.SetSharedMeshForFirstSlot(ref data.mesh);
                    item.SetSharedMeshForSecondSlot(ref data.meshSecondary);
                    break;
                case ItemMeshData.ItemShareTexture2DArrayMaterial.FirstMeshArraySecondDefault:
                    item.SetSharedMeshForFirstSlot(ref data.mesh);
                    item.SetSharedMeshForSecondSlot(ref data.meshSecondary);
                    item.SetSharedMaterialForSecondSlot(ref data.materialSecondary);
                    mode = ItemMeshData.ItemShareTexture2DArrayMaterial.FirstMesh;
                    break;
                default:
                    throw new NotSupportedException($"Unknown item texture mode: {mode}");
            }
            item.SetTexture2DArrayMaterialsForMesh(mode, data.textureIndex, ref data.material);
        }

        private static void Warn(Exception error)
        {
            string message = error.GetBaseException().Message;
            if (message != _lastError)
                SMLog.Warn($"[AW Refill] {message}");
            _lastError = message;
        }
    }
}
