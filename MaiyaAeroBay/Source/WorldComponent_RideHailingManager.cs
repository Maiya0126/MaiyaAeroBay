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

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (!promptShown && Find.TickManager.TicksGame > 120)
            {
                promptShown = true;
                ShowWelcomeDialog();
            }

            CheckExpiredOrders();

            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return;

            int tick = Find.TickManager.TicksGame;
            if (nextOrderCheckTick < 0 || tick >= nextOrderCheckTick)
            {
                TryGenerateOrders();
                int interval = Mathf.RoundToInt(60000f * 0.5f);
                nextOrderCheckTick = tick + Rand.Range(interval / 2, interval);
            }
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
            return activeOrders.FirstOrDefault(o => o.assignedShuttleID == shuttleID
                && (o.state == RideOrderState.Accepted || o.state == RideOrderState.PickedUp));
        }

        public void MoveToActive(RideOrder order)
        {
            pendingOrders.Remove(order);
            if (!activeOrders.Contains(order))
                activeOrders.Add(order);
        }

        public void RemoveOrder(RideOrder order)
        {
            pendingOrders.Remove(order);
            activeOrders.Remove(order);
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

        private ThingWithComps FindShuttleByID(string thingID)
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
