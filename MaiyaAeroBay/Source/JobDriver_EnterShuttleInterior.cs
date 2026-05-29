using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MaiyaAeroBay
{
    public class JobDriver_EnterShuttleInterior : JobDriver
    {
        private const TargetIndex ShuttleIndex = TargetIndex.A;

        private Building shuttle => job.GetTarget(TargetIndex.A).Thing as Building;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(shuttle, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => shuttle == null || !shuttle.Spawned);

            yield return Toils_Goto.GotoThing(ShuttleIndex, PathEndMode.Touch);

            Toil waitToil = Toils_General.Wait(15).FailOnCannotTouch(ShuttleIndex, PathEndMode.Touch);
            yield return waitToil;

            Toil enterToil = new Toil();
            enterToil.initAction = delegate
            {
                var shuttleRef = shuttle;
                if (shuttleRef == null)
                    return;

                var comp = shuttleRef.GetComp<Comp_ShuttleInterior>();
                if (comp == null)
                {
                    Messages.Message("MaiyaAeroBay_CannotEnterCompMissing".Translate(), shuttleRef, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                if (!comp.PocketMapExists)
                {
                    Messages.Message("MaiyaAeroBay_CannotEnterNoPocketMap".Translate(), shuttleRef, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                Map pocketMap = comp.PocketMap;
                if (pocketMap == null || pocketMap.Size.x <= 0 || pocketMap.Size.z <= 0)
                {
                    Messages.Message("MaiyaAeroBay_CannotEnterInvalidMap".Translate(), shuttleRef, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                IntVec3 destLoc = new IntVec3(2, 0, 1);
                if (!destLoc.InBounds(pocketMap) || !destLoc.Standable(pocketMap))
                {
                    destLoc = CellFinder.StandableCellNear(new IntVec3(2, 0, 1), pocketMap, 10f);
                }

                if (destLoc == IntVec3.Invalid || !destLoc.InBounds(pocketMap))
                {
                    Messages.Message("MaiyaAeroBay_CannotEnter".Translate(), shuttleRef, MessageTypeDefOf.NegativeEvent);
                    return;
                }

                bool wasDrafted = false;
                bool fireAtWill = false;
                if (pawn.IsPlayerControlled)
                {
                    wasDrafted = pawn.Drafted;
                    fireAtWill = pawn.drafter?.FireAtWill ?? false;
                }

                var comfortComp = shuttleRef.GetComp<CompShuttleComfort>();

                pawn.DeSpawnOrDeselect();
                GenSpawn.Spawn(pawn, destLoc, pocketMap, Rot4.Random);

                comp.OnEntered(pawn);

                if (comfortComp != null)
                {
                    comfortComp.ApplyComfortHediff(pawn);
                }

                if (pawn.IsPlayerControlled && wasDrafted)
                {
                    pawn.drafter.Drafted = true;
                    pawn.drafter.FireAtWill = fireAtWill;
                }

                pawn.mindState.priorityWork.ClearPrioritizedWorkAndJobQueue();
                if (pawn.GetLord() != null)
                {
                    pawn.GetLord().Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
                }
            };
            enterToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enterToil;
        }
    }
}