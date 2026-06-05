using System.Collections.Generic;
using System.Linq;
using LudeonTK;
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

        [DebugAction("MaiyaAeroBay", "短途运人 (Short Person)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShortPerson()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportPerson, false);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "远程运人 (Long Person)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLongPerson()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportPerson, true);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "短途货运 (Short Cargo)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShortCargo()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportCargo, false);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "远程货运 (Long Cargo)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLongCargo()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportCargo, true);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "超时惩罚 (Force Timeout)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
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

        [DebugAction("MaiyaAeroBay", "模拟飞单 (Force Fare Dodged)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceFareDodged()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceFareDodged();
            Log.Message("[MaiyaAeroBay] Debug: fare dodged will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟恶意差评 (Force Bad Review)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceBadReview()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceBadReview();
            Log.Message("[MaiyaAeroBay] Debug: bad review will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟好评打赏 (Force Good Tip)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceGoodTip()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceGoodTip();
            Log.Message("[MaiyaAeroBay] Debug: good tip will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟遗落物品 (Force Left-Behind)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceLeftBehind()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceLeftBehind();
            Log.Message("[MaiyaAeroBay] Debug: left-behind item will trigger on next complete");
        }
    }
}
