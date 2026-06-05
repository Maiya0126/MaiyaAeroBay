using System.Collections.Generic;
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

                return "MaiyaAeroBay_RideQuestDescription".Translate(
                    orderTypeLabel, pickupLabel, dropoffLabel, rewardSilver,
                    cargoInfo, timeLeft, stateStr);
            }
        }

        private string FindRideOrderState()
        {
            var manager = Find.World?.GetComponent<WorldComponent_RideHailingManager>();
            if (manager == null) return "?";
            var order = manager.FindOrderByID(orderID);
            if (order == null) return "MaiyaAeroBay_RideQuestEnded".Translate();
            if (order.state == RideOrderState.Accepted)
                return "MaiyaAeroBay_RideStatePickup".Translate();
            if (order.state == RideOrderState.PickedUp)
                return "MaiyaAeroBay_RideStateDeliver".Translate();
            return order.state.ToString();
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
        }
    }
}
