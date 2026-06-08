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
        private static List<CompShuttleRideHailing> GetAllRideHailings()
        {
            var result = new List<CompShuttleRideHailing>();
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var comp = thing.TryGetComp<CompShuttleRideHailing>();
                    if (comp != null && comp.installed)
                        result.Add(comp);
                }
            }
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                foreach (var thing in caravan.AllThings)
                {
                    if (thing is ThingWithComps twc)
                    {
                        var comp = twc.TryGetComp<CompShuttleRideHailing>();
                        if (comp != null && comp.installed)
                            result.Add(comp);
                    }
                }
            }
            return result;
        }

        private static CompShuttleRideHailing GetFirstRideHailing()
        {
            var all = GetAllRideHailings();
            return all.Count > 0 ? all[0] : null;
        }

        private static CompShuttleRideHailing SelectShuttle()
        {
            var all = GetAllRideHailings();
            if (all.Count == 0) return null;
            if (all.Count == 1) return all[0];

            var options = new List<FloatMenuOption>();
            foreach (var comp in all)
            {
                var c = comp;
                string label = c.parent.LabelCap + " (" + c.parent.ThingID + ")";
                options.Add(new FloatMenuOption(label, () => { }));
            }

            CompShuttleRideHailing selected = null;
            var menu = new FloatMenu(options.Select((opt, i) =>
            {
                var comp = all[i];
                return new FloatMenuOption(opt.Label, () => selected = comp);
            }).ToList());
            Find.WindowStack.Add(menu);
            return all[0];
        }

        private static void DoMandatoryDispatch(RideOrderType type, bool longRange)
        {
            var shuttles = GetAllRideHailings();
            if (shuttles.Count == 0)
            {
                Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed");
                return;
            }

            CompShuttleRideHailing target = null;
            if (shuttles.Count == 1)
            {
                target = shuttles[0];
            }
            else
            {
                var options = new List<FloatMenuOption>();
                foreach (var comp in shuttles)
                {
                    var c = comp;
                    string label = c.parent.LabelCap + " (" + c.StarRating.ToString("F1") + "星)";
                    if (c.ActiveOrder != null) label += " [有订单]";
                    options.Add(new FloatMenuOption(label, () => DoDispatch(c, type, longRange)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
                return;
            }

            DoDispatch(target, type, longRange);
        }

        private static void DoDispatch(CompShuttleRideHailing comp, RideOrderType type, bool longRange)
        {
            if (!comp.CanAcceptNewOrder())
            {
                Log.Message("[MaiyaAeroBay] Debug: shuttle " + comp.parent.ThingID + " cannot accept new order (has active order or offline)");
                return;
            }

            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            if (manager == null) return;

            var order = manager.DebugGenerateOrder(comp, type, longRange);
            if (order != null)
            {
                comp.AcceptOrder(order);
                Log.Message("[MaiyaAeroBay] Debug: mandatory dispatch to " + comp.parent.ThingID + " - " + order.pickupLabel + " -> " + order.dropoffLabel + " reward=" + order.rewardSilver);
            }
            else
            {
                Log.Message("[MaiyaAeroBay] Debug: failed to generate order (need 2+ non-hostile settlements)");
            }
        }

        private static CompShuttleRideHailing GetFirstShuttleWithOrder()
        {
            var all = GetAllRideHailings();
            return all.FirstOrDefault(c => c.ActiveOrder != null);
        }

        [DebugAction("MaiyaAeroBay", "指定派单·载客 (Mandatory Dispatch Passenger)", false, false, false, false, false, 100, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugMandatoryDispatchPerson()
        {
            DoMandatoryDispatch(RideOrderType.TransportPerson, false);
        }

        [DebugAction("MaiyaAeroBay", "指定派单·货运 (Mandatory Dispatch Cargo)", false, false, false, false, false, 100, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugMandatoryDispatchCargo()
        {
            DoMandatoryDispatch(RideOrderType.TransportCargo, false);
        }

        [DebugAction("MaiyaAeroBay", "短途载客 (Short Passenger)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShortPerson()
        {
            var comp = GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var manager = Find.World.GetComponent<WorldComponent_RideHailingManager>();
            var order = manager?.DebugGenerateOrder(comp, RideOrderType.TransportPerson, false);
            if (order != null) Log.Message("[MaiyaAeroBay] Debug: order generated - " + order.pickupLabel + " -> " + order.dropoffLabel);
            else Log.Message("[MaiyaAeroBay] Debug: failed to generate order");
        }

        [DebugAction("MaiyaAeroBay", "远程载客 (Long Passenger)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
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
            var comp = GetFirstShuttleWithOrder() ?? GetFirstRideHailing();
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
            var comp = GetFirstShuttleWithOrder() ?? GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceFareDodged();
            Log.Message("[MaiyaAeroBay] Debug: fare dodged will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟恶意差评 (Force Bad Review)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceBadReview()
        {
            var comp = GetFirstShuttleWithOrder() ?? GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceBadReview();
            Log.Message("[MaiyaAeroBay] Debug: bad review will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟好评打赏 (Force Good Tip)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceGoodTip()
        {
            var comp = GetFirstShuttleWithOrder() ?? GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceGoodTip();
            Log.Message("[MaiyaAeroBay] Debug: good tip will trigger on next complete");
        }

        [DebugAction("MaiyaAeroBay", "模拟遗落物品 (Force Left-Behind)", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceLeftBehind()
        {
            var comp = GetFirstShuttleWithOrder() ?? GetFirstRideHailing();
            if (comp == null) { Log.Message("[MaiyaAeroBay] Debug: no shuttle with R-ber installed"); return; }
            var order = comp.ActiveOrder;
            if (order == null) { Log.Message("[MaiyaAeroBay] Debug: no active order (accept an order first)"); return; }
            comp.SetDebugForceLeftBehind();
            Log.Message("[MaiyaAeroBay] Debug: left-behind item will trigger on next complete");
        }
    }
}
