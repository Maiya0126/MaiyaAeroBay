using System.Linq;
using RimWorld;
using Verse;

namespace MaiyaAeroBay
{
    public static class ShuttleFuelHelper
    {
        public static void ApplyFuelCapacityMultiplier(ThingWithComps shuttle, float multiplier)
        {
            if (!MaiyaAeroBayMod.settings.powerFuelEnabled) return;
            var refuelable = shuttle.TryGetComp<CompRefuelable>();
            if (refuelable == null) return;
            float baseFuelCapacity = shuttle.def.GetCompProperties<CompProperties_Refuelable>().fuelCapacity;
            float newCapacity = baseFuelCapacity * multiplier;
            var props = refuelable.Props;
            props.fuelCapacity = newCapacity;
            if (props.targetFuelLevelConfigurable)
            {
                float baseTarget = shuttle.def.GetCompProperties<CompProperties_Refuelable>().initialConfigurableTargetFuelLevel;
                props.initialConfigurableTargetFuelLevel = baseTarget * multiplier;
            }
            refuelable.TargetFuelLevel = newCapacity;
            refuelable.allowAutoRefuel = true;
        }

        public static void ApplyCooldownMultiplier(ThingWithComps shuttle, float multiplier)
        {
            if (!MaiyaAeroBayMod.settings.powerCooldownEnabled) return;
            var launchable = shuttle.TryGetComp<CompLaunchable>();
            if (launchable == null) return;
            int baseCooldown = shuttle.def.GetCompProperties<CompProperties_Launchable>().cooldownTicks;
            launchable.Props.cooldownTicks = (int)(baseCooldown * multiplier);
        }

        public static void ResetFuelCapacity(ThingWithComps shuttle)
        {
            var refuelable = shuttle.TryGetComp<CompRefuelable>();
            if (refuelable == null) return;
            float baseFuelCapacity = shuttle.def.GetCompProperties<CompProperties_Refuelable>().fuelCapacity;
            var props = refuelable.Props;
            props.fuelCapacity = baseFuelCapacity;
            if (props.targetFuelLevelConfigurable)
            {
                float baseTarget = shuttle.def.GetCompProperties<CompProperties_Refuelable>().initialConfigurableTargetFuelLevel;
                props.initialConfigurableTargetFuelLevel = baseTarget;
            }
            refuelable.TargetFuelLevel = baseFuelCapacity;
        }

        public static void ResetCooldownTicks(ThingWithComps shuttle)
        {
            var launchable = shuttle.TryGetComp<CompLaunchable>();
            if (launchable == null) return;
            int baseCooldown = shuttle.def.GetCompProperties<CompProperties_Launchable>().cooldownTicks;
            launchable.Props.cooldownTicks = baseCooldown;
        }
    }

    public class CompProperties_ShuttlePower : CompProperties
    {
        public float fuelCapacityMultiplier = 1f;
        public float cooldownMultiplier = 1f;

        public CompProperties_ShuttlePower()
        {
            compClass = typeof(CompShuttlePower);
        }
    }

    public class CompShuttlePower : ThingComp
    {
        public CompProperties_ShuttlePower Props => (CompProperties_ShuttlePower)props;
        public bool installed = false;
        private float s_fuelCapacityMultiplier = 1f;
        private float s_cooldownMultiplier = 1f;

        public override string CompInspectStringExtra()
        {
            if (!installed) return null;
            return "MaiyaAeroBay_PowerInspect".Translate(
                (Props.fuelCapacityMultiplier * 100f).ToString("F0"),
                (Props.cooldownMultiplier * 100f).ToString("F0"));
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (installed && s_fuelCapacityMultiplier > 1f)
            {
                var twc = parent as ThingWithComps;
                if (twc != null)
                {
                    ShuttleFuelHelper.ApplyFuelCapacityMultiplier(twc, s_fuelCapacityMultiplier);
                    ShuttleFuelHelper.ApplyCooldownMultiplier(twc, s_cooldownMultiplier);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref installed, "powerInstalled", false);
            Scribe_Values.Look(ref s_fuelCapacityMultiplier, "fuelCapacityMultiplier", 1f);
            Scribe_Values.Look(ref s_cooldownMultiplier, "cooldownMultiplier", 1f);

            if (!installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs && s_fuelCapacityMultiplier > 1f)
            {
                installed = true;
            }

            if (installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                RebuildProps();
            }
        }

        internal void RebuildProps()
        {
            var newProps = new CompProperties_ShuttlePower();
            newProps.fuelCapacityMultiplier = s_fuelCapacityMultiplier;
            newProps.cooldownMultiplier = s_cooldownMultiplier;
            props = newProps;

            if (s_fuelCapacityMultiplier > 1f)
            {
                var twc = parent as ThingWithComps;
                if (twc != null)
                {
                    ShuttleFuelHelper.ApplyFuelCapacityMultiplier(twc, s_fuelCapacityMultiplier);
                }
            }
            if (s_cooldownMultiplier < 1f)
            {
                var twc2 = parent as ThingWithComps;
                if (twc2 != null)
                {
                    ShuttleFuelHelper.ApplyCooldownMultiplier(twc2, s_cooldownMultiplier);
                }
            }
        }

        internal void SavePropsToFields()
        {
            installed = true;
            s_fuelCapacityMultiplier = Props.fuelCapacityMultiplier;
            s_cooldownMultiplier = Props.cooldownMultiplier;
        }
    }

