using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class Dialog_RequestTransport : Window
    {
        private Map sourceMap;
        private Caravan sourceCaravan;
        private int sourceTile;
        private int homeTile;
        private int tileDistance;
        private List<ThingItem> allItems = new List<ThingItem>();
        private Vector2 scrollPosition;
        private float cachedTotalWeight;
        private bool weightDirty = true;

        private struct ThingItem
        {
            public ThingDef def;
            public int count;
            public float massPerItem;
            public string label;
            public bool selected;
        }

        public override Vector2 InitialSize => new Vector2(560f, 640f);

        private bool FromMap => sourceMap != null;
        private bool FromCaravan => sourceCaravan != null;

        public Dialog_RequestTransport(Map map, int tile)
        {
            sourceMap = map;
            sourceTile = tile;
            Setup();
        }

        public Dialog_RequestTransport(Caravan caravan)
        {
            sourceCaravan = caravan;
            sourceTile = caravan.Tile;
            Setup();
        }

        private void Setup()
        {
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;

            homeTile = FindClosestHomeTile();
            tileDistance = homeTile >= 0 && sourceTile >= 0
                ? Find.WorldGrid.TraversalDistanceBetween(sourceTile, homeTile)
                : 0;

            if (FromMap)
            {
                foreach (var thing in sourceMap.listerThings.AllThings)
                {
                    if (!IsTransportable(thing)) continue;
                    AddOrMerge(thing);
                }
            }
            else if (FromCaravan)
            {
                foreach (var thing in sourceCaravan.AllThings)
                {
                    if (!IsTransportable(thing)) continue;
                    AddOrMerge(thing);
                }
            }

            allItems.Sort((a, b) => a.label.CompareTo(b.label));
            ComputeTotalWeight();
        }

        private int FindClosestHomeTile()
        {
            int bestTile = -1;
            int bestDist = int.MaxValue;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                int dist = Find.WorldGrid.TraversalDistanceBetween(sourceTile, map.Tile);
                if (dist >= 0 && dist < bestDist)
                {
                    bestDist = dist;
                    bestTile = map.Tile;
                }
            }
            return bestTile;
        }

        private static bool IsTransportable(Thing t)
        {
            if (t.def.category == ThingCategory.Pawn) return false;
            if (t.def.category == ThingCategory.Filth) return false;
            if (t.def.IsBlueprint || t.def.IsFrame) return false;
            if (!t.def.EverHaulable) return false;
            if (t.def == ThingDefOf.Silver) return false;
            if (t.Faction != null && t.Faction != Faction.OfPlayer) return false;
            return true;
        }

        private void AddOrMerge(Thing t)
        {
            int idx = allItems.FindIndex(i => i.def == t.def);
            if (idx >= 0)
            {
                var item = allItems[idx];
                item.count += t.stackCount;
                allItems[idx] = item;
            }
            else
            {
                allItems.Add(new ThingItem
                {
                    def = t.def,
                    count = t.stackCount,
                    massPerItem = t.GetStatValue(StatDefOf.Mass, true),
                    label = t.LabelCap,
                    selected = false
                });
            }
        }

        private void ComputeTotalWeight()
        {
            cachedTotalWeight = 0f;
            foreach (var item in allItems)
            {
                if (item.selected)
                    cachedTotalWeight += item.massPerItem * item.count;
            }
            weightDirty = false;
        }

        private int TotalCost => Mathf.CeilToInt(tileDistance * 10f + cachedTotalWeight * 0.5f);

        private static int AvailableSilver()
        {
            int total = 0;
            foreach (var map in Find.Maps)
            {
                foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                {
                    if (t.Faction == Faction.OfPlayer || t.Faction == null)
                        total += t.stackCount;
                }
            }
            return total;
        }

        private static void DeductSilver(int amount)
        {
            int remaining = amount;
            foreach (var map in Find.Maps)
            {
                var list = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                    .Where(t => t.Faction == Faction.OfPlayer || t.Faction == null)
                    .ToList();
                foreach (var thing in list)
                {
                    if (remaining <= 0) return;
                    int take = Mathf.Min(thing.stackCount, remaining);
                    thing.SplitOff(take).Destroy();
                    remaining -= take;
                }
            }
        }

        private List<Thing> CollectSelectedItems()
        {
            var result = new List<Thing>();
            if (FromMap)
            {
                foreach (var item in allItems)
                {
                    if (!item.selected) continue;
                    int needed = item.count;
                    foreach (var thing in sourceMap.listerThings.AllThings)
                    {
                        if (needed <= 0) break;
                        if (thing.def != item.def) continue;
                        if (thing.Faction != null && thing.Faction != Faction.OfPlayer) continue;
                        int take = Mathf.Min(thing.stackCount, needed);
                        thing.SplitOff(take).Destroy();
                        needed -= take;
                    }
                    var fresh = ThingMaker.MakeThing(item.def);
                    fresh.stackCount = item.count;
                    result.Add(fresh);
                }
            }
            else if (FromCaravan)
            {
                foreach (var item in allItems)
                {
                    if (!item.selected) continue;
                    int needed = item.count;
                    foreach (var thing in sourceCaravan.AllThings.ToList())
                    {
                        if (needed <= 0) break;
                        if (thing.def != item.def) continue;
                        int take = Mathf.Min(thing.stackCount, needed);
                        thing.SplitOff(take).Destroy();
                        needed -= take;
                    }
                    var fresh = ThingMaker.MakeThing(item.def);
                    fresh.stackCount = item.count;
                    result.Add(fresh);
                }
            }
            return result;
        }

        private void DoConfirm()
        {
            int cost = TotalCost;
            if (cost <= 0) return;
            int available = AvailableSilver();
            if (available < cost)
            {
                Messages.Message("MaiyaAeroBay_RequestDeliveryNoSilver".Translate(cost, available), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var items = CollectSelectedItems();
            if (items.Count == 0) return;

            DeductSilver(cost);

            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            manager?.StartPlayerDelivery(sourceTile, homeTile, tileDistance, FromMap ? sourceMap : null, items);

            Messages.Message("MaiyaAeroBay_RequestDeliveryOrdered".Translate(cost, tileDistance.ToString(), homeTile), MessageTypeDefOf.PositiveEvent);
            Close();
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "MaiyaAeroBay_RequestDeliveryTitle".Translate());
            Text.Font = GameFont.Small;
            y += 35f;

            string locDesc = FromMap ? sourceMap.Parent.Label : (FromCaravan ? sourceCaravan.Label : "?");
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f),
                "MaiyaAeroBay_RequestDeliverySubtitle".Translate(locDesc, tileDistance));
            y += 24f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width * 0.6f, 22f),
                "MaiyaAeroBay_RequestDeliveryCost".Translate(TotalCost, cachedTotalWeight.ToString("F1")));
            int silver = AvailableSilver();
            Widgets.Label(new Rect(inRect.x + inRect.width * 0.6f, y, inRect.width * 0.4f, 22f),
                "MaiyaAeroBay_RequestDeliverySilver".Translate(silver));
            y += 26f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 10f;

            float scrollHeight = allItems.Count * 30f;
            Rect scrollRect = new Rect(inRect.x, y, inRect.width, Mathf.Min(scrollHeight + 10f, 420f));
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, scrollHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float listY = 0f;
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                Rect rowRect = new Rect(0f, listY, viewRect.width, 28f);

                if (i % 2 == 0) Widgets.DrawHighlight(rowRect);

                bool wasSel = item.selected;
                Widgets.CheckboxLabeled(rowRect, item.label + " x" + item.count
                    + " (" + (item.massPerItem * item.count).ToString("F1") + "kg)", ref item.selected);
                allItems[i] = item;

                if (item.selected != wasSel) weightDirty = true;
                listY += 30f;
            }
            Widgets.EndScrollView();
            y = scrollRect.yMax + 10f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 10f;

            if (weightDirty) ComputeTotalWeight();

            bool hasSelection = allItems.Any(i => i.selected);
            int cost = TotalCost;
            bool canAfford = silver >= cost;

            Rect confirmRect = new Rect(inRect.x + inRect.width - 160f, y, 160f, 36f);
            if (hasSelection && canAfford && Widgets.ButtonText(confirmRect, "MaiyaAeroBay_RequestDeliveryConfirm".Translate()))
                DoConfirm();

            if (!hasSelection)
                Widgets.Label(new Rect(inRect.x, y + 8f, inRect.width - 170f, 22f),
                    "MaiyaAeroBay_RequestDeliveryNoSelection".Translate().Colorize(Color.grey));
            else if (!canAfford)
                Widgets.Label(new Rect(inRect.x, y + 8f, inRect.width - 170f, 22f),
                    "MaiyaAeroBay_RequestDeliveryNoSilver".Translate(cost, silver).Colorize(Color.red));

            if (Widgets.ButtonText(new Rect(inRect.x, y, 140f, 36f), "MaiyaAeroBay_RequestDeliverySelectAll".Translate()))
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    var item = allItems[i];
                    item.selected = true;
                    allItems[i] = item;
                }
                weightDirty = true;
            }
        }
    }
}
