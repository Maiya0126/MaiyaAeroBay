using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MaiyaAeroBay
{
    public class ShuttleInteriorPortal : MapPortal
    {
        public readonly Comp_ShuttleInterior comp;

        public ShuttleInteriorPortal(Comp_ShuttleInterior comp)
        {
            this.comp = comp;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
        }

        public override void ExposeData()
        {
        }

        public override bool IsEnterable(out string reason)
        {
            reason = "";
            return true;
        }

        public override Map GetOtherMap()
        {
            return comp.GetOtherMapDirect();
        }

        public override IntVec3 GetDestinationLocation()
        {
            return comp.Exit?.Position ?? IntVec3.Invalid;
        }

        public override void OnEntered(Pawn pawn)
        {
            comp.OnEntered(pawn);
        }

        protected override Map GeneratePocketMapInt()
        {
            int width = comp.Props.GetMapWidth(comp.UpgradeLevel);
            int height = comp.Props.GetMapHeight(comp.UpgradeLevel);
            var mapGenDef = DefDatabase<MapGeneratorDef>.GetNamed("MaiyaAeroBay_InteriorSpace", false);
            return PocketMapUtility.GeneratePocketMap(
                new IntVec3(width, 1, height),
                mapGenDef,
                Enumerable.Empty<GenStepWithParams>(),
                comp.parent.Map);
        }

        protected override IEnumerable<GenStepWithParams> GetExtraGenSteps()
        {
            return Enumerable.Empty<GenStepWithParams>();
        }
    }
}