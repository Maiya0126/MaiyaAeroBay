using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class MaiyaAeroBayMod : Mod
    {
        public static Harmony harmony;
        public static MaiyaAeroBayModSettings settings;
        private static FieldInfo basePowerConsumptionField;

        public MaiyaAeroBayMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<MaiyaAeroBayModSettings>();
            harmony = new Harmony("Maiya.AeroBay");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            basePowerConsumptionField = typeof(CompProperties_Power).GetField("basePowerConsumption", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void SetWallPowerOutput(CompPowerPlant powerPlant, float watts)
        {
            if (powerPlant != null && basePowerConsumptionField != null)
            {
                basePowerConsumptionField.SetValue(powerPlant.Props, -watts);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.Label("MaiyaAeroBay_KitFeatureTogglesDesc".Translate());
            listingStandard.Gap(8f);
            listingStandard.Label("MaiyaAeroBay_InteriorKitSettings".Translate());
            bool powerFuelEnabled = settings.powerFuelEnabled;
            bool powerCooldownEnabled = settings.powerCooldownEnabled;
            float wallPowerPerCell = settings.wallPowerPerCell;
            listingStandard.CheckboxLabeled("MaiyaAeroBay_InteriorSpaceEnabled".Translate(), ref settings.interiorSpaceEnabled);
            listingStandard.CheckboxLabeled("MaiyaAeroBay_InteriorMassEnabled".Translate(), ref settings.interiorMassEnabled);
            listingStandard.Gap();
            listingStandard.Label("MaiyaAeroBay_PowerKitSettings".Translate());
            listingStandard.CheckboxLabeled("MaiyaAeroBay_PowerFuelEnabled".Translate(), ref settings.powerFuelEnabled);
            listingStandard.CheckboxLabeled("MaiyaAeroBay_PowerCooldownEnabled".Translate(), ref settings.powerCooldownEnabled);
            listingStandard.Gap();
            listingStandard.Label("MaiyaAeroBay_RideHailingSettings".Translate());
            listingStandard.CheckboxLabeled("MaiyaAeroBay_RideHailingEnabled".Translate(), ref settings.rideHailingEnabled);
            listingStandard.Gap();
            listingStandard.Label("MaiyaAeroBay_WallPowerPerCell".Translate() + ": " + settings.wallPowerPerCell.ToString("F0") + "W");
            settings.wallPowerPerCell = listingStandard.Slider(settings.wallPowerPerCell, 0f, 200f);
            if (powerFuelEnabled != settings.powerFuelEnabled || powerCooldownEnabled != settings.powerCooldownEnabled)
            {
                ReapplyPowerSettings();
            }
            if (Math.Abs(wallPowerPerCell - settings.wallPowerPerCell) > 0.01f)
            {
                ReapplyWallPower();
            }
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        private void ReapplyPowerSettings()
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Thing item in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    CompShuttlePower compShuttlePower = item.TryGetComp<CompShuttlePower>();
                    if (compShuttlePower != null && compShuttlePower.installed)
                    {
                        if (settings.powerFuelEnabled)
                        {
                            ShuttleFuelHelper.ApplyFuelCapacityMultiplier(item as ThingWithComps, compShuttlePower.Props.fuelCapacityMultiplier);
                        }
                        else
                        {
                            ShuttleFuelHelper.ResetFuelCapacity(item as ThingWithComps);
                        }
                        if (settings.powerCooldownEnabled)
                        {
                            ShuttleFuelHelper.ApplyCooldownMultiplier(item as ThingWithComps, compShuttlePower.Props.cooldownMultiplier);
                        }
                        else
                        {
                            ShuttleFuelHelper.ResetCooldownTicks(item as ThingWithComps);
                        }
                    }
                }
            }
        }

        private void ReapplyWallPower()
        {
            ThingDef wallDef = DefDatabase<ThingDef>.GetNamedSilentFail("MaiyaAeroBay_InteriorWall");
            if (wallDef == null) return;
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPocketMap) continue;
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing.def == wallDef)
                    {
                        SetWallPowerOutput(thing.TryGetComp<CompPowerPlant>(), settings.wallPowerPerCell);
                    }
                }
            }
        }

        public override string SettingsCategory()
        {
            return "Shuttle Expansion: Wonderland 穿梭机拓展：别有洞天";
        }
    }
}
