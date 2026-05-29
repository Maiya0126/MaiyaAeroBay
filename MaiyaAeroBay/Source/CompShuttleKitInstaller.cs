using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MaiyaAeroBay
{
    public class CompProperties_ShuttleKitInstaller : CompProperties
    {
        public CompProperties_ShuttleKitInstaller()
        {
            compClass = typeof(CompShuttleKitInstaller);
        }
    }

    public class CompShuttleKitInstaller : ThingComp
    {
        private static Texture2D installKitTex;
        private static Texture2D InstallKitTex
        {
            get
            {
                if (installKitTex == null)
                    installKitTex = ContentFinder<Texture2D>.Get("UI/Commands/CallShuttle", false);
                return installKitTex;
            }
        }

        private List<Thing> cachedKits = new List<Thing>();
        private int cacheTick = -1;
        private const int CacheDuration = 60;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            yield return new Command_Action
            {
                defaultLabel = "MaiyaAeroBay_ModifyShuttle".Translate(),
                defaultDesc = "MaiyaAeroBay_ModifyShuttleDesc".Translate(),
                icon = InstallKitTex,
                action = ShowModificationDialog
            };
        }

        private void ShowModificationDialog()
        {
            Find.WindowStack.Add(new Dialog_ShuttleModification(this));
        }

        internal string GetShuttleStatusText()
        {
            var sb = new System.Text.StringBuilder();

            var interior = parent.TryGetComp<Comp_ShuttleInterior>();
            if (interior != null && interior.UpgradeLevel > 0)
            {
                float multiplier = interior.Props.GetMassMultiplier(interior.UpgradeLevel);
                int w = interior.Props.GetUsableWidth(interior.UpgradeLevel);
                int h = interior.Props.GetUsableHeight(interior.UpgradeLevel);
                sb.AppendLine("MaiyaAeroBay_StatusInterior".Translate(interior.UpgradeLevel, w, h));

                var transporter = parent.GetComp<CompTransporter>();
                if (transporter != null)
                    sb.AppendLine("MaiyaAeroBay_StatusCargoCapacity".Translate(transporter.MassCapacity.ToString("F0")));

                if (interior.PocketMapExists)
                {
                    int occupants = interior.PocketMap.mapPawns.AllPawnsSpawned.Count;
                    sb.AppendLine("MaiyaAeroBay_StatusOccupants".Translate(occupants));
                }
            }
            else
            {
                sb.AppendLine("MaiyaAeroBay_StatusInteriorNone".Translate());
            }

            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable != null)
            {
                float maxFuel = refuelable.Props.fuelCapacity;
                var powerForFuel = parent.TryGetComp<CompShuttlePower>();
                if (powerForFuel != null && powerForFuel.installed)
                    maxFuel *= powerForFuel.Props.fuelCapacityMultiplier;
                sb.AppendLine("MaiyaAeroBay_StatusFuel".Translate(refuelable.Fuel.ToString("F0"), maxFuel.ToString("F0")));
            }

            var launchable = parent.GetComp<CompLaunchable>();
            if (launchable != null)
            {
                var power = parent.TryGetComp<CompShuttlePower>();
                if (power != null && power.installed)
                {
                    int baseTicks = launchable.Props.cooldownTicks;
                    int actualTicks = (int)(baseTicks * power.Props.cooldownMultiplier);
                    sb.AppendLine("MaiyaAeroBay_StatusCooldownPower".Translate(actualTicks.ToStringTicksToPeriod(), baseTicks.ToStringTicksToPeriod()));
                }
                else
                {
                    sb.AppendLine("MaiyaAeroBay_StatusCooldown".Translate(launchable.Props.cooldownTicks.ToStringTicksToPeriod()));
                }
            }

            var weapon = parent.TryGetComp<CompShuttleWeapon>();
            if (weapon != null && weapon.installed)
            {
                string typeName = "MaiyaAeroBay_Weapon_" + weapon.Props.weaponType.ToString();
                sb.AppendLine("MaiyaAeroBay_StatusWeapon".Translate(typeName.Translate(), weapon.Props.damage, weapon.Props.range.ToString("F0")));
            }

            var shield = parent.TryGetComp<CompShuttleShield>();
            if (shield != null && shield.installed)
            {
                string typeName = "MaiyaAeroBay_Shield_" + shield.Props.shieldType.ToString();
                sb.AppendLine("MaiyaAeroBay_StatusShield".Translate(typeName.Translate(), shield.CurrentHitPoints, shield.MaxHitPoints));
            }

            var powerKit = parent.TryGetComp<CompShuttlePower>();
            if (powerKit != null && powerKit.installed)
                sb.AppendLine("MaiyaAeroBay_StatusPower".Translate(
                    (powerKit.Props.fuelCapacityMultiplier * 100f).ToString("F0"),
                    (powerKit.Props.cooldownMultiplier * 100f).ToString("F0")));

            var comfort = parent.TryGetComp<CompShuttleComfort>();
            if (comfort != null && comfort.installed)
                sb.AppendLine("MaiyaAeroBay_StatusComfort".Translate(
                    (comfort.Props.hungerRateMultiplier * 100f).ToString("F0"),
                    (comfort.Props.restRateMultiplier * 100f).ToString("F0"),
                    comfort.Props.comfortMoodOffset));

            return sb.ToString().TrimEndNewlines();
        }

        internal List<Thing> GetAvailableKits()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (cacheTick >= 0 && currentTick - cacheTick < CacheDuration && cachedKits != null)
            {
                return cachedKits;
            }

            cachedKits = new List<Thing>();

            if (parent?.Map == null)
                return cachedKits;

            var map = parent.Map;

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (thing == null || thing.Destroyed || !thing.Spawned)
                    continue;

                var kitComp = thing.TryGetComp<CompAeroBayKit>();
                if (kitComp != null)
                {
                    cachedKits.Add(thing);
                }
            }

            cacheTick = currentTick;
            return cachedKits;
        }

        internal bool CanInstallKit(Thing kit)
        {
            if (kit == null || parent == null)
                return false;

            var kitComp = kit.TryGetComp<CompAeroBayKit>();
            if (kitComp == null)
                return false;

            switch (kitComp.Props.kitType)
            {
                case ShuttleKitType.Interior:
                    var interior = parent.TryGetComp<Comp_ShuttleInterior>();
                    if (interior != null && interior.UpgradeLevel > 0)
                        return false;
                    if (parent.def.defName == "MaiyaAeroBay_CivilianShuttle" && kitComp.Props.level > 2)
                        return false;
                    return true;
                case ShuttleKitType.Weapon:
                    var weapon = parent.TryGetComp<CompShuttleWeapon>();
                    return weapon == null || !weapon.installed;
                case ShuttleKitType.Shield:
                    var shield = parent.TryGetComp<CompShuttleShield>();
                    return shield == null || !shield.installed;
                case ShuttleKitType.Power:
                    var power = parent.TryGetComp<CompShuttlePower>();
                    return power == null || !power.installed;
                case ShuttleKitType.Comfort:
                    var comfort = parent.TryGetComp<CompShuttleComfort>();
                    if (comfort != null && comfort.installed)
                        return false;
                    var interiorForComfort = parent.TryGetComp<Comp_ShuttleInterior>();
                    return interiorForComfort != null && interiorForComfort.UpgradeLevel > 0;
                default:
                    return false;
            }
        }

        internal string GetDisabledReason(Thing kit)
        {
            if (kit == null)
                return "MaiyaAeroBay_NotAKit".Translate();

            var kitComp = kit.TryGetComp<CompAeroBayKit>();
            if (kitComp == null)
                return "MaiyaAeroBay_NotAKit".Translate();

            switch (kitComp.Props.kitType)
            {
                case ShuttleKitType.Interior:
                    var interior = parent.TryGetComp<Comp_ShuttleInterior>();
                    if (interior != null && interior.UpgradeLevel > 0)
                        return "MaiyaAeroBay_ShuttleAlreadyHasInterior".Translate();
                    if (parent.def.defName == "MaiyaAeroBay_CivilianShuttle" && kitComp.Props.level > 2)
                        return "MaiyaAeroBay_CivilianShuttleMaxLevel".Translate(2);
                    break;
                case ShuttleKitType.Weapon:
                    var weapon = parent.TryGetComp<CompShuttleWeapon>();
                    if (weapon != null && weapon.installed)
                        return "MaiyaAeroBay_ShuttleAlreadyHasWeapon".Translate();
                    break;
                case ShuttleKitType.Shield:
                    var shield = parent.TryGetComp<CompShuttleShield>();
                    if (shield != null && shield.installed)
                        return "MaiyaAeroBay_ShuttleAlreadyHasShield".Translate();
                    break;
                case ShuttleKitType.Power:
                    var power = parent.TryGetComp<CompShuttlePower>();
                    if (power != null && power.installed)
                        return "MaiyaAeroBay_ShuttleAlreadyHasPower".Translate();
                    break;
                case ShuttleKitType.Comfort:
                    var comfort = parent.TryGetComp<CompShuttleComfort>();
                    if (comfort != null && comfort.installed)
                        return "MaiyaAeroBay_ShuttleAlreadyHasComfort".Translate();
                    var interiorForComfort = parent.TryGetComp<Comp_ShuttleInterior>();
                    if (interiorForComfort == null || interiorForComfort.UpgradeLevel <= 0)
                        return "MaiyaAeroBay_ComfortNeedsInterior".Translate();
                    break;
            }

            return "";
        }

        internal void SelectKitToInstall(List<Thing> kits)
        {
            if (kits == null || kits.Count == 0 || parent == null)
                return;

            var kit = kits[0];
            if (kit == null)
                return;

            var kitComp = kit.TryGetComp<CompAeroBayKit>();
            if (kitComp != null && kitComp.Props.kitType == ShuttleKitType.Interior)
            {
                var existingInterior = parent.TryGetComp<Comp_ShuttleInterior>();
                if (existingInterior != null && existingInterior.UpgradeLevel > 0 && existingInterior.PocketMapExists)
                {
                    var interiorMap = existingInterior.PocketMap;
                    if (interiorMap != null)
                    {
                        int occupantCount = interiorMap.mapPawns.AllPawnsSpawned.Count(p => p.IsColonist || p.RaceProps.Animal);
                        int itemCount = interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Count;
                        
                        if (occupantCount > 0 || itemCount > 0)
                        {
                            Find.WindowStack.Add(new Dialog_MessageBox(
                                "MaiyaAeroBay_UpgradeWarning_Content".Translate(occupantCount, itemCount),
                                "MaiyaAeroBay_UpgradeWarning_Continue".Translate(),
                                () => { ActuallyStartInstallJob(FindColonistToInstall(kit), kit); },
                                "MaiyaAeroBay_UpgradeWarning_Cancel".Translate(),
                                null,
                                "MaiyaAeroBay_UpgradeWarning_Title".Translate()));
                            return;
                        }
                    }
                }
            }

            Pawn worker = FindColonistToInstall(kit);
            if (worker == null)
            {
                Messages.Message("MaiyaAeroBay_NoColonistCanReach".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            ActuallyStartInstallJob(worker, kit);
        }

        private Pawn FindColonistToInstall(Thing kit)
        {
            if (parent?.Map == null || kit == null)
                return null;

            var map = parent.Map;
            Pawn bestPawn = null;
            float bestScore = float.MaxValue;

            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == null || pawn.Downed || pawn.Drafted)
                    continue;

                if (!pawn.CanReach(kit, PathEndMode.Touch, Danger.Deadly))
                    continue;

                if (!pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float score = (pawn.Position - kit.Position).LengthHorizontalSquared + (kit.Position - parent.Position).LengthHorizontalSquared;

                if (bestPawn == null || score < bestScore)
                {
                    bestPawn = pawn;
                    bestScore = score;
                }
            }

            return bestPawn;
        }

        private void ActuallyStartInstallJob(Pawn worker, Thing kit)
        {
            if (worker == null || kit == null || parent == null)
                return;

            JobDef installJob = DefDatabase<JobDef>.GetNamed("MaiyaAeroBay_InstallKit", false);
            if (installJob == null)
            {
                Log.Error("[MaiyaAeroBay] MaiyaAeroBay_InstallKit JobDef not found");
                return;
            }

            Job job = JobMaker.MakeJob(installJob, parent, kit);
            job.count = 1;
            job.playerForced = true;
            worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            Messages.Message("MaiyaAeroBay_InstallingKit".Translate(kit.def.label, parent.Label),
                new LookTargets(new TargetInfo[] { new TargetInfo(kit), new TargetInfo(parent) }),
                MessageTypeDefOf.NeutralEvent);
        }

        public override string CompInspectStringExtra()
        {
            return GetShuttleStatusText();
        }
    }

    [StaticConstructorOnStartup]
    public static class AeroBayDefInjector
    {
        static AeroBayDefInjector()
        {
            Patch_AddKitInstaller.InjectKitCompsToAllShuttleDefs();
        }
    }

    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Patch_GameLoadGame
    {
        public static void Postfix()
        {
            Patch_AddKitInstaller.AddKitInstallerToExistingShuttles();
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Patch_GameInitNewGame
    {
        public static void Postfix()
        {
            Patch_AddKitInstaller.AddKitInstallerToExistingShuttles();
        }
    }

    public static class Patch_AddKitInstaller
    {
        public static void InjectKitCompsToAllShuttleDefs()
        {
            int addedToDefs = 0;
            
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def == null)
                    continue;

                if (def.comps == null)
                    continue;

                bool hasShuttleComp = def.comps.Any(c => c is CompProperties_Shuttle);
                if (!hasShuttleComp)
                    continue;

                if (!def.comps.Any(c => c is CompProperties_ShuttleKitInstaller))
                {
                    def.comps.Add(new CompProperties_ShuttleKitInstaller());
                    addedToDefs++;
                }

                if (!def.comps.Any(c => c is CompProperties_ShuttleInterior))
                {
                    def.comps.Add(new CompProperties_ShuttleInterior());
                }
                if (!def.comps.Any(c => c is CompProperties_ShuttleWeapon))
                {
                    def.comps.Add(new CompProperties_ShuttleWeapon());
                }
                if (!def.comps.Any(c => c is CompProperties_ShuttleShield))
                {
                    def.comps.Add(new CompProperties_ShuttleShield());
                }
                if (!def.comps.Any(c => c is CompProperties_ShuttlePower))
                {
                    def.comps.Add(new CompProperties_ShuttlePower());
                }
                if (!def.comps.Any(c => c is CompProperties_ShuttleComfort))
                {
                    def.comps.Add(new CompProperties_ShuttleComfort());
                }
            }
        }

        public static void AddKitInstallerToExistingShuttles()
        {
            int addedToInstances = 0;

            foreach (var map in Find.Maps)
            {
                if (map == null)
                    continue;

                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                {
                    if (thing == null)
                        continue;

                    var shuttle = thing as ThingWithComps;
                    if (shuttle == null)
                        continue;

                    bool modified = false;
                    var allComps = shuttle.AllComps;

                    if (!allComps.Any(c => c is CompShuttleKitInstaller))
                    {
                        var c = (CompShuttleKitInstaller)Activator.CreateInstance(typeof(CompShuttleKitInstaller));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttleKitInstaller());
                        allComps.Add(c);
                        modified = true;
                    }
                    if (!allComps.Any(c => c is Comp_ShuttleInterior))
                    {
                        var c = (Comp_ShuttleInterior)Activator.CreateInstance(typeof(Comp_ShuttleInterior));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttleInterior());
                        allComps.Add(c);
                        modified = true;
                    }
                    if (!allComps.Any(c => c is CompShuttleWeapon))
                    {
                        var c = (CompShuttleWeapon)Activator.CreateInstance(typeof(CompShuttleWeapon));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttleWeapon());
                        allComps.Add(c);
                        modified = true;
                    }
                    if (!allComps.Any(c => c is CompShuttleShield))
                    {
                        var c = (CompShuttleShield)Activator.CreateInstance(typeof(CompShuttleShield));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttleShield());
                        allComps.Add(c);
                        modified = true;
                    }
                    if (!allComps.Any(c => c is CompShuttlePower))
                    {
                        var c = (CompShuttlePower)Activator.CreateInstance(typeof(CompShuttlePower));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttlePower());
                        allComps.Add(c);
                        modified = true;
                    }
                    if (!allComps.Any(c => c is CompShuttleComfort))
                    {
                        var c = (CompShuttleComfort)Activator.CreateInstance(typeof(CompShuttleComfort));
                        c.parent = shuttle;
                        c.Initialize(new CompProperties_ShuttleComfort());
                        allComps.Add(c);
                        modified = true;
                    }

                    if (modified)
                    {
                        JobDriver_InstallAeroBayKit.RebuildCompsByType(shuttle);
                        addedToInstances++;
                    }
                }
            }

            if (addedToInstances > 0)
            {
            }
        }
    }

    public class Dialog_ShuttleModification : Window
    {
        private readonly CompShuttleKitInstaller installer;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(500f, 700f);

        public Dialog_ShuttleModification(CompShuttleKitInstaller installer)
        {
            this.installer = installer;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var shuttle = installer.parent;
            if (shuttle == null)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "MaiyaAeroBay_ModifyShuttleTitle".Translate(shuttle.Label));
            Text.Font = GameFont.Small;

            float y = inRect.y + 35f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f), "MaiyaAeroBay_CurrentStatus".Translate());
            y += 22f;

            string statusText = installer.GetShuttleStatusText();
            float statusHeight = Text.CalcHeight(statusText, inRect.width);
            if (statusHeight < 60f) statusHeight = 60f;
            if (statusHeight > 250f) statusHeight = 250f;
            Rect statusRect = new Rect(inRect.x, y, inRect.width, statusHeight);
            Widgets.Label(statusRect, statusText);
            y += statusHeight + 5f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 5f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f), "MaiyaAeroBay_AvailableKits".Translate());
            y += 25f;

            var kits = installer.GetAvailableKits();
            if (kits == null || kits.Count == 0)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "MaiyaAeroBay_NoKitsAvailable".Translate());
                return;
            }

            float listHeight = inRect.yMax - y - 40f;
            if (listHeight < 50f) listHeight = 50f;

            Rect listRect = new Rect(inRect.x, y, inRect.width, listHeight);
            var kitGroups = kits.GroupBy(k => k.def).ToList();
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, kitGroups.Count * 40f + 10f);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float ky = 0f;
                foreach (var kitGroup in kitGroups)
                {
                    var def = kitGroup.Key;
                    int count = kitGroup.Count();
                    var kit = kitGroup.First();
                    bool canInstall = installer.CanInstallKit(kit);
                    string disabledReason = installer.GetDisabledReason(kit);

                    Rect iconRect = new Rect(2f, ky + 2f, 28f, 28f);
                    Widgets.ThingIcon(iconRect, def);

                    float labelX = 34f;
                    Rect rowRect = new Rect(labelX, ky, viewRect.width - labelX, 32f);

                    string labelText = canInstall
                        ? $"{def.label} ×{count}"
                        : $"{def.label} ×{count} ({disabledReason})";

                    if (canInstall)
                    {
                        if (Widgets.ButtonText(rowRect, labelText, false))
                        {
                            installer.SelectKitToInstall(kitGroup.ToList());
                            Close();
                        }
                    }
                    else
                    {
                        GUI.color = new Color(0.6f, 0.6f, 0.6f);
                        Widgets.Label(rowRect, labelText);
                        GUI.color = Color.white;
                    }

                    ky += 40f;
                }
            }
            Widgets.EndScrollView();
        }
    }
}
