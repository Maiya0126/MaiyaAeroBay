using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MaiyaAeroBay
{
    public class InteriorMapComponent : MapComponent
    {
        private Thing parentShuttleThing;
        private bool repairAttempted;
        private int cachedUpgradeLevel;

        public Comp_ShuttleInterior ParentShuttle
        {
            get
            {
                if (parentShuttleThing != null)
                {
                    var comp = parentShuttleThing.TryGetComp<Comp_ShuttleInterior>();
                    if (comp != null) return comp;
                }
                if (!repairAttempted)
                {
                    repairAttempted = true;
                    RepairParentShuttle();
                    if (parentShuttleThing != null)
                        return parentShuttleThing.TryGetComp<Comp_ShuttleInterior>();
                }
                return null;
            }
        }

        public int CachedUpgradeLevel
        {
            get
            {
                if (cachedUpgradeLevel > 0) return cachedUpgradeLevel;
                var shuttle = ParentShuttle;
                if (shuttle != null)
                {
                    cachedUpgradeLevel = shuttle.UpgradeLevel;
                    return cachedUpgradeLevel;
                }
                return 0;
            }
        }

        public InteriorMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref parentShuttleThing, "parentShuttleThing");
            Scribe_Values.Look(ref cachedUpgradeLevel, "cachedUpgradeLevel", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                repairAttempted = false;
        }

        public void SetParentShuttle(Comp_ShuttleInterior shuttle)
        {
            parentShuttleThing = shuttle?.parent;
            repairAttempted = false;
            if (shuttle != null)
                cachedUpgradeLevel = shuttle.UpgradeLevel;
        }

        public void UpdateCachedLevel(int level)
        {
            if (level > 0) cachedUpgradeLevel = level;
        }

        private void RepairParentShuttle()
        {
            foreach (Map extMap in Find.Maps)
            {
                if (extMap == null || extMap == map) continue;
                foreach (var thing in extMap.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    var interior = thing.TryGetComp<Comp_ShuttleInterior>();
                    if (interior != null && interior.PocketMap == map)
                    {
                        parentShuttleThing = thing;
                        cachedUpgradeLevel = interior.UpgradeLevel;
                        return;
                    }
                }
            }
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                var shuttle = caravan.Shuttle;
                if (shuttle == null) continue;
                var interior = shuttle.TryGetComp<Comp_ShuttleInterior>();
                if (interior != null && interior.PocketMap == map)
                {
                    parentShuttleThing = shuttle;
                    cachedUpgradeLevel = interior.UpgradeLevel;
                    return;
                }
            }
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
                                    parentShuttleThing = t;
                                    cachedUpgradeLevel = interior.UpgradeLevel;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
        }

        public void CheckAndDestroyManually()
        {
            var shuttle = ParentShuttle;
            if (shuttle == null)
            {
                return;
            }

            bool hasAnyPawn = false;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing is Pawn)
                {
                    hasAnyPawn = true;
                    break;
                }
            }

            if (!hasAnyPawn && shuttle.PocketMapExists)
            {
                Current.Game.DeinitAndRemoveMap(map, false);
            }
        }
    }
}
