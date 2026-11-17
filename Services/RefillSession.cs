#nullable enable
using System;
using System.Collections.Generic;
using ShopManager.Config;
using ShopManager.GameAccess;
using ShopManager.Logging;
using UnityEngine;
using ShopManager.Debugging;

namespace ShopManager.Services
{
    internal sealed class RefillSession
    {
        private bool _loggedConsolidationStart;
        internal enum Phase
        {
            None,
            GatherCandidates,
            ScanShopCompartments,
            BuildBoxCache,
            FillShelves,
            ConsolidateBoxes,
            RefreshWarehouseUI,
            Done,
            Cancelled
        }

        internal Phase CurrentPhase { get; private set; } = Phase.None;

        private readonly float _startedRealtime;
        private readonly List<ShelfCompartment> _candidates = new();
        private int _scanIndex;

        // Targets
        internal readonly Dictionary<EItemType, List<ShelfCompartment>> TargetsByType = new();
        internal int TargetCompartmentCount;

        // Boxes
        internal Dictionary<EItemType, List<InteractablePackagingBox_Item>> BoxCache = new();
        internal Dictionary<EItemType, List<ShelfCompartment>> StoredBoxCache = new();
        private int _storedIndexForType;
        private AutoWarehouseRefillSource? _autoWarehouse;

        // Fill iteration state
        private readonly List<EItemType> _typeKeys = new();
        private int _typeIndex;
        private int _compIndexForType;
        private int _boxIndexForType;

        internal int TotalPlaced;
        internal int TotalConsumed;

        private int _scannedSinceLastLog;

        internal RefillSession()
        {
            _startedRealtime = Time.realtimeSinceStartup;
        }

        internal void Cancel()
        {
            CurrentPhase = Phase.Cancelled;
        }

        internal void Tick()
        {
            if (CurrentPhase == Phase.Cancelled || CurrentPhase == Phase.Done)
                return;

            if (CurrentPhase == Phase.None)
                CurrentPhase = Phase.GatherCandidates;

            switch (CurrentPhase)
            {
                case Phase.GatherCandidates:
                    GatherCandidates();
                    if (CurrentPhase != Phase.Done)
                        CurrentPhase = Phase.ScanShopCompartments;
                    break;

                case Phase.ScanShopCompartments:
                    if (ScanShopCompartmentsChunk())
                    {
                        if (TargetCompartmentCount == 0)
                        {
                            SMLog.Info("[ShopManager] No refill needed, proceeding to consolidation.");
                            CurrentPhase = Phase.BuildBoxCache;
                            break;
                        }
                        CurrentPhase = Phase.BuildBoxCache;
                    }
                    break;

                case Phase.BuildBoxCache:
                    BoxCache = WarehouseBoxOps.BuildBoxCache(includeStored: true);
                    StoredBoxCache = WarehouseBoxOps.BuildStoredBoxCache();
                    _autoWarehouse = TargetsByType.Count == 0 ? null : AutoWarehouseRefillSource.Capture();

                    _typeKeys.Clear();
                    foreach (var k in TargetsByType.Keys) _typeKeys.Add(k);

                    _loggedConsolidationStart = false;
                    _typeIndex = 0;
                    _compIndexForType = 0;
                    _boxIndexForType = 0;
                    _storedIndexForType = 0;
                    CurrentPhase = Phase.FillShelves;
                    break;

                case Phase.FillShelves:
                    if (FillShelvesChunk())
                        CurrentPhase = Phase.ConsolidateBoxes;
                    break;

                case Phase.ConsolidateBoxes:
                    if (!_loggedConsolidationStart)
                    {
                        SMLog.Info("[ShopManager] Starting box consolidation...");
                        _loggedConsolidationStart = true;
                    }

                    if (WarehouseBoxOps.ConsolidateBoxesStep(BoxCache, 5))
                    {
                        SMLog.Info("[ShopManager] Consolidation complete.");
                        CurrentPhase = Phase.RefreshWarehouseUI;
                    }
                    break;

                case Phase.RefreshWarehouseUI:
                    WarehouseBoxOps.FlushDeferredDestroys();

                    // Hard prune immediately after destruction so RestockManager stops spamming Update NREs
                    try
                    {
                        var rm = UnityEngine.Object.FindObjectOfType<RestockManager>();
                        if (rm != null)
                            ShopManager.Patches.RestockManagerSanitizer.PruneLists(rm);
                    }
                    catch { /* ignore */ }

                    WarehouseBoxOps.RefreshAllWarehouseBoxSlotLabels();

                    CurrentPhase = Phase.Done;
                    SMLog.Info($"[ShopManager] Refill complete. Placed={TotalPlaced} Consumed={TotalConsumed}. Took {(Time.realtimeSinceStartup - _startedRealtime):0.00}s");
                    break;
            }
        }