    public class CompProperties_ShuttleComfort : CompProperties
    {
        public float hungerRateMultiplier = 1f;
        public float restRateMultiplier = 1f;
        public int comfortMoodOffset = 0;
        public string hediffDefName = "";

        public CompProperties_ShuttleComfort()
        {
            compClass = typeof(CompShuttleComfort);
        }
    }

    public class CompShuttleComfort : ThingComp
    {
        public CompProperties_ShuttleComfort Props => (CompProperties_ShuttleComfort)props;
        public bool installed = false;
        private float s_hungerRateMultiplier = 1f;
        private float s_restRateMultiplier = 1f;
        private int s_comfortMoodOffset = 0;
        private string s_hediffDefName = "";

        public override string CompInspectStringExtra()
        {
            if (!installed) return null;
            return "MaiyaAeroBay_ComfortInspect".Translate(
                (Props.hungerRateMultiplier * 100f).ToString("F0"),
                (Props.restRateMultiplier * 100f).ToString("F0"),
                Props.comfortMoodOffset);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref installed, "comfortInstalled", false);
            Scribe_Values.Look(ref s_hungerRateMultiplier, "hungerRateMultiplier", 1f);
            Scribe_Values.Look(ref s_restRateMultiplier, "restRateMultiplier", 1f);
            Scribe_Values.Look(ref s_comfortMoodOffset, "comfortMoodOffset", 0);
            Scribe_Values.Look(ref s_hediffDefName, "hediffDefName", "");

            if (!installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs
                && (s_hungerRateMultiplier != 1f || s_restRateMultiplier != 1f || s_comfortMoodOffset != 0 || !string.IsNullOrEmpty(s_hediffDefName)))
            {
                installed = true;
            }

            if (installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                RebuildProps();
            }
        }

        internal void RebuildProps()
        {
            var newProps = new CompProperties_ShuttleComfort();
            newProps.hungerRateMultiplier = s_hungerRateMultiplier;
            newProps.restRateMultiplier = s_restRateMultiplier;
            newProps.comfortMoodOffset = s_comfortMoodOffset;
            newProps.hediffDefName = s_hediffDefName;
            props = newProps;
        }

        internal void SavePropsToFields()
        {
            installed = true;
            s_hungerRateMultiplier = Props.hungerRateMultiplier;
            s_restRateMultiplier = Props.restRateMultiplier;
            s_comfortMoodOffset = Props.comfortMoodOffset;
            s_hediffDefName = Props.hediffDefName;
        }

        public void ApplyComfortHediff(Pawn pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(Props.hediffDefName)) return;
            var hediffDef = DefDatabase<HediffDef>.GetNamed(Props.hediffDefName, false);
            if (hediffDef == null) return;
            if (pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) == null)
            {
                pawn.health.AddHediff(hediffDef);
                Messages.Message("MaiyaAeroBay_ComfortApplied".Translate(
                    pawn.LabelShort,
                    (Props.hungerRateMultiplier * 100f).ToString("F0"),
                    (Props.restRateMultiplier * 100f).ToString("F0"),
                    Props.comfortMoodOffset),
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        public void RemoveComfortHediff(Pawn pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(Props.hediffDefName)) return;
            var hediffDef = DefDatabase<HediffDef>.GetNamed(Props.hediffDefName, false);
            if (hediffDef == null) return;
            var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public void ApplyToAllOccupants()
        {
            var interior = parent?.TryGetComp<Comp_ShuttleInterior>();
            if (interior == null || !interior.PocketMapExists) return;
            var map = interior.PocketMap;
            if (map == null) return;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.IsColonist || pawn.RaceProps.Humanlike)
                {
                    ApplyComfortHediff(pawn);
                }
            }
        }

        public void RemoveFromAllOccupants()
        {
            var interior = parent?.TryGetComp<Comp_ShuttleInterior>();
            if (interior == null || !interior.PocketMapExists) return;
            var map = interior.PocketMap;
            if (map == null) return;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                RemoveComfortHediff(pawn);
            }
        }

        public static void RemoveFromPawnByShuttle(Thing shuttleThing, Pawn pawn)
        {
            if (shuttleThing == null || pawn == null) return;
            var comfort = shuttleThing.TryGetComp<CompShuttleComfort>();
            comfort?.RemoveComfortHediff(pawn);
        }
    }
}
