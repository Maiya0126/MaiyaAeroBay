using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class Dialog_TransferItems : Window
    {
        private readonly Comp_ShuttleInterior interior;
        private bool isLoadMode = true;
        private readonly List<Thing> items = new List<Thing>();
        private readonly HashSet<Thing> selected = new HashSet<Thing>();
        private Vector2 scrollPosition;
        private string searchQuery = "";
        private readonly HashSet<ThingDef> expandedCategories = new HashSet<ThingDef>();

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public Dialog_TransferItems(Comp_ShuttleInterior interior)
        {
            this.interior = interior;
            forcePause = true;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;

            CollectItems();
        }

        private void SwitchMode(bool loadMode)
        {
            if (isLoadMode == loadMode) return;
            isLoadMode = loadMode;
            selected.Clear();
            expandedCategories.Clear();
            CollectItems();
        }

        private void CollectItems()
        {
            items.Clear();
            Map sourceMap = isLoadMode ? interior.ParentMap : interior.PocketMap;
            if (sourceMap == null) return;

            foreach (var thing in sourceMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (thing == null) continue;
                if (IsExcluded(thing)) continue;
                items.Add(thing);
            }
        }

        private bool IsExcluded(Thing thing)
        {
            if (!thing.Spawned) return true;
            if (thing.IsForbidden(Faction.OfPlayer)) return true;
            if (thing is Pawn) return true;
            if (thing.def.defName.StartsWith("MaiyaAeroBay_")) return true;
            if (thing is Building) return true;
            if (thing.def.category == ThingCategory.Filth) return true;
            if (thing.def.IsFrame) return true;
            if (thing.def.destroyOnDrop) return true;
            return false;
        }

        private bool MatchesSearch(Thing item)
        {
            if (string.IsNullOrEmpty(searchQuery)) return true;
            return item.LabelNoCount.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0
                || item.def.defName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "MaiyaAeroBay_TransferItemsTitle".Translate());
            Text.Font = GameFont.Small;
            y += 35f;

            float tabWidth = (inRect.width - 4f) / 2f;
            Rect loadTabRect = new Rect(inRect.x, y, tabWidth, 28f);
            Rect unloadTabRect = new Rect(inRect.x + tabWidth + 4f, y, tabWidth, 28f);

            DrawTabButton(loadTabRect, "MaiyaAeroBay_LoadTab".Translate(), true);
            DrawTabButton(unloadTabRect, "MaiyaAeroBay_UnloadTab".Translate(), false);
            y += 32f;

            Rect searchRect = new Rect(inRect.x, y, inRect.width - 10f, 25f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);
            y += 30f;

            Rect selectAllRect = new Rect(inRect.x, y, 120f, 25f);
            if (Widgets.ButtonText(selectAllRect, "MaiyaAeroBay_SelectAll".Translate()))
            {
                foreach (var item in items)
                    if (MatchesSearch(item))
                        selected.Add(item);
            }
            Rect deselectAllRect = new Rect(inRect.x + 125f, y, 120f, 25f);
            if (Widgets.ButtonText(deselectAllRect, "MaiyaAeroBay_DeselectAll".Translate()))
            {
                selected.Clear();
            }
            y += 30f;

            var visibleItems = items.Where(MatchesSearch).ToList();
            string countText = "MaiyaAeroBay_SelectedCount".Translate(selected.Count, visibleItems.Count);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f), countText);
            y += 25f;

            float listHeight = inRect.yMax - y - 40f;
            if (listHeight < 50f) listHeight = 50f;

            Rect listRect = new Rect(inRect.x, y, inRect.width - 16f, listHeight);
            float contentHeight = CalculateListHeight(visibleItems);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float itemY = 0f;

            var groupedItems = visibleItems
                .GroupBy(i => i.def)
                .OrderByDescending(g => g.Sum(i => i.stackCount))
                .ToList();

            foreach (var group in groupedItems)
            {
                var groupItems = group.ToList();
                bool isExpanded = expandedCategories.Contains(group.Key);

                Rect headerRect = new Rect(0f, itemY, viewRect.width, 24f);
                if (Widgets.ButtonText(headerRect, ""))
                {
                    if (isExpanded)
                        expandedCategories.Remove(group.Key);
                    else
                        expandedCategories.Add(group.Key);
                }

                bool allSelected = groupItems.All(i => selected.Contains(i));
                bool newAllSelected = allSelected;
                Widgets.Checkbox(new Vector2(4f, itemY + 2f), ref newAllSelected, 20f);
                if (newAllSelected != allSelected)
                {
                    if (newAllSelected)
                        foreach (var i in groupItems) selected.Add(i);
                    else
                        foreach (var i in groupItems) selected.Remove(i);
                }

                Texture2D icon = group.Key.uiIcon;
                if (icon != null)
                {
                    Rect iconRect = new Rect(26f, itemY + 2f, 20f, 20f);
                    GUI.DrawTexture(iconRect, icon);
                }

                string groupLabel = $"  {group.Key.LabelCap} x{group.Sum(i => i.stackCount)}";
                groupLabel += isExpanded ? "  ▼" : "  ▶";
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(48f, itemY, viewRect.width - 52f, 24f), groupLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                itemY += 24f;

                if (isExpanded)
                {
                    foreach (var item in groupItems)
                    {
                        bool isSelected = selected.Contains(item);
                        bool newSelected = isSelected;
                        Widgets.Checkbox(new Vector2(24f, itemY + 1f), ref newSelected, 18f);
                        if (newSelected != isSelected)
                        {
                            if (newSelected) selected.Add(item);
                            else selected.Remove(item);
                        }

                        Color prevColor = GUI.color;
                        if (!selected.Contains(item))
                            GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        Text.Font = GameFont.Tiny;
                        Widgets.Label(new Rect(44f, itemY, viewRect.width - 48f, 22f), "  " + item.Label);
                        Text.Font = GameFont.Small;
                        GUI.color = prevColor;
                        itemY += 22f;
                    }
                }
            }

            Widgets.EndScrollView();

            float buttonY = inRect.yMax - 35f;
            Rect transferBtnRect = new Rect(inRect.x, buttonY, inRect.width, 30f);
            string btnLabel = isLoadMode
                ? "MaiyaAeroBay_LoadItemsBtn".Translate(selected.Count)
                : "MaiyaAeroBay_UnloadItemsBtn".Translate(selected.Count);

            if (selected.Count > 0)
            {
                if (Widgets.ButtonText(transferBtnRect, btnLabel))
                {
                    ExecuteTransfer();
                    Close();
                }
            }
            else
            {
                Widgets.ButtonText(transferBtnRect, btnLabel, false, false, false);
            }
        }

        private void DrawTabButton(Rect rect, string label, bool isLoad)
        {
            bool active = (isLoadMode == isLoad);
            Color prevBg = GUI.color;
            if (active)
                GUI.color = new Color(0.2f, 0.5f, 0.8f);
            else
                GUI.color = new Color(0.3f, 0.3f, 0.3f);

            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(rect))
                SwitchMode(isLoad);

            GUI.color = prevBg;
        }

        private float CalculateListHeight(List<Thing> visibleItems)
        {
            float height = 0f;
            var groupedItems = visibleItems.GroupBy(i => i.def).ToList();
            foreach (var group in groupedItems)
            {
                height += 24f;
                if (expandedCategories.Contains(group.Key))
                    height += group.Count() * 22f;
            }
            return Mathf.Max(height, 50f);
        }

        private void ExecuteTransfer()
        {
            Map targetMap = isLoadMode ? interior.PocketMap : interior.ParentMap;
            if (targetMap == null) return;

            int transferredCount = 0;
            var toTransfer = selected.Where(i => i.Spawned && !i.Destroyed).ToList();

            foreach (var item in toTransfer)
            {
                IntVec3 targetCell;

                if (isLoadMode)
                {
                    targetCell = FindBestStorageCell(item, targetMap);
                    if (!targetCell.IsValid)
                        targetCell = FindEmptyCellNearExit(targetMap);
                    if (!targetCell.IsValid) continue;
                }
                else
                {
                    targetCell = IntVec3.Invalid;
                    foreach (var c in GenRadial.RadialCellsAround(interior.parent.Position, 3f, true))
                    {
                        if (c.InBounds(targetMap) && c.Walkable(targetMap) && !c.Fogged(targetMap)
                            && !targetMap.thingGrid.ThingsAt(c).Any(t => t.def.category == ThingCategory.Item))
                        {
                            targetCell = c;
                            break;
                        }
                    }
                    if (!targetCell.IsValid)
                        targetCell = CellFinder.RandomSpawnCellForPawnNear(interior.parent.Position, targetMap, 3);
                    if (!targetCell.IsValid) continue;
                }

                item.DeSpawn();
                GenSpawn.Spawn(item, targetCell, targetMap);
                transferredCount++;
            }

            if (transferredCount > 0)
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                string msg = isLoadMode
                    ? "MaiyaAeroBay_LoadItemsSuccess".Translate(transferredCount)
                    : "MaiyaAeroBay_UnloadItemsSuccess".Translate(transferredCount);
                Messages.Message(msg, interior.parent, MessageTypeDefOf.PositiveEvent);
            }
        }

        private IntVec3 FindBestStorageCell(Thing item, Map targetMap)
        {
            IntVec3 result = IntVec3.Invalid;
            float priority = float.MinValue;

            foreach (var cell in targetMap.AllCells)
            {
                if (!cell.InBounds(targetMap)) continue;
                if (!cell.Walkable(targetMap)) continue;

                var slotGroup = targetMap.haulDestinationManager.SlotGroupAt(cell);
                if (slotGroup == null) continue;

                var storeSettings = slotGroup.Settings;
                if (storeSettings != null && !storeSettings.AllowedToAccept(item)) continue;

                var thingsAtCell = targetMap.thingGrid.ThingsAt(cell);
                var existingStack = thingsAtCell.FirstOrDefault(t => t.def == item.def && t.stackCount < t.def.stackLimit);
                if (existingStack != null)
                    return cell;

                if (!thingsAtCell.Any(t => t.def.category == ThingCategory.Item))
                {
                    float cellPriority = slotGroup.parent is Building_Storage ? 1f : 0f;
                    if (cellPriority > priority)
                    {
                        priority = cellPriority;
                        result = cell;
                    }
                }
            }

            return result;
        }

        private IntVec3 FindEmptyCellNearExit(Map targetMap)
        {
            if (interior.exit != null && interior.exit.Spawned)
                return CellFinder.RandomSpawnCellForPawnNear(interior.exit.Position, targetMap, 2);

            var centerCell = new IntVec3(targetMap.Size.x / 2, 0, targetMap.Size.z / 2);
            return CellFinder.RandomSpawnCellForPawnNear(centerCell, targetMap, 3);
        }
    }
}
