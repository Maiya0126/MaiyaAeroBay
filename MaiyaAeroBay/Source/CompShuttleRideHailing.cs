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

            var order = ActiveOrder;
            if (order == null) return;

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

        public void CompleteOrder(RideOrder order, WorldComponent_RideHailingManager manager)
        {
            order.state = RideOrderState.Completed;
            s_ordersCompleted++;
            s_totalIncome += order.rewardSilver;
            StarRating += order.starReward;
            s_lastOrderTick = Find.TickManager.TicksGame;

            if (order.rewardSilver > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = order.rewardSilver;
                GenPlace.TryPlaceThing(silver, parent.Position, parent.Map, ThingPlaceMode.Near);
            }

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, 3, canSendMessage: true, canSendHostilityLetter: false);
            }

            if (order.questID >= 0)
            {
                var quest = Find.QuestManager?.QuestsListForReading?.FirstOrDefault(q => q.id == order.questID);
                if (quest != null && quest.State != QuestState.EndedFailed && quest.State != QuestState.EndedSuccess && quest.State != QuestState.EndedInvalid)
                {
                    try { quest.End(QuestEndOutcome.Success, sendLetter: false, playSound: false); } catch { }
                }
                order.questID = -1;
            }

            manager.RemoveOrder(order);

            Messages.Message("MaiyaAeroBay_RideCompleted".Translate(
                order.rewardSilver, s_starRating.ToString("F1"), order.faction?.Name ?? ""),
                parent, MessageTypeDefOf.PositiveEvent);
        }

        public void FailOrder(RideOrder order, WorldComponent_RideHailingManager manager, string reason)
        {
            order.state = RideOrderState.Failed;
            s_ordersFailed++;
            StarRating -= order.starPenalty;

            if (order.penaltySilver > 0)
            {
                TryDeductSilver(order.penaltySilver);
                Messages.Message("MaiyaAeroBay_RideFailedPenalty".Translate(reason, order.penaltySilver),
                    MessageTypeDefOf.NegativeEvent);
            }
            else
            {
                Messages.Message("MaiyaAeroBay_RideFailed".Translate(reason), MessageTypeDefOf.NegativeEvent);
            }

            if (order.faction != null)
            {
                order.faction.TryAffectGoodwillWith(Faction.OfPlayer, -5, canSendMessage: true, canSendHostilityLetter: false);
            }

            manager.RemoveOrder(order);
        }

        public void CancelOrder(RideOrder order, WorldComponent_RideHailingManager manager)
        {
            float penalty = order.dispatchMode == RideDispatchMode.Mandatory ? order.starPenalty : order.starPenalty * 0.5f;
            StarRating -= penalty;

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

        private void TryDeductSilver(int amount)
        {
            var silverThings = parent.Map?.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Where(t => t.Faction == Faction.OfPlayer)
                .ToList();
            if (silverThings == null) return;
            int remaining = amount;
            foreach (var thing in silverThings)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(thing.stackCount, remaining);
                thing.SplitOff(take).Destroy();
                remaining -= take;
            }
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

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_DebugRideShortPerson".Translate(),
                    defaultDesc = "Debug: generate short-range person order",
                    icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                    action = () => DebugGenerateOrder(RideOrderType.TransportPerson, false)
                };
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_DebugRideLongPerson".Translate(),
                    defaultDesc = "Debug: generate long-range person order",
                    icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                    action = () => DebugGenerateOrder(RideOrderType.TransportPerson, true)
                };
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_DebugRideShortCargo".Translate(),
                    defaultDesc = "Debug: generate short-range cargo order",
                    icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                    action = () => DebugGenerateOrder(RideOrderType.TransportCargo, false)
                };
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_DebugRideLongCargo".Translate(),
                    defaultDesc = "Debug: generate long-range cargo order",
                    icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                    action = () => DebugGenerateOrder(RideOrderType.TransportCargo, true)
                };
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_DebugRideTimeout".Translate(),
                    defaultDesc = "Debug: force current order to timeout",
                    icon = RideHailingIcon ?? ContentFinder<Texture2D>.Get("UI/Commands/car", false),
                    action = DebugForceTimeout
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
        }

        internal void RebuildProps()
        {
            var newProps = new CompProperties_ShuttleRideHailing();
            props = newProps;
        }

        internal void SavePropsToFields()
        {
            installed = true;
        }

        private void DebugGenerateOrder(RideOrderType type, bool longRange)
        {
            var manager = GetManager();
            if (manager == null) return;
            var order = manager.DebugGenerateOrder(this, type, longRange);
            if (order != null)
            {
                Messages.Message("Debug: order generated - " + order.pickupLabel + " → " + order.dropoffLabel, parent, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("Debug: failed to generate order (not enough settlements?)", parent, MessageTypeDefOf.RejectInput);
            }
        }

        private void DebugForceTimeout()
        {
            var order = ActiveOrder;
            if (order == null)
            {
                Messages.Message("Debug: no active order to timeout", parent, MessageTypeDefOf.RejectInput);
                return;
            }
            var manager = GetManager();
            if (manager == null) return;
            FailOrder(order, manager, "MaiyaAeroBay_RideFailedTimeout".Translate());
        }
    }
}
