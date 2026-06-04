using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MaiyaAeroBay
{
    public class RideOrder : IExposable
    {
        public string orderID = "";
        public RideOrderType orderType = RideOrderType.TransportPerson;
        public RideDispatchMode dispatchMode = RideDispatchMode.Optional;
        public RideOrderState state = RideOrderState.Pending;

        public int pickupTile = -1;
        public int dropoffTile = -1;
        public string pickupLabel = "";
        public string dropoffLabel = "";
        public Faction faction = null;

        public string passengerName = "";
        public ThingDef cargoDef = null;
        public int cargoCount = 0;

        public int rewardSilver = 0;
        public int penaltySilver = 0;
        public float starReward = 0.1f;
        public float starPenalty = 0.2f;

        public int createdTick = 0;
        public int acceptDeadlineTick = -1;
        public int completeDeadlineTick = -1;

        public string assignedShuttleID = "";

        public int questID = -1;
        public int pickupMarkerID = -1;
        public int dropoffMarkerID = -1;

        public RideOrder() { }

        public RideOrder(
            RideOrderType type,
            RideDispatchMode dispatch,
            int pickup, int dropoff,
            string pickupLbl, string dropoffLbl,
            Faction fac,
            int reward, int penalty,
            float sReward, float sPenalty,
            int acceptDeadline, int completeDeadline)
        {
            orderID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            orderType = type;
            dispatchMode = dispatch;
            pickupTile = pickup;
            dropoffTile = dropoff;
            pickupLabel = pickupLbl;
            dropoffLabel = dropoffLbl;
            faction = fac;
            rewardSilver = reward;
            penaltySilver = penalty;
            starReward = sReward;
            starPenalty = sPenalty;
            createdTick = Find.TickManager?.TicksGame ?? 0;
            acceptDeadlineTick = acceptDeadline;
            completeDeadlineTick = completeDeadline;
            state = RideOrderState.Pending;

            if (orderType == RideOrderType.TransportPerson)
            {
                passengerName = GeneratePassengerName(fac);
            }
            else
            {
                cargoDef = GenerateCargoDef();
                cargoCount = Rand.RangeInclusive(5, 30);
            }
        }

        private static string GeneratePassengerName(Faction fac)
        {
            var names = new[] { "MaiyaAeroBay_PassengerA".Translate(), "MaiyaAeroBay_PassengerB".Translate(), "MaiyaAeroBay_PassengerC".Translate() };
            return names.RandomElement();
        }

        private static ThingDef GenerateCargoDef()
        {
            var candidates = new[]
            {
                ThingDefOf.Steel, ThingDefOf.WoodLog,
                ThingDef.Named("Gold"), ThingDef.Named("Jade"),
                ThingDef.Named("Silver"), ThingDef.Named("Plasteel")
            };
            return candidates.RandomElement();
        }

        public string GetOrderTypeLabel()
        {
            return orderType == RideOrderType.TransportPerson
                ? "MaiyaAeroBay_RideOrderTypePerson".Translate()
                : "MaiyaAeroBay_RideOrderTypeCargo".Translate();
        }

        public string GetDispatchModeLabel()
        {
            return dispatchMode == RideDispatchMode.Mandatory
                ? "MaiyaAeroBay_RideDispatchMandatory".Translate()
                : "MaiyaAeroBay_RideDispatchOptional".Translate();
        }

        public bool IsExpired
        {
            get
            {
                int ticks = Find.TickManager?.TicksGame ?? 0;
                if (state == RideOrderState.Pending && acceptDeadlineTick > 0 && ticks > acceptDeadlineTick)
                    return true;
                if (state == RideOrderState.Accepted && completeDeadlineTick > 0 && ticks > completeDeadlineTick)
                    return true;
                return false;
            }
        }

        public int TicksRemaining
        {
            get
            {
                int ticks = Find.TickManager?.TicksGame ?? 0;
                if (state == RideOrderState.Pending && acceptDeadlineTick > 0)
                    return acceptDeadlineTick - ticks;
                if ((state == RideOrderState.Accepted || state == RideOrderState.PickedUp) && completeDeadlineTick > 0)
                    return completeDeadlineTick - ticks;
                return int.MaxValue;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref orderID, "orderID", "");
            Scribe_Values.Look(ref orderType, "orderType", RideOrderType.TransportPerson);
            Scribe_Values.Look(ref dispatchMode, "dispatchMode", RideDispatchMode.Optional);
            Scribe_Values.Look(ref state, "state", RideOrderState.Pending);
            Scribe_Values.Look(ref pickupTile, "pickupTile", -1);
            Scribe_Values.Look(ref dropoffTile, "dropoffTile", -1);
            Scribe_Values.Look(ref pickupLabel, "pickupLabel", "");
            Scribe_Values.Look(ref dropoffLabel, "dropoffLabel", "");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref passengerName, "passengerName", "");
            Scribe_Defs.Look(ref cargoDef, "cargoDef");
            Scribe_Values.Look(ref cargoCount, "cargoCount", 0);
            Scribe_Values.Look(ref rewardSilver, "rewardSilver", 0);
            Scribe_Values.Look(ref penaltySilver, "penaltySilver", 0);
            Scribe_Values.Look(ref starReward, "starReward", 0.1f);
            Scribe_Values.Look(ref starPenalty, "starPenalty", 0.2f);
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref acceptDeadlineTick, "acceptDeadlineTick", -1);
            Scribe_Values.Look(ref completeDeadlineTick, "completeDeadlineTick", -1);
            Scribe_Values.Look(ref assignedShuttleID, "assignedShuttleID", "");
            Scribe_Values.Look(ref questID, "questID", -1);
            Scribe_Values.Look(ref pickupMarkerID, "pickupMarkerID", -1);
            Scribe_Values.Look(ref dropoffMarkerID, "dropoffMarkerID", -1);
        }
    }
}