        private void GatherCandidates()
        {
            _candidates.Clear();
            TargetsByType.Clear();
            TargetCompartmentCount = 0;
            _scanIndex = 0;
            _scannedSinceLastLog = 0;

            // This is still one call, but it only *collects refs*. The expensive per-compartment work is chunked next.
            _candidates.AddRange(Finders.FindAllShopItemCompartments());

            if (_candidates.Count == 0)
            {
                SMLog.Warn("[ShopManager] No shop compartments found.");
                CurrentPhase = Phase.Done;
                return;
            }

            SMLog.Info($"[ShopManager] Candidate shop compartments: {_candidates.Count} (begin chunked scan)");
        }

        private bool ScanShopCompartmentsChunk()
        {
            int perFrame = Mathf.Max(1, SMConfig.ScanCompartmentsPerFrame.Value);
            int end = Math.Min(_candidates.Count, _scanIndex + perFrame);

            for (; _scanIndex < end; _scanIndex++)
            {
                var c = _candidates[_scanIndex];
                if (c == null)
                {
                    ShelfScanDebug.LogNullCandidate(_scanIndex);
                    continue;
                }

                // Skip inactive / disabled objects (prefabs, unused layouts, etc.)
                if (c.gameObject == null || !c.gameObject.activeInHierarchy)
                {
                    ShelfScanDebug.LogScanDecision(c, "SKIP:inactive", c.GetItemType(), max: -1, cur: -1);
                    continue;
                }

                // Skip any compartment that belongs to a packaging box
                // (held box, floor box, loose box, etc.)
                if (c.GetComponentInParent<InteractablePackagingBox_Item>() != null)
                {
                    ShelfScanDebug.LogScanDecision(c, "SKIP:packaging_box_internal", c.GetItemType(), max: -1, cur: -1, includePath: true);
                    continue;
                }


                // Hard gates (cheap + early)
                var t = c.GetItemType();
                if (t == EItemType.None)
                {
                    ShelfScanDebug.LogScanDecision(c, "SKIP:none_type", t, max: -1, cur: -1);
                    continue;
                }

                int max = c.GetMaxItemCount();
                if (max <= 0)
                {
                    ShelfScanDebug.LogScanDecision(c, "SKIP:max<=0", t, max, cur: -1);
                    continue;
                }

                int cur = ShelfCompartmentOps.GetCurrentUnits(c);

                // THIS is the exact decision point you care about:
                if (cur >= max)
                {
                    ShelfScanDebug.LogScanDecision(c, "SKIP:full(cur>=max)", t, max, cur);
                    continue;
                }

                ShelfScanDebug.LogScanDecision(c, "TARGET:needs_fill", t, max, cur, includePath: true);

                if (!TargetsByType.TryGetValue(t, out var list))
                {
                    list = new List<ShelfCompartment>();
                    TargetsByType[t] = list;
                }

                list.Add(c);
                TargetCompartmentCount++;
                _scannedSinceLastLog++;

                // Progress log
                int every = SMConfig.LogProgressEveryN.Value;
                if (every > 0 && (_scanIndex % every) == 0 && _scanIndex > 0)
                {
                    SMLog.Info($"[ShopManager] Scanning... {_scanIndex}/{_candidates.Count} compartments, targets={TargetCompartmentCount}");
                }
            }

            if (_scanIndex >= _candidates.Count)
            {
                SMLog.Info($"[ShopManager] Scan complete. targets={TargetCompartmentCount} types={TargetsByType.Count}");
                return true;
            }

            return false;
        }

