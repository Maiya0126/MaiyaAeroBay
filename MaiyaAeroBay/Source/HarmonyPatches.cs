using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MaiyaAeroBay
{
    internal static class InteriorLevelColors
    {
        public static readonly Color Lv1 = new Color(0.2f, 0.7f, 0.3f);
        public static readonly Color Lv2 = new Color(0.2f, 0.5f, 0.8f);
        public static readonly Color Lv3 = new Color(0.6f, 0.3f, 0.8f);
        public static readonly Color Lv4 = new Color(0.9f, 0.5f, 0.1f);
        public static readonly Color Lv5 = new Color(0.9f, 0.75f, 0.2f);

        public static Color ForLevel(int level)
        {
            switch (level)
            {
                case 1: return Lv1;
                case 2: return Lv2;
                case 3: return Lv3;
                case 4: return Lv4;
                case 5: return Lv5;
                default: return Color.white;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class LTOColonyGroupsCompat
    {
        private static bool attemptedPatch = false;

        static LTOColonyGroupsCompat()
        {
            TryPatchLTO();
        }

        public static void TryPatchLTO()
        {
            if (attemptedPatch) return;
            attemptedPatch = true;

            try
            {
                var ltoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TacticalGroups");
                if (ltoAssembly == null) return;

                var drawerType = ltoAssembly.GetType("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer");
                if (drawerType == null) return;

                var drawGroupFrameMethod = drawerType.GetMethod("DrawGroupFrame",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null);
                if (drawGroupFrameMethod == null) return;

                var prefixMethod = typeof(LTOColonyGroupsCompat).GetMethod("LTO_DrawGroupFrame_Prefix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                var harmony = new Harmony("MaiyaAeroBay.lto_compat");
                harmony.Patch(drawGroupFrameMethod, prefix: new HarmonyMethod(prefixMethod));
                Log.Message("[MaiyaAeroBay] LTO Colony Groups compat patch applied");
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] LTO compat patch failed: " + ex.Message);
            }
        }

        private static bool LTO_DrawGroupFrame_Prefix(int group, ColonistBarColonistDrawer __instance)
        {
            try
            {
                Map targetMap = LTORelfector.GetMapForGroup(group);
                if (targetMap == null || targetMap.Index < 0) return true;

                int level = GetInteriorLevel(targetMap);
                if (level <= 0) return true;

                Color color = InteriorLevelColors.ForLevel(level);
                Rect? groupRect = LTORelfector.GetGroupFrameRect(group);
                if (!groupRect.HasValue) return true;

                Rect position = groupRect.Value;
                float alpha = (targetMap == Find.CurrentMap && Find.CurrentMap != null && !WorldRendererUtility.WorldSelected) ? 1f : 0.75f;
                Color drawColor = new Color(color.r, color.g, color.b, 0.4f * alpha);
                Widgets.DrawRectFast(position, drawColor);

                return false;
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] LTO Prefix error: " + ex.ToString());
                return true;
            }
        }

        internal static int GetInteriorLevel(Map map)
        {
            try
            {
                if (map == null || map.Index < 0 || !map.IsPocketMap) return 0;
                if (map.generatorDef == null) return 0;
                if (map.generatorDef.defName != "MaiyaAeroBay_InteriorSpace") return 0;

                var mapComp = map.GetComponent<InteriorMapComponent>();
                if (mapComp != null && mapComp.CachedUpgradeLevel > 0)
                    return mapComp.CachedUpgradeLevel;

                int level = 0;
                var pocketMapParent = map.Parent as PocketMapParent;
                if (pocketMapParent != null)
                {
                    Map sourceMap = pocketMapParent.sourceMap;
                    if (sourceMap != null && sourceMap.Index >= 0)
                    {
                        foreach (var thing in sourceMap.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                        {
                            var interior = thing.TryGetComp<Comp_ShuttleInterior>();
                            if (interior != null && interior.PocketMap == map)
                            {
                                level = interior.UpgradeLevel;
                                break;
                            }
                        }
                    }
                }

                if (level <= 0)
                {
                    foreach (Map extMap in Find.Maps)
                    {
                        if (extMap == null) continue;
                        foreach (var thing in extMap.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                        {
                            var interior = thing.TryGetComp<Comp_ShuttleInterior>();
                            if (interior != null && interior.PocketMap == map)
                            {
                                level = interior.UpgradeLevel;
                                break;
                            }
                        }
                        if (level > 0) break;
                    }
                }

                if (level <= 0)
                {
                    foreach (Caravan caravan in Find.WorldObjects.Caravans)
                    {
                        var shuttle = caravan.Shuttle;
                        if (shuttle == null) continue;
                        var interior = shuttle.TryGetComp<Comp_ShuttleInterior>();
                        if (interior != null && interior.PocketMap == map)
                        {
                            level = interior.UpgradeLevel;
                            break;
                        }
                    }
                }

                if (level <= 0)
                {
                    foreach (var tt in Find.WorldObjects.TravellingTransporters)
                    {
                        var childHolders = new List<IThingHolder>();
                        tt.GetChildHolders(childHolders);
                        foreach (IThingHolder holder in childHolders)
                        {
                            if (holder is IThingHolder inner && inner.GetDirectlyHeldThings() != null)
                            {
                                for (int i = 0; i < inner.GetDirectlyHeldThings().Count; i++)
                                {
                                    var t = inner.GetDirectlyHeldThings()[i];
                                    if (t is Building_PassengerShuttle)
                                    {
                                        var interior = t.TryGetComp<Comp_ShuttleInterior>();
                                        if (interior != null && interior.PocketMap == map)
                                        {
                                            level = interior.UpgradeLevel;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (level > 0) break;
                        }
                        if (level > 0) break;
                    }
                }

                return level;
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] GetInteriorLevel exception: " + ex.Message);
                return 0;
            }
        }
    }

    internal static class LTORelfector
    {
        private static bool initialized = false;
        private static bool available = false;

        private static Type tacticalColonistBarType;
        private static Type entryType;
        private static FieldInfo cachedEntriesField;
        private static FieldInfo entryMapField;
        private static FieldInfo entryGroupField;
        private static MethodInfo drawLocsGetter;
        private static MethodInfo scaleGetter;
        private static object tacsBarInstance;

        private static void Init()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                var ltoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TacticalGroups");
                if (ltoAssembly == null) return;

                tacticalColonistBarType = ltoAssembly.GetType("TacticalGroups.TacticalColonistBar");
                if (tacticalColonistBarType == null) return;

                entryType = ltoAssembly.GetType("TacticalGroups.TacticalColonistBar+Entry");
                if (entryType == null) return;

                entryMapField = entryType.GetField("map",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                entryGroupField = entryType.GetField("group",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (entryMapField == null || entryGroupField == null) return;

                cachedEntriesField = tacticalColonistBarType.GetField("cachedEntries",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (cachedEntriesField == null) return;

                var drawLocsProp = tacticalColonistBarType.GetProperty("DrawLocs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (drawLocsProp != null)
                    drawLocsGetter = drawLocsProp.GetGetMethod(true);

                var scaleProp = tacticalColonistBarType.GetProperty("Scale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (scaleProp != null)
                    scaleGetter = scaleProp.GetGetMethod(true);

                var tacsUtilsType = ltoAssembly.GetType("TacticalGroups.TacticUtils");
                if (tacsUtilsType != null)
                {
                    foreach (var f in tacsUtilsType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType == tacticalColonistBarType || f.FieldType.IsSubclassOf(tacticalColonistBarType))
                        {
                            tacsBarInstance = f.GetValue(null);
                            break;
                        }
                    }
                }

                if (tacsBarInstance == null)
                {
                    foreach (var f in tacticalColonistBarType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType == tacticalColonistBarType)
                        {
                            tacsBarInstance = f.GetValue(null);
                            break;
                        }
                    }
                }

                available = true;
            }
            catch { }
        }

        public static Map GetMapForGroup(int group)
        {
            Init();
            if (!available) return null;

            try
            {
                var bar = tacsBarInstance;
                if (bar == null) return null;

                var entries = cachedEntriesField.GetValue(bar) as IList;
                if (entries == null) return null;

                foreach (var entry in entries)
                {
                    int g = (int)entryGroupField.GetValue(entry);
                    if (g == group)
                    {
                        return entryMapField.GetValue(entry) as Map;
                    }
                }
            }
            catch { }

            return null;
        }

        public static List<object> GetEntries()
        {
            Init();
            if (!available) return null;

            try
            {
                var bar = tacsBarInstance;
                if (bar == null) return null;

                var entries = cachedEntriesField.GetValue(bar) as IList;
                if (entries == null) return null;

                return entries.Cast<object>().ToList();
            }
            catch { }

            return null;
        }

        public static Vector2[] GetDrawLocs()
        {
            Init();
            if (!available) return null;

            try
            {
                var bar = tacsBarInstance;
                if (bar == null) return null;

                if (drawLocsGetter != null)
                    return drawLocsGetter.Invoke(bar, null) as Vector2[];

                var field = tacticalColonistBarType.GetField("drawLocs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field.GetValue(bar) as Vector2[];

                var colonistBarDrawLocsProp = tacticalColonistBarType.GetProperty("ColonistBarDrawLocs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (colonistBarDrawLocsProp != null)
                {
                    var getter = colonistBarDrawLocsProp.GetGetMethod(true);
                    return getter.Invoke(bar, null) as Vector2[];
                }

                var colonistBarDrawLocsField = tacticalColonistBarType.GetField("ColonistBarDrawLocs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (colonistBarDrawLocsField != null)
                    return colonistBarDrawLocsField.GetValue(bar) as Vector2[];

                var allFields = tacticalColonistBarType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in allFields)
                {
                    if (f.FieldType == typeof(Vector2[]) || f.FieldType == typeof(List<Vector2>))
                    {
                        var val = f.GetValue(bar);
                        if (val is Vector2[] arr) return arr;
                        if (val is List<Vector2> list) return list.ToArray();
                    }
                }

                var allProps = tacticalColonistBarType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var p in allProps)
                {
                    if (p.PropertyType == typeof(Vector2[]) || p.PropertyType == typeof(List<Vector2>))
                    {
                        var val = p.GetValue(bar);
                        if (val is Vector2[] arr) return arr;
                        if (val is List<Vector2> list) return list.ToArray();
                    }
                }
            }
            catch { }

            return null;
        }

        public static Rect? GetGroupFrameRect(int group)
        {
            Init();
            if (!available) return null;

            try
            {
                var bar = tacsBarInstance;
                if (bar == null) return null;

                var drawerField = tacticalColonistBarType.GetField("drawer",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (drawerField == null) return null;

                var drawer = drawerField.GetValue(bar);
                if (drawer == null) return null;

                var drawerType = drawer.GetType();
                var groupFrameRectMethod = drawerType.GetMethod("GroupFrameRect",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null);
                if (groupFrameRectMethod == null) return null;

                var result = groupFrameRectMethod.Invoke(drawer, new object[] { group });
                if (result is Rect rect) return rect;
            }
            catch { }

            return null;
        }

        public static float GetScale()
        {
            Init();
            if (!available) return 0f;

            try
            {
                var bar = tacsBarInstance;
                if (bar != null && scaleGetter != null)
                {
                    return (float)scaleGetter.Invoke(scaleGetter.IsStatic ? null : bar, null);
                }
            }
            catch { }

            return 0f;
        }

        public static int GetEntryGroup(object entry)
        {
            try
            {
                return (int)entryGroupField.GetValue(entry);
            }
            catch { return -1; }
        }
    }

    [HarmonyPatch]
    public static class ColonistBarGroupFramePatch
    {
        [HarmonyPatch(typeof(ColonistBar), "ColonistBarOnGUI")]
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (Event.current.type != EventType.Repaint) return;
                var entries = Find.ColonistBar.Entries;
                if (entries == null || entries.Count == 0) return;
                var drawnGroups = new HashSet<int>();

                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.map == null || drawnGroups.Contains(e.group)) continue;
                    drawnGroups.Add(e.group);

                    Map map = e.map;
                    int level = 0;

                    if (map.IsPocketMap)
                    {
                        level = LTOColonyGroupsCompat.GetInteriorLevel(map);
                    }
                    else
                    {
                        foreach (PocketMapParent pmp in Find.World.pocketMaps)
                        {
                            if (pmp.sourceMap != map || !pmp.HasMap) continue;
                            level = LTOColonyGroupsCompat.GetInteriorLevel(pmp.Map);
                            if (level > 0) break;
                        }
                    }

                    if (level <= 0) continue;
                    DrawColorForGroup(e.group, map, level);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] ColonistBarGroupFramePatch error: " + ex.Message);
            }
        }

        private static void DrawColorForGroup(int group, Map map, int level)
        {
            Color color = InteriorLevelColors.ForLevel(level);
            Rect frameRect = GroupFrameRect(group);
            if (frameRect.width <= 0 || frameRect.height <= 0) return;
            float alpha = (map == Find.CurrentMap && !WorldRendererUtility.WorldSelected) ? 0.35f : 0.2f;

            Color tint = color;
            tint.a = alpha;
            Widgets.DrawRectFast(frameRect, tint);

            Color borderColor = color;
            borderColor.a = Mathf.Min(alpha + 0.2f, 1f);
            Widgets.DrawBox(frameRect, 2, SolidColorMaterials.NewSolidColorTexture(borderColor));
        }

        private static Rect GroupFrameRect(int group)
        {
            var entries = Find.ColonistBar.Entries;
            var drawLocs = Find.ColonistBar.DrawLocs;
            float minX = 99999f;
            float maxX = 0f;
            float maxY = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].group == group)
                {
                    minX = Mathf.Min(minX, drawLocs[i].x);
                    maxX = Mathf.Max(maxX, drawLocs[i].x + Find.ColonistBar.Size.x);
                    maxY = Mathf.Max(maxY, drawLocs[i].y + Find.ColonistBar.Size.y);
                }
            }
            return new Rect(minX, 0f, maxX - minX, maxY - 0f).ContractedBy(-12f * Find.ColonistBar.Scale);
        }
    }

    [HarmonyPatch]
    public static class ShuttleMassCapacityPatch
    {
        [HarmonyPatch(typeof(CompTransporter), "MassCapacity", MethodType.Getter)]
        [HarmonyPostfix]
        public static void Postfix(CompTransporter __instance, ref float __result)
        {
            if (!MaiyaAeroBayMod.settings.interiorMassEnabled)
            {
                return;
            }
            var shuttle = __instance.parent;
            if (shuttle == null) return;

            var interior = shuttle.TryGetComp<Comp_ShuttleInterior>();
            if (interior != null && interior.UpgradeLevel > 0)
            {
                float multiplier = interior.Props.GetMassMultiplier(interior.UpgradeLevel);
                __result *= multiplier;
            }
        }
    }

    [HarmonyPatch(typeof(MapParent), "CheckRemoveMapNow")]
    public static class ShuttlePocketMapPreventRemovalPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MapParent __instance)
        {
            try
            {
                if (!__instance.HasMap) return true;
                Map map = __instance.Map;

                foreach (PocketMapParent pmp in Find.World.pocketMaps.ToList())
                {
                    if (pmp.sourceMap == map && pmp.HasMap && pmp.Map.mapPawns.AnyPawnBlockingMapRemoval)
                    {
                        return false;
                    }
                }

                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var interior = thing.TryGetComp<Comp_ShuttleInterior>();
                    if (interior == null || !interior.PocketMapExists) continue;
                    var pmp = interior.pocketMapParent;
                    if (pmp == null || !pmp.HasMap) continue;
                    if (pmp.Map.mapPawns.AnyPawnBlockingMapRemoval)
                    {
                        if (pmp.sourceMap != map)
                        {
                            pmp.sourceMap = map;
                        }
                        return false;
                    }
                }

                foreach (PocketMapParent pmp in Find.World.pocketMaps.ToList())
                {
                    if (pmp.sourceMap != map || !pmp.HasMap) continue;
                    try
                    {
                        if (pmp.Map.mapPawns.AllPawns.Any(p => p.IsColonist || p.RaceProps?.Animal == true))
                        {
                            return false;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] ShuttlePocketMapPreventRemoval check failed: " + ex.Message);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Game), "DeinitAndRemoveMap")]
    public static class ShuttlePocketMapEvacPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Map map)
        {
            try
            {
                if (map == null) return;

                var affectedPMPs = new List<PocketMapParent>();
                foreach (PocketMapParent pmp in Find.World.pocketMaps.ToList())
                {
                    if (!pmp.HasMap) continue;
                    if (pmp.sourceMap == map)
                    {
                        affectedPMPs.Add(pmp);
                        continue;
                    }
                    if (pmp.sourceMap != null && Find.Maps.Contains(pmp.sourceMap)) continue;
                    if (pmp.Map != null && pmp.Map.mapPawns.AllPawns.Any(p => p.IsColonist))
                    {
                        affectedPMPs.Add(pmp);
                    }
                }

                foreach (var pmp in affectedPMPs)
                {
                    Map pocketMap = pmp.Map;
                    if (pocketMap == null) continue;

                    var shuttles = new List<Comp_ShuttleInterior>();
                    foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                    {
                        var comp = thing.TryGetComp<Comp_ShuttleInterior>();
                        if (comp != null && comp.PocketMap == pocketMap)
                            shuttles.Add(comp);
                    }

                    Map fallbackMap = Find.AnyPlayerHomeMap;
                    if (fallbackMap == null || fallbackMap == map)
                    {
                        foreach (Map m in Find.Maps)
                        {
                            if (m != map && m != pocketMap)
                            {
                                fallbackMap = m;
                                break;
                            }
                        }
                    }
                    if (fallbackMap == null) continue;

                    List<Pawn> pawnsToEvac = new List<Pawn>();
                    foreach (Thing t in pocketMap.listerThings.AllThings)
                    {
                        if (t is Pawn p && (p.IsColonist || p.RaceProps.Animal))
                            pawnsToEvac.Add(p);
                    }

                    foreach (Pawn p in pawnsToEvac.ToList())
                    {
                        if (shuttles.Count > 0)
                            CompShuttleComfort.RemoveFromPawnByShuttle(shuttles[0].parent, p);
                        if (p.Spawned) p.DeSpawn();
                        IntVec3 loc = CellFinder.RandomSpawnCellForPawnNear(fallbackMap.Center, fallbackMap, 10);
                        GenSpawn.Spawn(p, loc, fallbackMap, Rot4.Random);
                    }

                    if (pawnsToEvac.Count > 0)
                        Messages.Message("MaiyaAeroBay_PawnsEvacuated".Translate(pawnsToEvac.Count), MessageTypeDefOf.PositiveEvent);

                    pmp.sourceMap = fallbackMap;
                    foreach (var shuttle in shuttles)
                    {
                        shuttle.OnSourceMapRemoved(fallbackMap);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] Emergency evacuation on map removal failed: " + ex.Message);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class RideHailingSettlementPatches
    {
        static RideHailingSettlementPatches()
        {
            var harmony = MaiyaAeroBayMod.harmony;
            var settlementType = typeof(Settlement);
            var getFloatMenuMethod = settlementType.GetMethod("GetFloatMenuOptions",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Caravan) }, null);
            if (getFloatMenuMethod != null)
            {
                var postfix = typeof(RideHailingSettlementPatches).GetMethod("SettlementFloatMenuPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(getFloatMenuMethod, postfix: new HarmonyMethod(postfix));
            }
        }

        private static void SettlementFloatMenuPostfix(Caravan caravan, ref IEnumerable<FloatMenuOption> __result, Settlement __instance)
        {
            if (!MaiyaAeroBayMod.settings.rideHailingEnabled) return;
            var opts = RideHailingSettlementInteractions.GetMenuOptions(__instance);
            if (opts != null)
                __result = __result.Concat(opts);
        }
    }

    internal static class RideHailingSettlementInteractions
    {
        public static IEnumerable<FloatMenuOption> GetMenuOptions(Settlement settlement)
        {
            var manager = Find.World?.GetComponent<WorldComponent_RideHailingManager>();
            if (manager == null) yield break;

            var allActive = manager.GetAllActiveOrders();
            foreach (var order in allActive)
            {
                if (order.state == RideOrderState.Accepted && settlement.Tile == order.pickupTile)
                {
                    yield return new FloatMenuOption(
                        "MaiyaAeroBay_RidePickupAction".Translate(order.GetOrderTypeLabel(), order.pickupLabel),
                        () =>
                        {
                            order.state = RideOrderState.PickedUp;
                            manager.UpdatePickupMarkerCompleted(order);
                            var comp = manager.FindShuttleCompByOrder(order);
                            string detail = order.orderType == RideOrderType.TransportPerson
                                ? "MaiyaAeroBay_RidePickedUpPerson".Translate(order.passengerName, order.dropoffLabel)
                                : "MaiyaAeroBay_RidePickedUpCargo".Translate(order.cargoDef?.label ?? "cargo", order.dropoffLabel);
                            Messages.Message("MaiyaAeroBay_RidePickedUp".Translate(detail), MessageTypeDefOf.PositiveEvent);
                        });
                }
                else if (order.state == RideOrderState.PickedUp && settlement.Tile == order.dropoffTile)
                {
                    yield return new FloatMenuOption(
                        "MaiyaAeroBay_RideDropoffAction".Translate(order.GetOrderTypeLabel(), order.dropoffLabel),
                        () =>
                        {
                            var comp = manager.FindShuttleCompByOrder(order);
                            if (comp != null)
                            {
                                comp.CompleteOrderFromWorld(order, manager);
                            }
                            else
                            {
                                order.state = RideOrderState.Completed;
                                manager.EndQuestForOrder(order, QuestEndOutcome.Success);
                                manager.RemoveOrder(order);
                                Messages.Message("MaiyaAeroBay_RideCompletedSimple".Translate(order.rewardSilver),
                                    MessageTypeDefOf.PositiveEvent);
                            }
                        });
                }
            }
        }
    }
}
