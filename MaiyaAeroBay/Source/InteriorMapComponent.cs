using RimWorld;
using Verse;

namespace MaiyaAeroBay
{
    public class InteriorMapComponent : MapComponent
    {
        private Thing parentShuttleThing;

        public Comp_ShuttleInterior ParentShuttle
        {
            get
            {
                if (parentShuttleThing != null)
                {
                    return parentShuttleThing.TryGetComp<Comp_ShuttleInterior>();
                }
                return null;
            }
        }

        public InteriorMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref parentShuttleThing, "parentShuttleThing");
        }

        public void SetParentShuttle(Comp_ShuttleInterior shuttle)
        {
            parentShuttleThing = shuttle?.parent;
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