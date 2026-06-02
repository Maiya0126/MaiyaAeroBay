using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MaiyaAeroBay
{
    public class GenStep_ShuttleInterior : GenStep
    {
        private static Comp_ShuttleInterior pendingShuttle;

        public static void SetPendingShuttle(Comp_ShuttleInterior shuttle)
        {
            pendingShuttle = shuttle;
        }

        public override int SeedPart
        {
            get
            {
                return 123456789;
            }
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            var shuttle = pendingShuttle;
            pendingShuttle = null;
            
            GenerateInterior(map, shuttle);
        }

        public void GenerateWithShuttle(Map map, Comp_ShuttleInterior shuttle)
        {
            GenerateInterior(map, shuttle);
        }

        private void GenerateInterior(Map map, Comp_ShuttleInterior shuttle)
        {
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            IntVec3 size = map.Size;

            var floorDef = DefDatabase<TerrainDef>.GetNamed("MaiyaAeroBay_InteriorFloor", false);
            var wallDef = DefDatabase<ThingDef>.GetNamed("MaiyaAeroBay_InteriorWall", false);
            var roofDef = DefDatabase<RoofDef>.GetNamed("MaiyaAeroBay_ShuttleRoof", false);

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    map.terrainGrid.SetTerrain(cell, floorDef);
                    elevation[cell] = 0f;
                }
            }

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    if (x == 0 || x == size.x - 1 || z == 0 || z == size.z - 1)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        if (x == 0 || x == size.x - 1)
                        {
                            CreateWall(map, cell, wallDef);
                        }
                        if (z == 0 || z == size.z - 1)
                        {
                            if (map.edificeGrid[cell] == null)
                            {
                                CreateWall(map, cell, wallDef);
                            }
                        }
                    }
                }
            }

            CreateRoof(map, size, roofDef);
            CreatePowerInfrastructure(map, size, shuttle);
            CreateInteriorMapComponent(map, shuttle);
            CreateExit(map, size, shuttle);
        }

        private void CreatePowerInfrastructure(Map map, IntVec3 size, Comp_ShuttleInterior shuttle)
        {
            int level = shuttle?.UpgradeLevel ?? 0;
            if (level <= 0) return;

            var conduitDef = DefDatabase<ThingDef>.GetNamed("MaiyaAeroBay_InteriorConduit", false);
            if (conduitDef == null) return;

            for (int x = 1; x < size.x - 1; x++)
            {
                for (int z = 1; z < size.z - 1; z++)
                {
                    if (x == 1 || x == size.x - 2 || z == 1 || z == size.z - 2)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        Thing conduit = ThingMaker.MakeThing(conduitDef);
                        conduit.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(conduit, cell, map);
                    }
                }
            }
        }

        private void CreateInteriorMapComponent(Map map, Comp_ShuttleInterior shuttle)
        {
            var comp = map.GetComponent<InteriorMapComponent>();
            if (comp == null)
            {
                comp = new InteriorMapComponent(map);
                map.components.Add(comp);
            }
            
            if (shuttle != null)
            {
                comp.SetParentShuttle(shuttle);
            }
        }

        private void CreateWall(Map map, IntVec3 cell, ThingDef wallDef)
        {
            if (!cell.InBounds(map))
                return;

            if (wallDef == null) return;

            Thing wall = ThingMaker.MakeThing(wallDef);
            wall.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(wall, cell, map);
            MaiyaAeroBayMod.SetWallPowerOutput(wall.TryGetComp<CompPowerPlant>(), MaiyaAeroBayMod.settings.wallPowerPerCell);
        }

        private void CreateRoof(Map map, IntVec3 size, RoofDef roofDef)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    map.roofGrid.SetRoof(cell, roofDef);
                }
            }
        }

        private void CreateExit(Map map, IntVec3 size, Comp_ShuttleInterior shuttle)
        {
            IntVec3 exitPos = new IntVec3(1, 0, 1);

            if (!exitPos.InBounds(map)) return;

            var building = map.edificeGrid[exitPos];
            if (building != null)
            {
                building.Destroy();
            }

            var portalDef = DefDatabase<ThingDef>.GetNamed("MaiyaAeroBay_ExitPortal", false);
            if (portalDef == null) 
            {
                Log.Warning("MaiyaAeroBay_ExitPortal not found!");
                return;
            }

            Thing exit = ThingMaker.MakeThing(portalDef);
            exit.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(exit, exitPos, map, Rot4.North);

            var exitBuilding = exit as ExitPortal;
            
            if (shuttle == null)
            {
                var interiorComp = map.GetComponent<InteriorMapComponent>();
                if (interiorComp != null)
                {
                    shuttle = interiorComp.ParentShuttle;
                }
            }
            
            if (exitBuilding != null && shuttle != null)
            {
                exitBuilding.SetLinkedShuttle(shuttle);
                shuttle.exit = exitBuilding;
            }
            else
            {
                Log.Warning("[MaiyaAeroBay] Could not link ExitPortal");
            }
        }
    }
}