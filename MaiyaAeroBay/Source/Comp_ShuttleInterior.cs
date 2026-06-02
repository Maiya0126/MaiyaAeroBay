using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class Comp_ShuttleInterior : ThingComp
    {
        private static Texture2D enterTex;
        private static Texture2D EnterTex
        {
            get
            {
                if (enterTex == null)
                    enterTex = ContentFinder<Texture2D>.Get("UI/Commands/EnterCave", true);
                return enterTex;
            }
        }
        private static Texture2D viewPocketMapTex;
        private static Texture2D ViewPocketMapTex
        {
            get
            {
                if (viewPocketMapTex == null)
                    viewPocketMapTex = ContentFinder<Texture2D>.Get("UI/Commands/ViewPlanet", true);
                return viewPocketMapTex;
            }
        }
        private static Texture2D upgradeTex;
        private static Texture2D UpgradeTex
        {
            get
            {
                if (upgradeTex == null)
                    upgradeTex = ContentFinder<Texture2D>.Get("UI/Commands/Install", true);
                return upgradeTex;
            }
        }
        private static Texture2D transferItemsTex;
        private static Texture2D TransferItemsTex
        {
            get
            {
                if (transferItemsTex == null)
                    transferItemsTex = ContentFinder<Texture2D>.Get("UI/Commands/TransferItems", true);
                return transferItemsTex;
            }
        }

        public Map pocketMap;
        public PocketMapParent pocketMapParent;
        public ExitPortal exit;
        private bool beenEntered;
        private bool hasBeenOccupied;
        private int upgradeLevel = 0;
        private int s_maxUpgradeLevel = 0;
        private bool s_hasCustomProps = false;

        public CompProperties_ShuttleInterior Props => (CompProperties_ShuttleInterior)props;

        public int UpgradeLevel
        {
            get => upgradeLevel;
            set
            {
                upgradeLevel = Mathf.Clamp(value, 0, Props.maxUpgradeLevel);
            }
        }

        public void SetUpgradeLevel(int level)
        {
            upgradeLevel = Mathf.Clamp(level, 0, Props.maxUpgradeLevel);
            if (upgradeLevel > 0 && MaiyaAeroBayMod.settings.interiorSpaceEnabled)
            {
                GeneratePocketMap();
            }
        }

        public Map PocketMap
        {
            get
            {
                if (pocketMapParent != null)
                {
                    pocketMap = pocketMapParent.Map;
                }
                return pocketMap;
            }
        }

        public bool PocketMapExists
        {
            get
            {
                if (pocketMapParent == null) return false;
                if (!pocketMapParent.HasMap) return false;
                pocketMap = pocketMapParent.Map;
                return pocketMap != null && pocketMap.Size.x > 0 && pocketMap.Size.z > 0;
            }
        }
        public ExitPortal Exit => exit;
        public Map ParentMap => base.parent.Map;
        public bool HasBeenOccupied => hasBeenOccupied;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (pocketMapParent != null && pocketMapParent.sourceMap == null && parent.Map != null)
            {
                pocketMapParent.sourceMap = parent.Map;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref upgradeLevel, "upgradeLevel", 0);
            Scribe_References.Look(ref pocketMap, "pocketMap");
            Scribe_References.Look(ref pocketMapParent, "pocketMapParent");
            Scribe_References.Look(ref exit, "exit");
            Scribe_Values.Look(ref beenEntered, "beenEntered", false);
            Scribe_Values.Look(ref hasBeenOccupied, "hasBeenOccupied", false);
            Scribe_Values.Look(ref s_maxUpgradeLevel, "s_maxUpgradeLevel", 0);
            Scribe_Values.Look(ref s_hasCustomProps, "s_hasCustomProps", false);

            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs && s_hasCustomProps)
            {
                RebuildProps();
            }
        }

        private void RebuildProps()
        {
            CompProperties_ShuttleInterior interiorProps = new CompProperties_ShuttleInterior();
            interiorProps.mapWidths = new int[] { 6, 7, 8, 9, 10, 12 };
            interiorProps.mapHeights = new int[] { 5, 6, 7, 7, 8, 10 };
            interiorProps.maxUpgradeLevel = s_maxUpgradeLevel;
            this.props = interiorProps;
        }

        public void SavePropsToFields()
        {
            s_maxUpgradeLevel = Props.maxUpgradeLevel;
            s_hasCustomProps = true;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (mode == DestroyMode.Vanish || mode == DestroyMode.WillReplace)
            {
                base.PostDestroy(mode, previousMap);
                return;
            }

            Map interiorMap = null;
            if (pocketMapParent != null && pocketMapParent.HasMap)
            {
                interiorMap = pocketMapParent.Map;
            }
            else if (pocketMap != null)
            {
                interiorMap = pocketMap;
            }

            Map safeMap = previousMap;
            if (safeMap == null || !Find.Maps.Contains(safeMap))
            {
                safeMap = FindAFallbackMap();
            }

            if (interiorMap != null)
            {
                List<Pawn> pawnsToEvacuate = new List<Pawn>();
                foreach (var thing in interiorMap.listerThings.AllThings)
                {
                    if (thing is Pawn pawn && (pawn.IsColonist || pawn.RaceProps.Animal))
                    {
                        pawnsToEvacuate.Add(pawn);
                    }
                }

                if (pawnsToEvacuate.Count > 0 && safeMap != null && safeMap.Size.x > 0)
                {
                    IntVec3 spawnPos = safeMap.Center;
                    if (!spawnPos.Walkable(safeMap) || !spawnPos.InBounds(safeMap))
                    {
                        spawnPos = CellFinder.RandomSpawnCellForPawnNear(safeMap.Center, safeMap, 10);
                    }

                    foreach (var pawn in pawnsToEvacuate)
                    {
                        CompShuttleComfort.RemoveFromPawnByShuttle(base.parent, pawn);
                        if (pawn.Spawned)
                            pawn.DeSpawn();
                        try
                        {
                            IntVec3 pos = CellFinder.RandomSpawnCellForPawnNear(spawnPos, safeMap, 3);
                            GenSpawn.Spawn(pawn, pos, safeMap, Rot4.Random);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[MaiyaAeroBay] Failed to evacuate pawn: " + ex.Message);
                        }
                    }
                    Messages.Message("MaiyaAeroBay_PawnsEvacuated".Translate(pawnsToEvacuate.Count), MessageTypeDefOf.PositiveEvent);
                }
                else if (pawnsToEvacuate.Count > 0)
                {
                    Log.Error("[MaiyaAeroBay] Cannot evacuate interior pawns - no valid map found! " + pawnsToEvacuate.Count + " pawns may be lost.");
                }
            }

            if (pocketMapParent != null && pocketMapParent.HasMap)
            {
                PocketMapUtility.DestroyPocketMap(pocketMapParent.Map);
            }
            else if (pocketMap != null)
            {
                PocketMapUtility.DestroyPocketMap(pocketMap);
            }
            pocketMap = null;
            pocketMapParent = null;
            exit = null;
            base.PostDestroy(mode, previousMap);
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (UpgradeLevel > 0)
            {
                yield return new Command_Action
                {
                    icon = EnterTex,
                    defaultLabel = "MaiyaAeroBay_EnterInterior".Translate(),
                    defaultDesc = "MaiyaAeroBay_EnterInteriorDesc".Translate(),
                    action = TryEnterInterior
                };
            }

            if (PocketMapExists)
            {
                yield return new Command_Action
                {
                    icon = ViewPocketMapTex,
                    defaultLabel = "MaiyaAeroBay_ViewInterior".Translate(),
                    defaultDesc = "MaiyaAeroBay_ViewInteriorDesc".Translate(),
                    action = delegate
                    {
                        if (PocketMap != null)
                        {
                            CameraJumper.TryJump(PocketMap.Center, PocketMap);
                        }
                    }
                };
            }

            if (UpgradeLevel > 0 && PocketMapExists)
            {
                yield return new Command_Action
                {
                    icon = TransferItemsTex,
                    defaultLabel = "MaiyaAeroBay_TransferItems".Translate(),
                    defaultDesc = "MaiyaAeroBay_TransferItemsDesc".Translate(),
                    action = TryTransferItems
                };
            }
        }

        private void TryEnterInterior()
        {
            if (UpgradeLevel <= 0)
            {
                Messages.Message("MaiyaAeroBay_NeedUpgradeFirst".Translate(), base.parent, MessageTypeDefOf.RejectInput);
                return;
            }

            if (!PocketMapExists)
            {
                if (!MaiyaAeroBayMod.settings.interiorSpaceEnabled)
                {
                    Messages.Message("MaiyaAeroBay_InteriorSpaceDisabled".Translate(), parent, MessageTypeDefOf.RejectInput);
                    return;
                }
                GeneratePocketMap();
            }

            if (PocketMapExists)
            {
                Find.WindowStack.Add(new Dialog_EnterShuttleInterior(this));
            }
            else
            {
                Log.Error("[MaiyaAeroBay] Cannot enter - PocketMap is null after generation!");
                Messages.Message("MaiyaAeroBay_ExitNotReady".Translate(), base.parent, MessageTypeDefOf.RejectInput);
            }
        }

        private void TryTransferItems()
        {
            if (!PocketMapExists)
            {
                Messages.Message("MaiyaAeroBay_NeedUpgradeFirst".Translate(), base.parent, MessageTypeDefOf.RejectInput);
                return;
            }
            Find.WindowStack.Add(new Dialog_TransferItems(this));
        }

        public void OnSourceMapRemoved(Map newSourceMap)
        {
            if (pocketMapParent != null && pocketMapParent.sourceMap == null)
            {
                pocketMapParent.sourceMap = newSourceMap;
            }
            if (parent is ThingWithComps twc && twc.Map != null)
            {
            }
        }

        private void GeneratePocketMap()
        {
            int width = Props.GetMapWidth(UpgradeLevel);
            int height = Props.GetMapHeight(UpgradeLevel);
            
            var mapGenDef = DefDatabase<MapGeneratorDef>.GetNamed("MaiyaAeroBay_InteriorSpace", false);
            if (mapGenDef == null)
            {
                Log.Error("[MaiyaAeroBay] MaiyaAeroBay_InteriorSpace MapGeneratorDef not found!");
                return;
            }

            GenStep_ShuttleInterior.SetPendingShuttle(this);
            
            pocketMap = PocketMapUtility.GeneratePocketMap(
                new IntVec3(width, 1, height),
                mapGenDef,
                Enumerable.Empty<GenStepWithParams>(),
                base.parent.Map);
            
            pocketMapParent = pocketMap?.Parent as PocketMapParent;
        }

        public void OnEntered(Pawn pawn)
        {
            hasBeenOccupied = true;
            if (!beenEntered)
            {
                beenEntered = true;
            }
            if (Find.CurrentMap == base.parent.Map)
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        public Map GetOtherMapDirect()
        {
            if (PocketMap == null)
            {
                GeneratePocketMap();
            }
            return PocketMap;
        }

        public bool HasOccupantsInInterior()
        {
            if (!PocketMapExists) return false;
            var map = PocketMap;
            if (map == null) return false;
            
            return map.mapPawns.AllPawnsSpawned.Any();
        }

        public override string CompInspectStringExtra()
        {
            if (UpgradeLevel > 0)
            {
                int width = Props.GetUsableWidth(UpgradeLevel);
                int height = Props.GetUsableHeight(UpgradeLevel);
                return "MaiyaAeroBay_InteriorSpace".Translate(width, height, UpgradeLevel);
            }
            return null;
        }
    }
}