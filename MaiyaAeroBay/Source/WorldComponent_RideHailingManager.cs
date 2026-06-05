using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class WorldComponent_RideHailingManager : WorldComponent
    {
        private List<RideOrder> pendingOrders = new List<RideOrder>();
        private List<RideOrder> activeOrders = new List<RideOrder>();
        private bool promptShown = false;
        private int nextOrderCheckTick = -1;

        public bool PromptShown => promptShown;

        public WorldComponent_RideHailingManager(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingOrders, "pendingOrders", LookMode.Deep);
            Scribe_Collections.Look(ref activeOrders, "activeOrders", LookMode.Deep);
            Scribe_Values.Look(ref promptShown, "promptShown", false);
            Scribe_Values.Look(ref nextOrderCheckTick, "nextOrderCheckTick", -1);

            if (pendingOrders == null) pendingOrders = new List<RideOrder>();
            if (activeOrders == null) activeOrders = new List<RideOrder>();
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            foreach (var order in activeOrders)
            {
                if ((order.state == RideOrderState.Accepted || order.state == RideOrderState.PickedUp)
                    && order.questID < 0)
                {
                    CreateQuestForOrder(order);
                    CreateMarkersForOrder(order);
                }
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (!promptShown && Find.TickManager.TicksGame > 120)
            {
                promptShown = true;
                ShowWelcomeDialog();
            }

            CheckExpiredOrders();
            CheckWorldArrival();
            MaintainPocketMapSourceMaps();

            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return;

            int tick = Find.TickManager.TicksGame;
            if (nextOrderCheckTick < 0 || tick >= nextOrderCheckTick)
            {
                TryGenerateOrders();
                float intervalDays = MaiyaAeroBayMod.settings.rideHailingOrderIntervalDays;
                int interval = Mathf.RoundToInt(60000f * intervalDays);
                nextOrderCheckTick = tick + Rand.Range(Mathf.Max(interval / 2, 100), Mathf.Max(interval, 200));
            }
        }

        private void MaintainPocketMapSourceMaps()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;

            foreach (PocketMapParent pmp in Find.World.pocketMaps)
            {
                if (!pmp.HasMap || pmp.Map?.generatorDef?.defName != "MaiyaAeroBay_InteriorSpace") continue;
                if (pmp.sourceMap != null && Find.Maps.Contains(pmp.sourceMap)) continue;

                Map fallback = null;
                foreach (Map m in Find.Maps)
                {
                    if (m == pmp.Map) continue;
                    foreach (Thing thing in m.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                    {
                        var interior = thing.TryGetComp<Comp_ShuttleInterior>();
                        if (interior != null && interior.PocketMap == pmp.Map)
                        {
                            fallback = m;
                            break;
                        }
                    }
                    if (fallback != null) break;
                }

                if (fallback == null)
                {
                    foreach (Caravan caravan in Find.WorldObjects.Caravans)
                    {
                        var shuttle = caravan.Shuttle;
                        if (shuttle == null) continue;
                        var interior = shuttle.TryGetComp<Comp_ShuttleInterior>();
                        if (interior != null && interior.PocketMap == pmp.Map)
                        {
                            fallback = Find.AnyPlayerHomeMap;
                            break;
                        }
                    }
                }

                if (fallback == null)
                    fallback = Find.AnyPlayerHomeMap;

                if (fallback != null)
                    pmp.sourceMap = fallback;
            }
        }

        private void CheckWorldArrival()
        {
            if (Find.TickManager.TicksGame % 120 != 0) return;
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return;

            foreach (var order in activeOrders.ToList())
            {
                if (order.state != RideOrderState.Accepted && order.state != RideOrderState.PickedUp) continue;

                int shuttleTile = FindShuttleWorldTile(order.assignedShuttleID);
                if (shuttleTile < 0) continue;

                if (order.state == RideOrderState.Accepted && shuttleTile == order.pickupTile)
                {
                    order.state = RideOrderState.PickedUp;
                    UpdatePickupMarkerCompleted(order);
                    var comp = FindShuttleCompAnywhere(order.assignedShuttleID);
                    string detail = order.orderType == RideOrderType.TransportPerson
                        ? "MaiyaAeroBay_RidePickedUpPerson".Translate(order.passengerName, order.dropoffLabel)
                        : "MaiyaAeroBay_RidePickedUpCargo".Translate(order.cargoDef?.label ?? "cargo", order.dropoffLabel);
                    Messages.Message("MaiyaAeroBay_RidePickedUp".Translate(detail), MessageTypeDefOf.PositiveEvent);
                }
                else if (order.state == RideOrderState.PickedUp && shuttleTile == order.dropoffTile)
                {
                    var comp = FindShuttleCompAnywhere(order.assignedShuttleID);
                    if (comp != null)
                    {
                        comp.CompleteOrder(order, this);
                    }
                    else
                    {
                        order.state = RideOrderState.Completed;
                        EndQuestForOrder(order, QuestEndOutcome.Success);
                        RemoveOrder(order);
                        Messages.Message("MaiyaAeroBay_RideCompletedSimple".Translate(order.rewardSilver),
                            MessageTypeDefOf.PositiveEvent);
                    }
                }
            }
        }

        private int FindShuttleWorldTile(string thingID)
        {
            foreach (var map in Find.Maps)
            {
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    if (thing.ThingID == thingID)
                        return map.Tile;
                }
            }
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                foreach (var thing in caravan.AllThings)
                {
                    if (thing.ThingID == thingID)
                        return caravan.Tile;
                }
            }
            return -1;
        }

        private void ShowWelcomeDialog()
        {
            Find.WindowStack.Add(new Dialog_RideHailingWelcome());
        }

        public void SetPromptShown()
        {
            promptShown = true;
        }

        private void CheckExpiredOrders()
        {
            for (int i = pendingOrders.Count - 1; i >= 0; i--)
            {
                if (pendingOrders[i].IsExpired)
                {
                    pendingOrders.RemoveAt(i);
                }
            }

            for (int i = activeOrders.Count - 1; i >= 0; i--)
            {
                var order = activeOrders[i];
                if (order.IsExpired)
                {
                    var shuttle = FindShuttleByID(order.assignedShuttleID);
                    if (shuttle != null)
                    {
                        var comp = shuttle.TryGetComp<CompShuttleRideHailing>();
                        if (comp != null)
                        {
                            comp.FailOrder(order, this, "MaiyaAeroBay_RideFailedTimeout".Translate());
                            continue;
                        }
                    }
                    CleanupOrderResources(order);
                    activeOrders.RemoveAt(i);
                }
            }
        }

        private void TryGenerateOrders()
        {
            var shuttles = GetAllRideHailingShuttles();
            if (shuttles.Count == 0) return;

            foreach (var shuttle in shuttles)
            {
                var comp = shuttle.TryGetComp<CompShuttleRideHailing>();
                if (comp == null || !comp.CanAcceptNewOrder()) continue;
                if (GetPendingOrdersForShuttle(shuttle.ThingID, comp.StarRating).Count >= 3) continue;

                float chance = Mathf.Lerp(0.1f, 0.8f, (comp.StarRating - 0.5f) / 4.5f);
                if (!Rand.Chance(chance)) continue;

                var order = GenerateOrder(comp);
                if (order != null)
                {
                    pendingOrders.Add(order);
                }
            }
        }

        private RideOrder GenerateOrder(CompShuttleRideHailing comp)
        {
            var settlements = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.HostileTo(Faction.OfPlayer) && s.Faction != Faction.OfPlayer)
                .ToList();

            if (settlements.Count < 2) return null;

            var playerMaps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            if (playerMaps.Count == 0) return null;

            int homeTile = playerMaps[0].Tile;

            Settlement pickup = settlements.RandomElement();
            Settlement dropoff = settlements.Where(s => s != pickup).RandomElement();
            if (dropoff == null) return null;

            float starRating = comp.StarRating;
            int dist = Find.WorldGrid.TraversalDistanceBetween(pickup.Tile, dropoff.Tile);

            float maxRange = Mathf.Lerp(comp.Props.baseOrderRange * 0.5f, comp.Props.baseOrderRange * 2f, (starRating - 0.5f) / 4.5f);
            if (dist > maxRange) return null;

            int homeToPickup = Find.WorldGrid.TraversalDistanceBetween(homeTile, pickup.Tile);
            bool isMandatory = homeToPickup <= comp.Props.baseOrderRange && Rand.Chance(0.3f);

            RideOrderType type = Rand.Chance(0.55f) ? RideOrderType.TransportPerson : RideOrderType.TransportCargo;
            RideDispatchMode dispatch = isMandatory ? RideDispatchMode.Mandatory : RideDispatchMode.Optional;

            float tierMult = Mathf.Lerp(0.5f, 2.5f, (starRating - 0.5f) / 4.5f);
            int baseReward = Mathf.RoundToInt(comp.Props.fareBasePrice * tierMult * (1f + dist * 0.03f));
            int reward = Mathf.RoundToInt(baseReward * Rand.Range(0.8f, 1.3f));
            int penalty = isMandatory ? Mathf.RoundToInt(reward * 0.3f) : 0;

            float starR = Mathf.Lerp(0.1f, 0.3f, dist / maxRange);
            float starP = isMandatory ? 0.5f : 0.15f;

            int acceptTimeout = isMandatory ? -1 : Find.TickManager.TicksGame + Rand.Range(3, 8) * 60000;
            int completeTimeout = -1;

            var order = new RideOrder(
                type, dispatch,
                pickup.Tile, dropoff.Tile,
                pickup.Label, dropoff.Label,
                pickup.Faction,
                reward, penalty,
                starR, starP,
                acceptTimeout, completeTimeout);

            return order;
        }

        public List<RideOrder> GetPendingOrdersForShuttle(string shuttleID, float starRating)
        {
            return pendingOrders.Where(o => o.state == RideOrderState.Pending).ToList();
        }

        public RideOrder GetActiveOrderByShuttle(string shuttleID)
        {
            if (shuttleID == null)
                return activeOrders.FirstOrDefault(o => o.state == RideOrderState.Accepted || o.state == RideOrderState.PickedUp);
            return activeOrders.FirstOrDefault(o => o.assignedShuttleID == shuttleID
                && (o.state == RideOrderState.Accepted || o.state == RideOrderState.PickedUp));
        }

        public RideOrder FindOrderByID(string orderID)
        {
            var order = activeOrders.FirstOrDefault(o => o.orderID == orderID);
            if (order != null) return order;
            return pendingOrders.FirstOrDefault(o => o.orderID == orderID);
        }

        public List<RideOrder> GetAllActiveOrders()
        {
            return activeOrders.Where(o => o.state == RideOrderState.Accepted || o.state == RideOrderState.PickedUp).ToList();
        }

        public RideOrder RecoverShuttleID(string currentThingID)
        {
            foreach (var order in activeOrders)
            {
                if ((order.state == RideOrderState.Accepted || order.state == RideOrderState.PickedUp)
                    && order.assignedShuttleID != currentThingID)
                {
                    var comp = FindShuttleCompAnywhere(currentThingID);
                    if (comp != null)
                    {
                        order.assignedShuttleID = currentThingID;
                        return order;
                    }
                    var oldComp = FindShuttleCompAnywhere(order.assignedShuttleID);
                    if (oldComp == null)
                    {
                        order.assignedShuttleID = currentThingID;
                        return order;
                    }
                }
            }
            return null;
        }

        public CompShuttleRideHailing FindShuttleCompByOrder(RideOrder order)
        {
            return FindShuttleCompAnywhere(order.assignedShuttleID);
        }

        public CompShuttleRideHailing FindShuttleCompAnywhere(string thingID)
        {
            foreach (var map in Find.Maps)
            {
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    if (thing.ThingID == thingID)
                        return thing.TryGetComp<CompShuttleRideHailing>();
                }
            }
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                foreach (var thing in caravan.AllThings)
                {
                    if (thing.ThingID == thingID && thing is ThingWithComps twc)
                        return twc.TryGetComp<CompShuttleRideHailing>();
                }
            }
            return null;
        }

        public void EndQuestForOrder(RideOrder order, QuestEndOutcome outcome)
        {
            if (order.questID >= 0)
            {
                var quest = Find.QuestManager?.QuestsListForReading?.FirstOrDefault(q => q.id == order.questID);
                if (quest != null && quest.State == QuestState.Ongoing)
                {
                    try { quest.End(outcome, sendLetter: false, playSound: false); } catch { }
                }
                order.questID = -1;
            }
        }

        public void MoveToActive(RideOrder order)
        {
            pendingOrders.Remove(order);
            if (!activeOrders.Contains(order))
                activeOrders.Add(order);
            CreateQuestForOrder(order);
            CreateMarkersForOrder(order);
        }

        public void RemoveOrder(RideOrder order)
        {
            CleanupOrderResources(order);
            pendingOrders.Remove(order);
            activeOrders.Remove(order);
        }

        internal void UpdatePickupMarkerCompleted(RideOrder order)
        {
            RemoveMarker(order.pickupMarkerID);
            order.pickupMarkerID = -1;
        }

        private void CreateQuestForOrder(RideOrder order)
        {
            try
            {
                var quest = Quest.MakeRaw();
                quest.name = "MaiyaAeroBay_RideQuestName".Translate(order.GetOrderTypeLabel(), order.pickupLabel, order.dropoffLabel);
                quest.description = "MaiyaAeroBay_RideQuestDescription".Translate(
                    order.GetOrderTypeLabel(), order.pickupLabel, order.dropoffLabel,
                    order.rewardSilver.ToString());

                var part = new QuestPart_RideHailingOrder();
                part.orderID = order.orderID;
                part.pickupTile = order.pickupTile;
                part.dropoffTile = order.dropoffTile;
                part.pickupLabel = order.pickupLabel;
                part.dropoffLabel = order.dropoffLabel;
                part.orderTypeLabel = order.GetOrderTypeLabel();
                part.completeDeadlineTick = order.completeDeadlineTick;
                quest.AddPart(part);

                quest.appearanceTick = Find.TickManager.TicksGame;
                quest.acceptanceTick = Find.TickManager.TicksGame;

                Find.QuestManager.Add(quest);

                order.questID = quest.id;
            }
            catch (System.Exception ex)
            {
                Log.Warning("[MaiyaAeroBay] Failed to create quest for ride order: " + ex.Message);
            }
        }

        private void CreateMarkersForOrder(RideOrder order)
        {
            try
            {
                var pickupMarker = (RideOrderMarker)WorldObjectMaker.MakeWorldObject(
                    DefDatabase<WorldObjectDef>.GetNamed("MaiyaAeroBay_RideOrderMarker"));
                pickupMarker.Initialize(order.orderID, true, order.pickupLabel, order.dropoffLabel, order.GetOrderTypeLabel());
                pickupMarker.Tile = order.pickupTile;
                Find.WorldObjects.Add(pickupMarker);
                order.pickupMarkerID = pickupMarker.ID;

                var dropoffMarker = (RideOrderMarker)WorldObjectMaker.MakeWorldObject(
                    DefDatabase<WorldObjectDef>.GetNamed("MaiyaAeroBay_RideOrderMarker"));
                dropoffMarker.Initialize(order.orderID, false, order.dropoffLabel, order.pickupLabel, order.GetOrderTypeLabel());
                dropoffMarker.Tile = order.dropoffTile;
                Find.WorldObjects.Add(dropoffMarker);
                order.dropoffMarkerID = dropoffMarker.ID;
            }
            catch (System.Exception ex)
            {
                Log.Warning("[MaiyaAeroBay] Failed to create markers for ride order: " + ex.Message);
            }
        }

        private void CleanupOrderResources(RideOrder order)
        {
            RemoveMarker(order.pickupMarkerID);
            RemoveMarker(order.dropoffMarkerID);
            order.pickupMarkerID = -1;
            order.dropoffMarkerID = -1;
            EndQuestForOrder(order, QuestEndOutcome.Fail);
        }

        private void RemoveMarker(int markerID)
        {
            if (markerID < 0) return;
            var obj = Find.WorldObjects.AllWorldObjects.FirstOrDefault(w => w.ID == markerID);
            if (obj != null && !obj.Destroyed)
            {
                Find.WorldObjects.Remove(obj);
            }
        }

        private List<ThingWithComps> GetAllRideHailingShuttles()
        {
            var result = new List<ThingWithComps>();
            foreach (var map in Find.Maps)
            {
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var comp = thing.TryGetComp<CompShuttleRideHailing>();
                    if (comp != null && comp.installed)
                        result.Add(thing as ThingWithComps);
                }
            }
            return result;
        }

        public ThingWithComps FindShuttleByID(string thingID)
        {
            foreach (var map in Find.Maps)
            {
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    if (thing.ThingID == thingID)
                        return thing as ThingWithComps;
                }
            }
            return null;
        }

        public RideOrder DebugGenerateOrder(CompShuttleRideHailing comp, RideOrderType type, bool longRange)
        {
            var settlements = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.HostileTo(Faction.OfPlayer) && s.Faction != Faction.OfPlayer)
                .ToList();

            if (settlements.Count < 2) return null;

            Settlement pickup = settlements.RandomElement();
            Settlement dropoff = settlements.Where(s => s != pickup).RandomElement();
            if (dropoff == null) return null;

            int dist = Find.WorldGrid.TraversalDistanceBetween(pickup.Tile, dropoff.Tile);
            if (!longRange && dist > 15)
            {
                foreach (var s in settlements)
                {
                    foreach (var s2 in settlements)
                    {
                        if (s == s2) continue;
                        int d = Find.WorldGrid.TraversalDistanceBetween(s.Tile, s2.Tile);
                        if (d > 0 && d <= 15)
                        {
                            pickup = s;
                            dropoff = s2;
                            dist = d;
                            break;
                        }
                    }
                    if (dist <= 15) break;
                }
            }

            int baseReward = longRange ? 200 : 80;
            int reward = Mathf.RoundToInt(baseReward * Rand.Range(0.8f, 1.3f));
            int completeTimeout = Find.TickManager.TicksGame + (longRange ? 8 : 3) * 60000;

            var order = new RideOrder(
                type,
                longRange ? RideDispatchMode.Optional : RideDispatchMode.Mandatory,
                pickup.Tile, dropoff.Tile,
                pickup.Label, dropoff.Label,
                pickup.Faction,
                reward, longRange ? 0 : 20,
                longRange ? 0.2f : 0.15f, longRange ? 0.1f : 0.3f,
                -1, completeTimeout);

            pendingOrders.Add(order);
            return order;
        }
    }
}
