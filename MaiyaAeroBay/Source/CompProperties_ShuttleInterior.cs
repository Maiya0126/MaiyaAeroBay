using System;
using System.Collections.Generic;
using Verse;

namespace MaiyaAeroBay
{
    public class CompProperties_ShuttleInterior : CompProperties
    {
        public int upgradeLevel = 0;

        public int[] mapWidths = new int[] { 6, 7, 8, 9, 10, 12 };
        public int[] mapHeights = new int[] { 5, 6, 7, 7, 8, 10 };

        public float[] massMultipliers = new float[] { 1f, 1.2f, 1.5f, 3.0f, 5.0f, 10f };

        public int[] upgradeSteelCost = new int[] { 0, 500, 500, 500, 500, 500 };
        public int[] upgradeComponentCost = new int[] { 0, 10, 10, 10, 10, 10 };
        public int[] upgradeAdvComponentCost = new int[] { 0, 2, 2, 2, 2, 2 };

        public int maxUpgradeLevel = 5;

        public CompProperties_ShuttleInterior()
        {
            compClass = typeof(Comp_ShuttleInterior);
        }

        public int GetMapWidth(int level)
        {
            if (level >= 0 && level < mapWidths.Length)
                return mapWidths[level];
            return mapWidths[0];
        }

        public int GetMapHeight(int level)
        {
            if (level >= 0 && level < mapHeights.Length)
                return mapHeights[level];
            return mapHeights[0];
        }

        public int GetUsableWidth(int level)
        {
            return Math.Max(2, GetMapWidth(level) - 2);
        }

        public int GetUsableHeight(int level)
        {
            return Math.Max(1, GetMapHeight(level) - 2);
        }

        public float GetMassMultiplier(int level)
        {
            if (level >= 0 && level < massMultipliers.Length)
                return massMultipliers[level];
            return 1f;
        }

        public int GetUpgradeSteelCost(int targetLevel)
        {
            if (targetLevel >= 0 && targetLevel < upgradeSteelCost.Length)
                return upgradeSteelCost[targetLevel];
            return 0;
        }

        public int GetUpgradeComponentCost(int targetLevel)
        {
            if (targetLevel >= 0 && targetLevel < upgradeComponentCost.Length)
                return upgradeComponentCost[targetLevel];
            return 0;
        }

        public int GetUpgradeAdvComponentCost(int targetLevel)
        {
            if (targetLevel >= 0 && targetLevel < upgradeAdvComponentCost.Length)
                return upgradeAdvComponentCost[targetLevel];
            return 0;
        }

        public int GetStorageSlots(int level)
        {
            return 200;
        }
    }
}