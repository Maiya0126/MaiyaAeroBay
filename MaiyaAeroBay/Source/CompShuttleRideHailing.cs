using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class CompProperties_ShuttleRideHailing : CompProperties
    {
        public int fareBasePrice = 50;
        public int baseOrderRange = 20;
        public float orderIntervalDays = 2f;

        public CompProperties_ShuttleRideHailing()
        {
            compClass = typeof(CompShuttleRideHailing);
        }
    }

    public class CompShuttleRideHailing : ThingComp
    {
        public CompProperties_ShuttleRideHailing Props => (CompProperties_ShuttleRideHailing)props;
        public bool installed = false;

        private float s_starRating = 1.0f;
        private int s_ordersCompleted = 0;
        private int s_ordersFailed = 0;
        private int s_totalIncome = 0;
        private bool s_isAcceptingRides = false;
        private int s_lastOrderTick = -1;
        private bool debugForceFareDodged = false;
        private bool debugForceBadReview = false;
        private bool debugForceGoodTip = false;
        private bool debugForceLeftBehind = false;

        private int pendingComplaintTick = -1;
        private int pendingComplaintPenaltySilver = 0;
        private float pendingComplaintPenaltyStars = 0f;
        private string pendingComplaintFactionName = "";

        private int pendingFuelDeliveryTick = -1;

        public float StarRating
        {
            get => s_starRating;
            set => s_starRating = Mathf.Clamp(value, 0.5f, 5.0f);
        }
        public int OrdersCompleted => s_ordersCompleted;
        public int OrdersFailed => s_ordersFailed;
        public int TotalIncome => s_totalIncome;
        public bool IsAcceptingRides
        {
            get => s_isAcceptingRides;
            set => s_isAcceptingRides = value;
        }
        public int LastOrderTick => s_lastOrderTick;

        private static Texture2D rideHailingIcon;
        private static Texture2D RideHailingIcon
        {
            get
            {
                if (rideHailingIcon == null)
                    rideHailingIcon = ContentFinder<Texture2D>.Get("UI/Commands/car", false);
                return rideHailingIcon;
            }
        }

        private static Texture2D rideOrderIcon;
        private static Texture2D RideOrderIcon
        {
            get
            {
                if (rideOrderIcon == null)
                    rideOrderIcon = ContentFinder<Texture2D>.Get("UI/Commands/car", false);
                return rideOrderIcon;
            }
        }

        public RideOrder ActiveOrder
        {
            get
            {
                var manager = GetManager();
                if (manager == null) return null;
                return manager.GetActiveOrderByShuttle(parent.ThingID);
            }
        }

        public List<RideOrder> PendingOrders
        {
            get
            {
                var manager = GetManager();
                if (manager == null) return new List<RideOrder>();
                return manager.GetPendingOrdersForShuttle(parent.ThingID, s_starRating);
            }
        }

        private WorldComponent_RideHailingManager GetManager()
        {
            return Find.World?.GetComponent<WorldComponent_RideHailingManager>();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!installed) return;
            CheckArrival();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!installed) return;
            if (Find.TickManager.TicksGame % 60 != 0) return;
            CheckArrival();
            CheckPendingComplaint();
            CheckFuelDelivery();
        }

        private void CheckArrival()
        {
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return;

            var manager = GetManager();
            if (manager == null) return;

            int currentTile = -1;
            if (parent.Map != null)
                currentTile = parent.Map.Tile;

            if (currentTile < 0) return;

            var order = manager.GetActiveOrderByShuttle(parent.ThingID);
            if (order == null)
            {
                order = manager.RecoverShuttleID(parent.ThingID);
                if (order == null) return;
            }

            if (order.state == RideOrderState.Accepted && currentTile == order.pickupTile)
            {
                order.state = RideOrderState.PickedUp;
                manager.UpdatePickupMarkerCompleted(order);
                string detail = order.orderType == RideOrderType.TransportPerson
                    ? "MaiyaAeroBay_RidePickedUpPerson".Translate(order.passengerName, order.dropoffLabel)
                    : "MaiyaAeroBay_RidePickedUpCargo".Translate(order.cargoDef?.label ?? "cargo", order.dropoffLabel);
                Messages.Message("MaiyaAeroBay_RidePickedUp".Translate(detail), parent, MessageTypeDefOf.PositiveEvent);
            }
            else if (order.state == RideOrderState.PickedUp && currentTile == order.dropoffTile)
            {
                CompleteOrder(order, manager);
            }
        }

        public void CompleteOrderFromWorld(RideOrder order, WorldComponent_RideHailingManager manager)
        {
            bool isFareDodged = false;
            bool isBadReview = false;

            if (debugForceFareDodged)
            {
                isFareDodged = true;
                debugForceFareDodged = false;
            }
            else if (debugForceBadReview)
            {
                isBadReview = true;
                debugForceBadReview = false;
            }
            else if (!debugForceGoodTip)
            {
                float starRatingFactor = Mathf.Max(0.3f, 1f - (s_starRating - 1f) * 0.1f);

                if (Rand.Value < 0.08f * starRatingFactor)
                {
                    isFareDodged = true;
                }

                if (!isFareDodged)
                {
                    float badReviewChance = 0.05f * starRatingFactor;
                    if (order.completeDeadlineTick > 0 && Find.TickManager.TicksGame > order.completeDeadlineTick - 60000)
                        badReviewChance *= 2f;
                    if (Rand.Value < badReviewChance)
                    {
                        isBadReview = true;
                    }
                }
            }

            if (isBadReview)
            {
                order.state = RideOrderState.Completed;
                s_ordersCompleted++;
                s_lastOrderTick = Find.TickManager.TicksGame;

                float penaltyStars = order.starReward * 2f;
                StarRating -= penaltyStars;

                int penaltySilver = Mathf.CeilToInt(order.rewardSilver * 0.5f);
                if (penaltySilver > 0)
                    TryDeductSilver(penaltySilver);

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, -3, canSendMessage: true, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Fail);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideBadReviewTitle".Translate(),
                    "MaiyaAeroBay_RideBadReview".Translate(penaltySilver, penaltyStars.ToString("F1"), order.faction?.Name ?? ""),
                    LetterDefOf.NegativeEvent);
                RimTuberCompat.OnBadReview(penaltySilver, s_starRating);
                return;
            }

            if (isFareDodged)
            {
                order.state = RideOrderState.Completed;
                s_ordersCompleted++;
                StarRating += order.starReward;
                s_lastOrderTick = Find.TickManager.TicksGame;

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 1, canSendMessage: false, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Success);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideFareDodgedTitle".Translate(),
                    "MaiyaAeroBay_RideFareDodged".Translate(order.rewardSilver, order.starReward.ToString("F1")),
                    LetterDefOf.NegativeEvent);
                RimTuberCompat.OnFareDodged(order.rewardSilver, s_starRating);
                return;
            }

            order.state = RideOrderState.Completed;
            s_ordersCompleted++;
            s_totalIncome += order.rewardSilver;
            StarRating += order.starReward;
            s_lastOrderTick = Find.TickManager.TicksGame;

            Map map = parent?.Map ?? Find.AnyPlayerHomeMap;
            if (order.rewardSilver > 0 && map != null)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = order.rewardSilver;
                IntVec3 silverPos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(map);
                if (!silverPos.IsValid) silverPos = CellFinder.RandomEdgeCell(map);
                if (!GenPlace.TryPlaceThing(silver, silverPos, map, ThingPlaceMode.Near))
                    GenPlace.TryPlaceThing(silver, CellFinder.RandomEdgeCell(map), map, ThingPlaceMode.Near);
            }

            bool isGoodTip = false;
            if (debugForceGoodTip)
            {
                isGoodTip = true;
                debugForceGoodTip = false;
            }
            else
            {
                float tipChance = 0.10f + (s_starRating - 1f) * 0.03f;
                isGoodTip = Rand.Value < tipChance;
            }

            if (isGoodTip)
            {
                int tipSilver = Mathf.CeilToInt(order.rewardSilver * 0.5f);
                StarRating += order.starReward * 0.3f;
                s_totalIncome += tipSilver;

                if (tipSilver > 0 && map != null)
                {
                    var tip = ThingMaker.MakeThing(ThingDefOf.Silver);
                    tip.stackCount = tipSilver;
                    IntVec3 tipPos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(map);
                    if (!tipPos.IsValid) tipPos = CellFinder.RandomEdgeCell(map);
                    if (!GenPlace.TryPlaceThing(tip, tipPos, map, ThingPlaceMode.Near))
                        GenPlace.TryPlaceThing(tip, CellFinder.RandomEdgeCell(map), map, ThingPlaceMode.Near);
                }

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 5, canSendMessage: true, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Success);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideCompleteLetterTitle".Translate(),
                    "MaiyaAeroBay_RideGoodTip".Translate(order.rewardSilver, tipSilver, (order.starReward + order.starReward * 0.3f).ToString("F1"), order.faction?.Name ?? ""),
                    LetterDefOf.PositiveEvent);
                RimTuberCompat.OnGoodTip(tipSilver, s_starRating);
                return;
            }

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 3, canSendMessage: true, canSendHostilityLetter: false);
            }

            manager.EndQuestForOrder(order, QuestEndOutcome.Success);
            manager.RemoveOrder(order);

            Find.LetterStack.ReceiveLetter(
                "MaiyaAeroBay_RideCompleteLetterTitle".Translate(),
                "MaiyaAeroBay_RideCompleted".Translate(order.rewardSilver, order.starReward.ToString("F1"), order.faction?.Name ?? ""),
                LetterDefOf.PositiveEvent);
            RimTuberCompat.OnOrderCompleted(order.rewardSilver, s_starRating);

            TryTriggerLeftBehind(order);
        }

        public void CompleteOrder(RideOrder order, WorldComponent_RideHailingManager manager)
        {
            Log.Message("[MaiyaAeroBay] CompleteOrder called. Shuttle on map: " + (parent?.Map != null) + ", rewardSilver: " + order.rewardSilver);
            bool isFareDodged = false;
            bool isBadReview = false;

            if (debugForceFareDodged)
            {
                isFareDodged = true;
                debugForceFareDodged = false;
            }
            else if (debugForceBadReview)
            {
                isBadReview = true;
                debugForceBadReview = false;
            }
            else if (!debugForceGoodTip)
            {
                float starRatingFactor = Mathf.Max(0.3f, 1f - (s_starRating - 1f) * 0.1f);

                if (Rand.Value < 0.08f * starRatingFactor)
                {
                    isFareDodged = true;
                }

                if (!isFareDodged)
                {
                    float badReviewChance = 0.05f * starRatingFactor;
                    if (order.completeDeadlineTick > 0 && Find.TickManager.TicksGame > order.completeDeadlineTick - 60000)
                        badReviewChance *= 2f;
                    if (Rand.Value < badReviewChance)
                    {
                        isBadReview = true;
                    }
                }
            }

            if (isBadReview)
            {
                order.state = RideOrderState.Completed;
                s_ordersCompleted++;
                s_lastOrderTick = Find.TickManager.TicksGame;

                float penaltyStars = order.starReward * 2f;
                StarRating -= penaltyStars;

                int penaltySilver = Mathf.CeilToInt(order.rewardSilver * 0.5f);
                if (penaltySilver > 0)
                    TryDeductSilver(penaltySilver);

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, -3, canSendMessage: true, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Fail);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideBadReviewTitle".Translate(),
                    "MaiyaAeroBay_RideBadReview".Translate(penaltySilver, penaltyStars.ToString("F1"), order.faction?.Name ?? ""),
                    LetterDefOf.NegativeEvent);
                RimTuberCompat.OnBadReview(penaltySilver, s_starRating);
                return;
            }

            if (isFareDodged)
            {
                order.state = RideOrderState.Completed;
                s_ordersCompleted++;
                StarRating += order.starReward;
                s_lastOrderTick = Find.TickManager.TicksGame;

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 1, canSendMessage: false, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Success);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideFareDodgedTitle".Translate(),
                    "MaiyaAeroBay_RideFareDodged".Translate(order.rewardSilver, order.starReward.ToString("F1")),
                    LetterDefOf.NegativeEvent);
                RimTuberCompat.OnFareDodged(order.rewardSilver, s_starRating);
                return;
            }

            order.state = RideOrderState.Completed;
            s_ordersCompleted++;
            s_totalIncome += order.rewardSilver;
            StarRating += order.starReward;
            s_lastOrderTick = Find.TickManager.TicksGame;

            Map dropMap = parent?.Map ?? Find.AnyPlayerHomeMap;
            if (order.rewardSilver > 0 && dropMap != null)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = order.rewardSilver;
                IntVec3 dropPos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(dropMap);
                if (!dropPos.IsValid)
                {
                    Log.Warning("[MaiyaAeroBay] Invalid dropPos for silver reward, using random edge cell");
                    dropPos = CellFinder.RandomEdgeCell(dropMap);
                }
                bool placed = GenPlace.TryPlaceThing(silver, dropPos, dropMap, ThingPlaceMode.Near);
                if (!placed)
                {
                    Log.Warning("[MaiyaAeroBay] Failed to place silver reward at " + dropPos + ", trying random edge cell");
                    dropPos = CellFinder.RandomEdgeCell(dropMap);
                    GenPlace.TryPlaceThing(silver, dropPos, dropMap, ThingPlaceMode.Near);
                }
            }

            bool isGoodTip = false;
            if (debugForceGoodTip)
            {
                isGoodTip = true;
                debugForceGoodTip = false;
            }
            else
            {
                float tipChance = 0.10f + (s_starRating - 1f) * 0.03f;
                isGoodTip = Rand.Value < tipChance;
            }

            if (isGoodTip)
            {
                int tipSilver = Mathf.CeilToInt(order.rewardSilver * 0.5f);
                StarRating += order.starReward * 0.3f;
                s_totalIncome += tipSilver;

                if (tipSilver > 0 && dropMap != null)
                {
                    var tip = ThingMaker.MakeThing(ThingDefOf.Silver);
                    tip.stackCount = tipSilver;
                    IntVec3 tipPos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(dropMap);
                    if (!tipPos.IsValid) tipPos = CellFinder.RandomEdgeCell(dropMap);
                    if (!GenPlace.TryPlaceThing(tip, tipPos, dropMap, ThingPlaceMode.Near))
                        GenPlace.TryPlaceThing(tip, CellFinder.RandomEdgeCell(dropMap), dropMap, ThingPlaceMode.Near);
                }

                if (order.faction != null)
                {
                    order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 5, canSendMessage: true, canSendHostilityLetter: false);
                }

                manager.EndQuestForOrder(order, QuestEndOutcome.Success);
                manager.RemoveOrder(order);

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_RideCompleteLetterTitle".Translate(),
                    "MaiyaAeroBay_RideGoodTip".Translate(order.rewardSilver, tipSilver, (order.starReward + order.starReward * 0.3f).ToString("F1"), order.faction?.Name ?? ""),
                    LetterDefOf.PositiveEvent);
                RimTuberCompat.OnGoodTip(tipSilver, s_starRating);
                return;
            }

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 3, canSendMessage: true, canSendHostilityLetter: false);
            }

            manager.EndQuestForOrder(order, QuestEndOutcome.Success);
            manager.RemoveOrder(order);

            Find.LetterStack.ReceiveLetter(
                "MaiyaAeroBay_RideCompleteLetterTitle".Translate(),
                "MaiyaAeroBay_RideCompleted".Translate(order.rewardSilver, order.starReward.ToString("F1"), order.faction?.Name ?? ""),
                LetterDefOf.PositiveEvent);
            RimTuberCompat.OnOrderCompleted(order.rewardSilver, s_starRating);

            TryTriggerLeftBehind(order);
        }

        public void FailOrder(RideOrder order, WorldComponent_RideHailingManager manager, string reason)
        {
            order.state = RideOrderState.Failed;
            s_ordersFailed++;
            StarRating -= order.starPenalty * Rand.Range(0.8f, 1.2f);

            string failText = order.penaltySilver > 0
                ? "MaiyaAeroBay_RideFailedPenalty".Translate(reason, order.penaltySilver)
                : "MaiyaAeroBay_RideFailed".Translate(reason);

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, -5, canSendMessage: true, canSendHostilityLetter: false);
            }

            manager.RemoveOrder(order);

            Find.LetterStack.ReceiveLetter(
                "MaiyaAeroBay_RideFailLetterTitle".Translate(),
                failText,
                LetterDefOf.NegativeEvent);
        }

        public void CancelOrder(RideOrder order, WorldComponent_RideHailingManager manager)
        {
            float penalty = order.dispatchMode == RideDispatchMode.Mandatory ? order.starPenalty : order.starPenalty * 0.5f;
            StarRating -= penalty * Rand.Range(0.8f, 1.2f);

            if (order.penaltySilver > 0 && order.dispatchMode == RideDispatchMode.Mandatory)
            {
                TryDeductSilver(order.penaltySilver);
            }

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, -2, canSendMessage: true, canSendHostilityLetter: false);
            }

            order.state = RideOrderState.Cancelled;
            manager.RemoveOrder(order);
        }

        private int CountAvailableSilver()
        {
            int total = 0;
            if (parent?.Map != null)
            {
                foreach (var t in parent.Map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                {
                    if (t.Faction == Faction.OfPlayer || t.Faction == null)
                        total += t.stackCount;
                }
            }
            else
            {
                var caravan = GetParentCaravan();
                if (caravan != null)
                {
                    foreach (var t in caravan.AllThings)
                    {
                        if (t.def == ThingDefOf.Silver)
                            total += t.stackCount;
                    }
                }
                else
                {
                    foreach (var map in Find.Maps)
                    {
                        foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                        {
                            if (t.Faction == Faction.OfPlayer || t.Faction == null)
                                total += t.stackCount;
                        }
                    }
                }
            }
            return total;
        }

        private void TryDeductSilver(int amount)
        {
            int remaining = amount;
            if (parent?.Map != null)
            {
                var silverThings = parent.Map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                    .Where(t => t.Faction == Faction.OfPlayer || t.Faction == null)
                    .ToList();
                foreach (var thing in silverThings)
                {
                    if (remaining <= 0) break;
                    int take = Mathf.Min(thing.stackCount, remaining);
                    thing.SplitOff(take).Destroy();
                    remaining -= take;
                }
            }
            else
            {
                var caravan = GetParentCaravan();
                if (caravan != null)
                {
                    foreach (var thing in caravan.AllThings.ToList())
                    {
                        if (remaining <= 0) break;
                        if (thing.def != ThingDefOf.Silver) continue;
                        int take = Mathf.Min(thing.stackCount, remaining);
                        thing.SplitOff(take).Destroy();
                        remaining -= take;
                    }
                }
                else
                {
                    foreach (var map in Find.Maps)
                    {
                        var silverThings = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                            .Where(t => t.Faction == Faction.OfPlayer || t.Faction == null)
                            .ToList();
                        foreach (var thing in silverThings)
                        {
                            if (remaining <= 0) break;
                            int take = Mathf.Min(thing.stackCount, remaining);
                            thing.SplitOff(take).Destroy();
                            remaining -= take;
                        }
                        if (remaining <= 0) break;
                    }
                }
            }
        }

        private Caravan GetParentCaravan()
        {
            if (parent?.Map != null) return null;
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                foreach (var thing in caravan.AllThings)
                {
                    if (thing == parent)
                        return caravan;
                }
            }
            return null;
        }

        public bool CanAcceptNewOrder()
        {
            if (!installed || !s_isAcceptingRides) return false;
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return false;
            return ActiveOrder == null;
        }

        public void AcceptOrder(RideOrder order)
        {
            if (!CanAcceptNewOrder()) return;

            order.state = RideOrderState.Accepted;
            order.assignedShuttleID = parent.ThingID;

            int travelTicks = Mathf.CeilToInt(order.orderType == RideOrderType.TransportPerson ? 1f : 1.5f) * 60000;
            order.completeDeadlineTick = Find.TickManager.TicksGame + travelTicks;

            var manager = GetManager();
            manager?.MoveToActive(order);

            string detail = order.orderType == RideOrderType.TransportPerson
                ? "MaiyaAeroBay_RideAcceptPerson".Translate(order.passengerName, order.pickupLabel, order.dropoffLabel)
                : "MaiyaAeroBay_RideAcceptCargo".Translate(order.cargoCount, order.cargoDef?.label ?? "cargo", order.pickupLabel, order.dropoffLabel);
            Messages.Message("MaiyaAeroBay_RideAccepted".Translate(detail), parent, MessageTypeDefOf.PositiveEvent);

            Find.LetterStack.ReceiveLetter(
                "MaiyaAeroBay_RideLetterTitle".Translate(order.GetOrderTypeLabel()),
                "MaiyaAeroBay_RideLetterText".Translate(detail, order.rewardSilver, order.GetDispatchModeLabel()),
                LetterDefOf.PositiveEvent,
                new LookTargets(parent));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!installed) yield break;
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "MaiyaAeroBay_RideHailingToggle".Translate(),
                defaultDesc = "MaiyaAeroBay_RideHailingToggleDesc".Translate(),
                icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                isActive = () => s_isAcceptingRides,
                toggleAction = () =>
                {
                    s_isAcceptingRides = !s_isAcceptingRides;
                    if (s_isAcceptingRides && ActiveOrder == null)
                    {
                        Messages.Message("MaiyaAeroBay_RideHailingOnline".Translate(), parent, MessageTypeDefOf.PositiveEvent);
                    }
                    else if (!s_isAcceptingRides)
                    {
                        Messages.Message("MaiyaAeroBay_RideHailingOffline".Translate(), parent, MessageTypeDefOf.NeutralEvent);
                    }
                }
            };

            var pending = PendingOrders;
            if (s_isAcceptingRides && (pending.Count > 0 || ActiveOrder != null))
            {
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_RideViewOrders".Translate(),
                    defaultDesc = "MaiyaAeroBay_RideViewOrdersDesc".Translate(),
                    icon = RideOrderIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/CallShuttle", false),
                    action = () => Find.WindowStack.Add(new Dialog_RideOrders(this))
                };
            }

            yield return new Gizmo_RideHailingStatus { rideHailing = this };

            if (installed)
            {
                var refuelable = parent.TryGetComp<CompRefuelable>();
                if (refuelable != null)
                {
                    foreach (var g in GetFuelDeliveryGizmos(refuelable))
                        yield return g;
                }
            }
        }

        public IEnumerable<Gizmo> GetFuelDeliveryGizmos(CompRefuelable refuelable)
        {
            float chemfuelPrice = ThingDef.Named("Chemfuel").BaseMarketValue;
            var icon = ContentFinder<Texture2D>.Get("UI/Commands/RefuelPassengerShuttle", false);

            if (pendingFuelDeliveryTick > 0)
            {
                int ticksLeft = pendingFuelDeliveryTick - Find.TickManager.TicksGame;
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_FuelDeliveryPending".Translate(),
                    defaultDesc = "MaiyaAeroBay_FuelDeliveryPendingDesc".Translate(ticksLeft.ToStringTicksToPeriod()),
                    icon = icon,
                    action = () => { }
                };
            }
            else if (refuelable.Fuel >= refuelable.TargetFuelLevel)
            {
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_FuelDeliveryLabel".Translate(),
                    defaultDesc = "MaiyaAeroBay_FuelDeliveryDescFull".Translate(),
                    icon = icon,
                    action = () => { },
                    Disabled = true,
                    disabledReason = "MaiyaAeroBay_FuelDeliveryDescFull".Translate()
                };
            }
            else
            {
                int fuelNeeded = refuelable.GetFuelCountToFullyRefuel();
                int deliveryCost = Mathf.CeilToInt(fuelNeeded * chemfuelPrice * 1.5f);
                int availableSilver = CountAvailableSilver();
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_FuelDeliveryLabel".Translate(),
                    defaultDesc = "MaiyaAeroBay_FuelDeliveryDesc".Translate(deliveryCost, fuelNeeded) + "\n" + "MaiyaAeroBay_FuelDeliverySilverAvailable".Translate(availableSilver),
                    icon = icon,
                    action = () => OrderFuelDelivery(deliveryCost),
                    Disabled = availableSilver < deliveryCost,
                    disabledReason = "MaiyaAeroBay_FuelDeliveryNoSilver".Translate(deliveryCost, availableSilver)
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!installed) return null;
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return null;

            string text = "MaiyaAeroBay_RideHailingInspect".Translate(
                StarIconString(s_starRating),
                s_starRating.ToString("F1"),
                s_ordersCompleted,
                s_totalIncome);

            var order = ActiveOrder;
            if (order != null)
            {
                text += "\n" + "MaiyaAeroBay_RideHailingActiveOrder".Translate(
                    order.GetOrderTypeLabel(),
                    order.pickupLabel,
                    order.dropoffLabel,
                    order.rewardSilver);
            }
            else if (!s_isAcceptingRides)
            {
                text += "\n" + "MaiyaAeroBay_RideHailingOffline".Translate();
            }

            return text;
        }

        public static string StarIconString(float stars)
        {
            int full = Mathf.FloorToInt(stars);
            bool half = (stars - full) >= 0.5f;
            string s = "";
            for (int i = 0; i < full; i++) s += "\u2605";
            if (half) s += "\u00BD";
            return s;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref installed, "rideHailingInstalled", false);
            Scribe_Values.Look(ref s_starRating, "starRating", 1.0f);
            Scribe_Values.Look(ref s_ordersCompleted, "ordersCompleted", 0);
            Scribe_Values.Look(ref s_ordersFailed, "ordersFailed", 0);
            Scribe_Values.Look(ref s_totalIncome, "totalIncome", 0);
            Scribe_Values.Look(ref s_isAcceptingRides, "isAcceptingRides", false);
            Scribe_Values.Look(ref s_lastOrderTick, "lastOrderTick", -1);
            Scribe_Values.Look(ref pendingComplaintTick, "pendingComplaintTick", -1);
            Scribe_Values.Look(ref pendingComplaintPenaltySilver, "pendingComplaintPenaltySilver", 0);
            Scribe_Values.Look(ref pendingComplaintPenaltyStars, "pendingComplaintPenaltyStars", 0f);
            Scribe_Values.Look(ref pendingComplaintFactionName, "pendingComplaintFactionName", "");
            Scribe_Values.Look(ref pendingFuelDeliveryTick, "pendingFuelDeliveryTick", -1);
        }

        private void OrderFuelDelivery(int cost)
        {
            int available = CountAvailableSilver();
            if (available < cost)
            {
                Messages.Message("MaiyaAeroBay_FuelDeliveryNoSilver".Translate(cost, available), parent, MessageTypeDefOf.RejectInput);
                return;
            }

            TryDeductSilver(cost);
            pendingFuelDeliveryTick = Find.TickManager.TicksGame + 2500;
            Messages.Message("MaiyaAeroBay_FuelDeliveryOrdered".Translate(), parent, MessageTypeDefOf.PositiveEvent);
        }

        private void CheckFuelDelivery()
        {
            if (pendingFuelDeliveryTick < 0) return;
            if (Find.TickManager.TicksGame < pendingFuelDeliveryTick) return;

            var refuelable = parent?.TryGetComp<CompRefuelable>();
            if (refuelable != null)
            {
                float toAdd = refuelable.TargetFuelLevel - refuelable.Fuel;
                if (toAdd > 0) refuelable.Refuel(toAdd);
            }

            pendingFuelDeliveryTick = -1;
            Messages.Message("MaiyaAeroBay_FuelDeliveryArrived".Translate(), parent, MessageTypeDefOf.PositiveEvent);
        }

        internal void RebuildProps()
        {
            var newProps = new CompProperties_ShuttleRideHailing();
            props = newProps;
        }

        internal void SavePropsToFields()
        {
            installed = true;
            s_isAcceptingRides = true;
        }

        internal void SetDebugForceFareDodged()
        {
            debugForceFareDodged = true;
        }

        internal void SetDebugForceBadReview()
        {
            debugForceBadReview = true;
        }

        internal void SetDebugForceGoodTip()
        {
            debugForceGoodTip = true;
        }

        internal void SetDebugForceLeftBehind()
        {
            var item = GenerateLeftBehindItem();
            if (item != null)
            {
                var order = ActiveOrder;
                Find.WindowStack.Add(new Dialog_LeftBehindItem(item, this, order?.faction?.Name ?? ""));
            }
            else
            {
                Messages.Message("[Debug] No valid left-behind item to generate", MessageTypeDefOf.RejectInput);
            }
        }

        private void CheckPendingComplaint()
        {
            if (pendingComplaintTick < 0) return;
            if (Find.TickManager.TicksGame < pendingComplaintTick) return;

            StarRating -= pendingComplaintPenaltyStars;
            if (pendingComplaintPenaltySilver > 0)
                TryDeductSilver(pendingComplaintPenaltySilver);

            Find.LetterStack.ReceiveLetter(
                "MaiyaAeroBay_ComplaintTitle".Translate(),
                "MaiyaAeroBay_ComplaintDesc".Translate(pendingComplaintPenaltySilver, s_starRating.ToString("F1"), pendingComplaintFactionName),
                LetterDefOf.NegativeEvent);

            pendingComplaintTick = -1;
            pendingComplaintPenaltySilver = 0;
            pendingComplaintPenaltyStars = 0f;
            pendingComplaintFactionName = "";
        }

        private Thing GenerateLeftBehindItem()
        {
            var candidates = DefDatabase<ThingDef>.AllDefs
                .Where(d => d.tradeability == Tradeability.All || d.tradeability == Tradeability.Sellable)
                .Where(d => d.EverHaulable)
                .Where(d => d.statBases?.Any(s => s.stat == StatDefOf.Mass && s.value <= 5f) == true)
                .Where(d => d.thingClass != null && d.thingClass != typeof(Pawn))
                .ToList();

            if (candidates.Count == 0) return null;

            var def = candidates.RandomElement();
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = def.stackLimit > 1 ? Rand.RangeInclusive(1, Mathf.Min(5, def.stackLimit)) : 1;
            return thing;
        }

        private void TryTriggerLeftBehind(RideOrder order)
        {
            bool shouldTrigger = false;
            if (debugForceLeftBehind)
            {
                shouldTrigger = true;
                debugForceLeftBehind = false;
            }
            else
            {
                float chance = 0.12f + (s_starRating - 1f) * 0.02f;
                shouldTrigger = Rand.Value < chance;
            }

            if (!shouldTrigger) return;

            var item = GenerateLeftBehindItem();
            if (item == null) return;

            if (parent.Map != null)
            {
                Find.WindowStack.Add(new Dialog_LeftBehindItem(item, this, order.faction?.Name ?? ""));
            }
        }

        public void HandleLeftBehindKeep(Thing item)
        {
            Map map = parent?.Map ?? Find.AnyPlayerHomeMap;
            if (map != null)
            {
                IntVec3 pos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(map);
                if (!pos.IsValid) pos = CellFinder.RandomEdgeCell(map);
                GenPlace.TryPlaceThing(item, pos, map, ThingPlaceMode.Near);
            }

            if (Rand.Value < 0.6f)
            {
                pendingComplaintPenaltySilver = Mathf.Max(30, item.MarketValue * item.stackCount > 0 ? (int)item.MarketValue : 30);
                pendingComplaintPenaltyStars = 0.2f;
                pendingComplaintFactionName = item.LabelCap;
                pendingComplaintTick = Find.TickManager.TicksGame + Rand.RangeInclusive(3, 5) * 60000;
            }

            Messages.Message("MaiyaAeroBay_LeftBehindKept".Translate(item.LabelCap), parent, MessageTypeDefOf.NeutralEvent);
            RimTuberCompat.OnLeftBehind(item.LabelCap, true);
        }

        public void HandleLeftBehindReturn(Thing item)
        {
            if (Rand.Value < 0.3f)
            {
                int rewardSilver = Mathf.Max(20, (int)(item.MarketValue * 0.8f));
                StarRating += 0.1f;

                Map map = parent?.Map ?? Find.AnyPlayerHomeMap;
                if (map != null && rewardSilver > 0)
                {
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = rewardSilver;
                    IntVec3 pos = (parent != null && parent.Map != null) ? parent.Position : CellFinder.RandomEdgeCell(map);
                    if (!pos.IsValid) pos = CellFinder.RandomEdgeCell(map);
                    GenPlace.TryPlaceThing(silver, pos, map, ThingPlaceMode.Near);
                }

                Find.LetterStack.ReceiveLetter(
                    "MaiyaAeroBay_ReturnRewardTitle".Translate(),
                    "MaiyaAeroBay_ReturnRewardDesc".Translate(rewardSilver, s_starRating.ToString("F1")),
                    LetterDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("MaiyaAeroBay_LeftBehindReturned".Translate(item.LabelCap), parent, MessageTypeDefOf.PositiveEvent);
            }
            RimTuberCompat.OnLeftBehind(item.LabelCap, false);
        }
    }
}
