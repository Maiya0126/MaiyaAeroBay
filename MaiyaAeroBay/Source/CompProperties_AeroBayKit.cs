using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace MaiyaAeroBay
{
    public class CompProperties_AeroBayKit : CompProperties
    {
        public ShuttleKitType kitType = ShuttleKitType.Interior;
        
        public int level = 1;
        public int mapWidth = 7;
        public int mapHeight = 6;

        public WeaponType weaponType = WeaponType.DualMachineGun;
        public int weaponDamage = 12;
        public float weaponRange = 28f;
        public int weaponBurstCount = 2;
        public float weaponCooldown = 4.8f;

        public ShieldType shieldType = ShieldType.Simple;
        public int shieldMaxHP = 150;
        public float shieldRechargeRate = 1f;
        public int shieldEMPDisarmTicks = 600;

        public float fuelCapacityMultiplier = 1f;
        public float cooldownMultiplier = 1f;

        public float hungerRateMultiplier = 1f;
        public float restRateMultiplier = 1f;
        public int comfortMoodOffset = 0;
        public string hediffDefName = "";

        public int fareBasePrice = 50;
        public int baseOrderRange = 20;
        public float orderIntervalDays = 8f;

        public CompProperties_AeroBayKit()
        {
            compClass = typeof(CompAeroBayKit);
        }

        public int GetUsableWidth()
        {
            return mapWidth - 2;
        }

        public int GetUsableHeight()
        {
            return mapHeight - 2;
        }
    }

    public class CompAeroBayKit : ThingComp
    {
        public CompProperties_AeroBayKit Props => (CompProperties_AeroBayKit)props;

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.CompFloatMenuOptions(selPawn))
                yield return opt;

            if (parent?.Map == null)
                yield break;

            var shuttles = parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle)
                .Where(t => t.TryGetComp<CompShuttleKitInstaller>() != null)
                .ToList();

            if (shuttles.Count == 0)
                yield break;

            foreach (var shuttle in shuttles)
            {
                var installer = shuttle.TryGetComp<CompShuttleKitInstaller>();
                bool canInstall = installer.CanInstallKit(parent);
                string disabledReason = canInstall ? "" : installer.GetDisabledReason(parent);

                string label = canInstall
                    ? "MaiyaAeroBay_InstallKitToShuttle".Translate(parent.Label, shuttle.Label)
                    : "MaiyaAeroBay_InstallKitToShuttle".Translate(parent.Label, shuttle.Label) + $" ({disabledReason})";

                var capturedShuttle = shuttle;
                yield return new FloatMenuOption(label, canInstall ? (Action)(() =>
                {
                    installer.SelectKitToInstall(new List<Thing> { parent });
                }) : null);
            }
        }
    }
}
