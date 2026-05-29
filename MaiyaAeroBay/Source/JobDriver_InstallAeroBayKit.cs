using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class JobDriver_InstallAeroBayKit : JobDriver
    {
        private const TargetIndex ShuttleInd = TargetIndex.A;
        private const TargetIndex KitInd = TargetIndex.B;
        private const int DurationTicks = 600;

        private Thing Shuttle => job.GetTarget(ShuttleInd).Thing;
        private Thing Kit => job.GetTarget(KitInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Shuttle, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Kit, job, 1, -1, null, errorOnFailed);
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(KitInd, PathEndMode.Touch)
                .FailOnDespawnedOrNull(KitInd)
                .FailOnDespawnedOrNull(ShuttleInd);
            
            yield return Toils_Haul.StartCarryThing(KitInd);
            
            yield return Toils_Goto.GotoThing(ShuttleInd, PathEndMode.Touch)
                .FailOnDespawnedOrNull(ShuttleInd);
            
            Toil installToil = Toils_General.WaitWith(ShuttleInd, DurationTicks, useProgressBar: false, maintainPosture: true, maintainSleep: false, ShuttleInd);
            installToil.WithProgressBarToilDelay(ShuttleInd);
            installToil.FailOnDespawnedOrNull(ShuttleInd);
            installToil.FailOnCannotTouch(ShuttleInd, PathEndMode.Touch);
            yield return installToil;
            
            yield return Toils_General.Do(Install);
        }

