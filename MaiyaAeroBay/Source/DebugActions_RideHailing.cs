using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MaiyaAeroBay
{
    public static class DebugActions_RideHailing
    {
        private static CompShuttleRideHailing GetFirstRideHailing()
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var comp = thing.TryGetComp<CompShuttleRideHailing>();
                    if (comp != null && comp.installed) return comp;
                }
            }
            return null;
        }

        private static List<CompShuttleRideHailing> GetAllRideHailing()
        {
            var list = new List<CompShuttleRideHailing>();
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var comp = thing.TryGetComp<CompShuttleRideHailing>();
                    if (comp != null && comp.installed) list.Add(comp);
                }
            }
            return list;
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Short person order", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShortPerson()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportPerson, false);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Long person order", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLongPerson()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportPerson, true);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Short cargo order", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShortCargo()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportCargo, false);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Long cargo order", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLongCargo()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportCargo, true);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Force timeout", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceTimeout()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order to timeout"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            if (manager == null) return;
            comp.FailOrder(order, manager, "MaiyaAeroBay_RideFailedTimeout".Translate());
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Force fare dodged", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceFareDodged()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceFareDodged();
            Log.Message("[MaiyaAeroBay] Debug: fare dodged will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "R-ber: Force bad review", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceBadReview()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceBadReview();
            Log.Message("[MaiyaAeroBay] Debug: bad review will trigger on next complete");
        }
    }
}
