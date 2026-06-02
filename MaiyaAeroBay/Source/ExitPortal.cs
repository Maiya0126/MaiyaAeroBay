using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class ExitPortal : Building
    {
        private Comp_ShuttleInterior linkedShuttle;
        private Thing linkedShuttleThing;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                FindAndLinkToShuttle();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedShuttleThing, "linkedShuttleThing");
        }

        private void FindAndLinkToShuttle()
        {
            var interiorComp = Map.GetComponent<InteriorMapComponent>();
            if (interiorComp != null && interiorComp.ParentShuttle != null)
            {
                linkedShuttle = interiorComp.ParentShuttle;
                linkedShuttleThing = linkedShuttle?.parent;
            }
        }

        public void SetLinkedShuttle(Comp_ShuttleInterior shuttle)
        {
            linkedShuttle = shuttle;
            linkedShuttleThing = shuttle?.parent;
        }

        public Comp_ShuttleInterior GetLinkedShuttle()
        {
            if (linkedShuttle == null && linkedShuttleThing != null)
            {
                linkedShuttle = linkedShuttleThing.TryGetComp<Comp_ShuttleInterior>();
            }
            return linkedShuttle;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "MaiyaAeroBay_ExitToShuttle".Translate(),
                defaultDesc = "MaiyaAeroBay_ExitToShuttleDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Exit", true),
                action = delegate
                {
                    ShowExitSelectionDialog();
                }
            };
        }

        private void ShowExitSelectionDialog()
        {
            var shuttle = GetLinkedShuttle();
            if (shuttle != null && shuttle.parent != null && shuttle.parent.Spawned)
            {
                Find.WindowStack.Add(new Dialog_SelectPawnsForExit(this, shuttle));
                return;
            }

            Find.WindowStack.Add(new Dialog_SelectPawnsForExit(this, null));
        }

        public void TransportPawnsToShuttle(List<Pawn> pawns)
        {
            var shuttle = GetLinkedShuttle();
            bool normalExit = shuttle != null && shuttle.parent != null && shuttle.parent.Spawned;

            Map targetMap;
            IntVec3 targetPos;

            if (normalExit)
            {
                targetMap = shuttle.parent.Map;
                targetPos = shuttle.parent.Position;
            }
            else
            {
                targetMap = FindAFallbackMap();
                if (targetMap == null)
                {
                    Messages.Message("MaiyaAeroBay_NoSafeMapFound".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }
                targetPos = CellFinder.RandomSpawnCellForPawnNear(targetMap.Center, targetMap, 10);
            }

            foreach (Pawn pawn in pawns.ToList())
            {
                if (!pawn.Spawned || pawn.Map != Map)
                    continue;

                CompShuttleComfort.RemoveFromPawnByShuttle(linkedShuttleThing, pawn);

                bool wasDrafted = pawn.Drafted;
                bool fireAtWill = pawn.drafter?.FireAtWill ?? false;

                pawn.DeSpawnOrDeselect();
                IntVec3 pos = CellFinder.RandomSpawnCellForPawnNear(targetPos, targetMap, normalExit ? 2 : 3);
                GenSpawn.Spawn(pawn, pos, targetMap, Rot4.Random);

                if (pawn.IsPlayerControlled && wasDrafted)
                {
                    pawn.drafter.Drafted = true;
                    pawn.drafter.FireAtWill = fireAtWill;
                }

                pawn.mindState.priorityWork.ClearPrioritizedWorkAndJobQueue();
            }

            SoundDefOf.Click.PlayOneShotOnCamera();
            if (normalExit)
            {
                Messages.Message("MaiyaAeroBay_ExitSuccess".Translate(pawns.Count), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("MaiyaAeroBay_EmergencyEvacuated".Translate(pawns.Count), MessageTypeDefOf.PositiveEvent);
            }
        }

        private Map FindAFallbackMap()
        {
            Map homeMap = Find.AnyPlayerHomeMap;
            if (homeMap != null) return homeMap;
            foreach (Map map in Find.Maps)
            {
                if (map.mapPawns.FreeColonistsSpawnedCount > 0)
                    return map;
            }
            if (Find.Maps.Any())
                return Find.Maps[0];
            return null;
        }

        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();
            var nets = Map?.powerNetManager?.AllNetsListForReading;
            if (nets != null && nets.Count > 0)
            {
                float totalProduction = 0f;
                float totalStorage = 0f;
                foreach (var net in nets)
                {
                    totalProduction += net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
                    totalStorage += net.CurrentStoredEnergy();
                }
                string powerInfo = "MaiyaAeroBay_InteriorPowerInfo".Translate(totalProduction.ToString("F0"), totalStorage.ToString("F0"));
                if (!string.IsNullOrEmpty(baseStr))
                    baseStr += "\n";
                baseStr += powerInfo;
            }
            return baseStr;
        }

        public void EvacuateAll()
        {
            List<Pawn> allPawns = Map.mapPawns.AllPawnsSpawned
                .Where(p => p.IsColonist || p.IsColonyAnimal || p.RaceProps.Animal)
                .ToList();

            if (allPawns.Count == 0)
            {
                Messages.Message("MaiyaAeroBay_NoPawnsToExit".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            TransportPawnsToShuttle(allPawns);
        }
    }
}