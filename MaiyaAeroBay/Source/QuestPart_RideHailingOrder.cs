using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MaiyaAeroBay
{
    public class QuestPart_RideHailingOrder : QuestPart
    {
        public string orderID = "";
        public int pickupTile = -1;
        public int dropoffTile = -1;
        public string pickupLabel = "";
        public string dropoffLabel = "";
        public string orderTypeLabel = "";
        public int rewardSilver = 0;
        public string cargoInfo = "";
        public int completeDeadlineTick = -1;
        public RideOrderType orderType = RideOrderType.TransportPerson;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (var t in base.QuestLookTargets)
                    yield return t;
                if (pickupTile >= 0)
                    yield return new GlobalTargetInfo(pickupTile);
                if (dropoffTile >= 0)
                    yield return new GlobalTargetInfo(dropoffTile);
            }
        }

        public override string DescriptionPart
        {
            get
            {
                string stateStr = FindRideOrderState();
                string timeLeft = "";
                if (completeDeadlineTick > 0)
                {
                    int ticks = completeDeadlineTick - Find.TickManager.TicksGame;
                    if (ticks > 0)
                        timeLeft = ticks.ToStringTicksToPeriod();
                    else
                        timeLeft = "MaiyaAeroBay_TimeOverdue".Translate();
                }

                var sb = new StringBuilder();
                sb.Append(orderTypeLabel).Append(": ").Append(pickupLabel).Append(" → ").Append(dropoffLabel);
                if (!string.IsNullOrEmpty(cargoInfo))
                    sb.Append("\n").Append(cargoInfo);
                sb.Append("\n").Append("MaiyaAeroBay_RideQuestReward".Translate(rewardSilver));
                if (!string.IsNullOrEmpty(timeLeft))
                    sb.Append("\n").Append("MaiyaAeroBay_RideQuestTimeLeft".Translate(timeLeft));
                if (!string.IsNullOrEmpty(stateStr))
                    sb.Append("\n").Append(stateStr);
                return sb.ToString();
            }
        }

        private string FindRideOrderState()
        {
            var manager = Find.World?.GetComponent<WorldComponent_RideHailingManager>();
            if (manager == null) return "";
            var order = manager.FindOrderByID(orderID);
            if (order == null) return "";
            if (order.state == RideOrderState.Accepted)
            {
                if (orderType == RideOrderType.TransportPerson)
                    return "MaiyaAeroBay_RideStatePickupPassenger".Translate();
                return "MaiyaAeroBay_RideStatePickup".Translate();
            }
            if (order.state == RideOrderState.PickedUp)
            {
                if (orderType == RideOrderType.TransportPerson)
                    return "MaiyaAeroBay_RideStateDeliverPassenger".Translate();
                return "MaiyaAeroBay_RideStateDeliver".Translate();
            }
            return "";
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref orderID, "orderID", "");
            Scribe_Values.Look(ref pickupTile, "pickupTile", -1);
            Scribe_Values.Look(ref dropoffTile, "dropoffTile", -1);
            Scribe_Values.Look(ref pickupLabel, "pickupLabel", "");
            Scribe_Values.Look(ref dropoffLabel, "dropoffLabel", "");
            Scribe_Values.Look(ref orderTypeLabel, "orderTypeLabel", "");
            Scribe_Values.Look(ref rewardSilver, "rewardSilver", 0);
            Scribe_Values.Look(ref cargoInfo, "cargoInfo", "");
            Scribe_Values.Look(ref completeDeadlineTick, "completeDeadlineTick", -1);
            Scribe_Values.Look(ref orderType, "orderType", RideOrderType.TransportPerson);
        }
    }
}