private void Install()
        {
            Thing shuttle = Shuttle;
            Thing kit = Kit;
            
            if (shuttle == null || kit == null)
                return;

            if (!(shuttle is ThingWithComps shuttleWithComps))
            {
                Log.Error("[MaiyaAeroBay] Shuttle is not ThingWithComps");
                return;
            }

            CompAeroBayKit kitComp = kit.TryGetComp<CompAeroBayKit>();
            if (kitComp == null)
                return;

            bool success = false;

            switch (kitComp.Props.kitType)
            {
                case ShuttleKitType.Interior:
                    success = InstallInteriorKit(shuttleWithComps, kitComp);
                    break;
                case ShuttleKitType.Weapon:
                    success = InstallWeaponKit(shuttleWithComps, kitComp);
                    break;
                case ShuttleKitType.Shield:
                    success = InstallShieldKit(shuttleWithComps, kitComp);
                    break;
                case ShuttleKitType.Power:
                    success = InstallPowerKit(shuttleWithComps, kitComp);
                    break;
                case ShuttleKitType.Comfort:
                    success = InstallComfortKit(shuttleWithComps, kitComp);
                    break;
            }

            if (success)
            {
                SoundDef.Named("Building_Complete").PlayOneShot(new TargetInfo(shuttle.Position, shuttle.Map));
                kit.SplitOff(1).Destroy();
            }
        }

        private bool InstallInteriorKit(ThingWithComps shuttle, CompAeroBayKit kitComp)
        {
            var existingComp = shuttle.TryGetComp<Comp_ShuttleInterior>();
            if (existingComp == null)
            {
                Log.Error("[MaiyaAeroBay] Comp_ShuttleInterior not found on shuttle. This should not happen.");
                return false;
            }
            if (existingComp.UpgradeLevel > 0)
            {
                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasInterior".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            int level = kitComp.Props.level;
            int width = kitComp.Props.mapWidth;
            int height = kitComp.Props.mapHeight;

            bool isCivilianShuttle = shuttle.def.defName == "MaiyaAeroBay_CivilianShuttle";
            int maxAllowedLevel = isCivilianShuttle ? 2 : 5;

            if (level > maxAllowedLevel)
            {
                Messages.Message("MaiyaAeroBay_CivilianShuttleMaxLevel".Translate(maxAllowedLevel), shuttle, MessageTypeDefOf.RejectInput);
                return false;
            }

            CompProperties_ShuttleInterior interiorProps = new CompProperties_ShuttleInterior();
            interiorProps.mapWidths = new int[] { 6, 7, 8, 9, 10, 12 };
            interiorProps.mapHeights = new int[] { 5, 6, 7, 7, 8, 10 };
            interiorProps.maxUpgradeLevel = maxAllowedLevel;

            existingComp.props = interiorProps;
            existingComp.SavePropsToFields();
            
            existingComp.SetUpgradeLevel(level);

            Messages.Message("MaiyaAeroBay_KitInstalled".Translate(
                kitComp.parent.def.label, 
                shuttle.Label, 
                width - 2, 
                height - 2), 
                shuttle, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool InstallWeaponKit(ThingWithComps shuttle, CompAeroBayKit kitComp)
        {
            var existingWeapon = shuttle.TryGetComp<CompShuttleWeapon>();
            if (existingWeapon != null && existingWeapon.installed)
            {
                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasWeapon".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            CompProperties_ShuttleWeapon weaponProps = new CompProperties_ShuttleWeapon();
            weaponProps.weaponType = kitComp.Props.weaponType;
            weaponProps.damage = kitComp.Props.weaponDamage;
            weaponProps.range = kitComp.Props.weaponRange;
            weaponProps.burstShotCount = kitComp.Props.weaponBurstCount;
            weaponProps.cooldownTime = kitComp.Props.weaponCooldown;

            switch (weaponProps.weaponType)
            {
                case WeaponType.DualMachineGun:
                    weaponProps.projectileDef = DefDatabase<ThingDef>.GetNamed("Bullet_MiniTurret", false);
                    weaponProps.soundCast = SoundDef.Named("GunShotA");
                    break;
                case WeaponType.Autocannon:
                    weaponProps.projectileDef = DefDatabase<ThingDef>.GetNamed("Bullet_AutocannonTurret", false);
                    weaponProps.soundCast = SoundDef.Named("GunShotA");
                    break;
                case WeaponType.RocketPod:
                    weaponProps.projectileDef = DefDatabase<ThingDef>.GetNamed("Bullet_Rocket", false);
                    weaponProps.soundCast = SoundDef.Named("Explosion_EMP");
                    break;
                case WeaponType.LaserCannon:
                    weaponProps.projectileDef = DefDatabase<ThingDef>.GetNamed("Bullet_ChargeRifle", false);
                    weaponProps.soundCast = SoundDef.Named("GunShotA");
                    break;
            }

            if (existingWeapon != null)
            {
                existingWeapon.props = weaponProps;
                existingWeapon.SavePropsToFields();
            }
            else
            {
                ThingComp weaponComp = (ThingComp)Activator.CreateInstance(typeof(CompShuttleWeapon));
                weaponComp.props = weaponProps;
                shuttle.AllComps.Add(weaponComp);
                weaponComp.parent = shuttle;
                ((CompShuttleWeapon)weaponComp).SavePropsToFields();
                RebuildCompsByType(shuttle);
            }

            Messages.Message("MaiyaAeroBay_WeaponKitInstalled".Translate(
                kitComp.parent.def.label, 
                shuttle.Label), 
                shuttle, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool InstallShieldKit(ThingWithComps shuttle, CompAeroBayKit kitComp)
        {
            var existingShield = shuttle.TryGetComp<CompShuttleShield>();
            if (existingShield != null && existingShield.installed)
            {
                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasShield".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            CompProperties_ShuttleShield shieldProps = new CompProperties_ShuttleShield();
            shieldProps.shieldType = kitComp.Props.shieldType;
            shieldProps.maxHitPoints = kitComp.Props.shieldMaxHP;
            shieldProps.rechargeRate = kitComp.Props.shieldRechargeRate;
            shieldProps.empDisarmTicks = kitComp.Props.shieldEMPDisarmTicks;

            if (existingShield != null)
            {
                existingShield.props = shieldProps;
                existingShield.SavePropsToFields();
                existingShield.InitHitPoints();
            }
            else
            {
                ThingComp shieldComp = (ThingComp)Activator.CreateInstance(typeof(CompShuttleShield));
                shieldComp.props = shieldProps;
                shuttle.AllComps.Add(shieldComp);
                shieldComp.parent = shuttle;
                ((CompShuttleShield)shieldComp).SavePropsToFields();
                ((CompShuttleShield)shieldComp).InitHitPoints();
                RebuildCompsByType(shuttle);
            }

            Messages.Message("MaiyaAeroBay_ShieldKitInstalled".Translate(
                kitComp.parent.def.label, 
                shuttle.Label), 
                shuttle, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool InstallPowerKit(ThingWithComps shuttle, CompAeroBayKit kitComp)
        {
            var existingPower = shuttle.TryGetComp<CompShuttlePower>();
            if (existingPower != null && existingPower.installed)
            {
                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasPower".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            CompProperties_ShuttlePower powerProps = new CompProperties_ShuttlePower();
            powerProps.fuelCapacityMultiplier = kitComp.Props.fuelCapacityMultiplier;
            powerProps.cooldownMultiplier = kitComp.Props.cooldownMultiplier;

            if (existingPower != null)
            {
                existingPower.props = powerProps;
                existingPower.SavePropsToFields();
            }
            else
            {
                ThingComp powerComp = (ThingComp)Activator.CreateInstance(typeof(CompShuttlePower));
                powerComp.props = powerProps;
                shuttle.AllComps.Add(powerComp);
                powerComp.parent = shuttle;
                ((CompShuttlePower)powerComp).SavePropsToFields();
                RebuildCompsByType(shuttle);
            }

            ShuttleFuelHelper.ApplyFuelCapacityMultiplier(shuttle, powerProps.fuelCapacityMultiplier);

            string fuelMsg = "MaiyaAeroBay_PowerFuelMsg".Translate((powerProps.fuelCapacityMultiplier * 100f).ToString("F0"));
            string cooldownMsg = powerProps.cooldownMultiplier < 1f
                ? "MaiyaAeroBay_PowerCooldownReduced".Translate(((1f - powerProps.cooldownMultiplier) * 100).ToString("F0"))
                : "MaiyaAeroBay_PowerCooldownMsg".Translate((powerProps.cooldownMultiplier * 100f).ToString("F0"));

            Messages.Message("MaiyaAeroBay_PowerKitInstalledMsg".Translate(kitComp.parent.def.label, shuttle.Label, fuelMsg, cooldownMsg),
                shuttle, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool InstallComfortKit(ThingWithComps shuttle, CompAeroBayKit kitComp)
        {
            var existingComfort = shuttle.TryGetComp<CompShuttleComfort>();
            if (existingComfort != null && existingComfort.installed)
            {
                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasComfort".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            var interior = shuttle.TryGetComp<Comp_ShuttleInterior>();
            if (interior == null || interior.UpgradeLevel <= 0)
            {
                Messages.Message("MaiyaAeroBay_ComfortNeedsInterior".Translate(), shuttle, MessageTypeDefOf.RejectInput);
                return false;
            }

            CompProperties_ShuttleComfort comfortProps = new CompProperties_ShuttleComfort();
            comfortProps.hungerRateMultiplier = kitComp.Props.hungerRateMultiplier;
            comfortProps.restRateMultiplier = kitComp.Props.restRateMultiplier;
            comfortProps.comfortMoodOffset = kitComp.Props.comfortMoodOffset;
            comfortProps.hediffDefName = kitComp.Props.hediffDefName;

            if (existingComfort != null)
            {
                existingComfort.props = comfortProps;
                existingComfort.SavePropsToFields();
            }
            else
            {
                ThingComp comfortComp = (ThingComp)Activator.CreateInstance(typeof(CompShuttleComfort));
                comfortComp.props = comfortProps;
                shuttle.AllComps.Add(comfortComp);
                comfortComp.parent = shuttle;
                ((CompShuttleComfort)comfortComp).SavePropsToFields();
                RebuildCompsByType(shuttle);
            }

            var comfortCompTyped = shuttle.TryGetComp<CompShuttleComfort>();
            comfortCompTyped.ApplyToAllOccupants();

            Messages.Message("MaiyaAeroBay_ComfortKitInstalled".Translate(
                kitComp.parent.def.label,
                shuttle.Label),
                shuttle, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        internal static void RebuildCompsByType(ThingWithComps thing)
        {
            var field = typeof(ThingWithComps).GetField("compsByType", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;
            var dict = thing.AllComps
                .GroupBy(c => c.GetType())
                .ToDictionary(g => g.Key, g => g.ToArray());
            field.SetValue(thing, dict);
        }
    }
}