        private bool FillShelvesChunk()
        {
            int placementsBudget = Mathf.Max(1, SMConfig.PlacementsPerFrame.Value);
            int placementsThisTick = 0;

            while (placementsThisTick < placementsBudget)
            {
                if (_typeIndex >= _typeKeys.Count)
                    return true; // all done

                var type = _typeKeys[_typeIndex];

                if (!TargetsByType.TryGetValue(type, out var comps) || comps.Count == 0)
                {
                    _typeIndex++;
                    _compIndexForType = 0;
                    _boxIndexForType = 0;
                    _storedIndexForType = 0;
                    continue;
                }

                BoxCache.TryGetValue(type, out var boxes);
                StoredBoxCache.TryGetValue(type, out var storedSlots);
                bool hasPhysical = boxes != null && boxes.Count > 0;
                bool hasStored = storedSlots != null && storedSlots.Count > 0;
                if (!hasPhysical && !hasStored && _autoWarehouse?.HasProduct(type) != true)
                {
                    _typeIndex++;
                    _compIndexForType = 0;
                    _boxIndexForType = 0;
                    _storedIndexForType = 0;
                    continue;
                }

                // Done with this type’s compartments?
                if (_compIndexForType >= comps.Count)
                {
                    _typeIndex++;
                    _compIndexForType = 0;
                    _boxIndexForType = 0;
                    _storedIndexForType = 0;
                    continue;
                }

                var comp = comps[_compIndexForType];
                if (comp == null) { _compIndexForType++; continue; }

                if (comp.gameObject == null || !comp.gameObject.activeInHierarchy)
                {
                    _compIndexForType++;
                    continue;
                }

                int cur = ShelfCompartmentOps.GetCurrentUnits(comp);
                int max = comp.GetMaxItemCount();
                int need = max - cur;
                if (need <= 0) { _compIndexForType++; continue; }

                // Try place ONE per loop iteration to respect budget
                if (!comp.HasEnoughSlot())
                {
                    _compIndexForType++;
                    continue;
                }

                // Prefer stored records; use the old physical-box path as a fallback.
                bool consumed = false;
                bool fromStored = false;
                ShelfCompartment? sourceSlot = null;
                bool sourceBig = false;

                if (storedSlots != null)
                {
                    while (_storedIndexForType < storedSlots.Count)
                    {
                        var slot = storedSlots[_storedIndexForType];
                        if (slot == null || slot.GetStoredBoxRecordCount() == 0)
                        {
                            _storedIndexForType++;
                            continue;
                        }
                        var record = slot.PeekStoredBoxRecord(slot.GetStoredBoxRecordCount() - 1);
                        if (record.itemType != type || record.amount <= 0)
                        {
                            _storedIndexForType++;
                            continue;
                        }
                        sourceBig = record.isBigBox;
                        if (WarehouseBoxOps.TryConsumeStoredUnit(slot, type))
                        {
                            sourceSlot = slot;
                            consumed = true;
                            fromStored = true;
                            break;
                        }
                        _storedIndexForType++;
                    }
                }

                if (!consumed && boxes != null)
                {
                    while (_boxIndexForType < boxes.Count &&
                           WarehouseBoxOps.GetAvailableUnits(boxes[_boxIndexForType]) <= 0)
                        _boxIndexForType++;
                    while (_boxIndexForType < boxes.Count)
                    {
                        var box = boxes[_boxIndexForType];
                        if (box != null && !box.IsBoxOpened() &&
                            WarehouseBoxOps.TryConsumeOneFromUnopenedBox(box))
                        {
                            consumed = true;
                            break;
                        }
                        _boxIndexForType++;
                    }
                }

                if (!consumed && _autoWarehouse?.TryRefillOne(comp, type) == true)
                {
                    TotalConsumed++;
                    TotalPlaced++;
                    placementsThisTick++;
                    if (ShelfCompartmentOps.GetCurrentUnits(comp) >= comp.GetMaxItemCount())
                        _compIndexForType++;
                    continue;
                }

                if (!consumed)
                {
                    _typeIndex++;
                    _compIndexForType = 0;
                    _boxIndexForType = 0;
                    _storedIndexForType = 0;
                    continue;
                }

                bool placedOne = ItemTransferOps.TrySpawnIntoShelfCompartment(comp, type);
                if (!placedOne)
                {
                    if (fromStored && sourceSlot != null)
                        WarehouseBoxOps.RestoreStoredUnit(sourceSlot, type, sourceBig);
                    else
                        SMLog.Warn($"[ShopManager] Physical box consumed but spawn failed: {type}");
                    SMLog.Warn($"[ShopManager] Spawn failed: {type}");
                    _compIndexForType++;
                    continue;
                }

                if (placedOne)
                {
                    TotalConsumed++;
                    TotalPlaced++;
                    placementsThisTick++;

                    // Don’t spam logs, but keep your old “filled” log concept as optional
                    if (SMConfig.VerboseLogging.Value)
                        SMLog.Debug($"[ShopManager] Placed 1 item for {type} (totalPlaced={TotalPlaced}).");
                }

                // If compartment is now full, move on; otherwise keep filling it next loop if budget remains
                if (ShopManager.GameAccess.ShelfCompartmentOps.GetCurrentUnits(comp) >= comp.GetMaxItemCount())
                    _compIndexForType++;
            }

            return false;

        }
        internal bool IsFinished => CurrentPhase is Phase.Done or Phase.Cancelled;
    }
}
