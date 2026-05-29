using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class CompProperties_ShuttleWeapon : CompProperties
    {
        public WeaponType weaponType = WeaponType.DualMachineGun;
        public int damage = 12;
        public float range = 28f;
        public int burstShotCount = 2;
        public int ticksBetweenBurstShots = 8;
        public float cooldownTime = 4.8f;
        public float accuracyTouch = 0.77f;
        public float accuracyShort = 0.7f;
        public float accuracyMedium = 0.45f;
        public float accuracyLong = 0.24f;
        public ThingDef projectileDef;
        public SoundDef soundCast;

        public CompProperties_ShuttleWeapon()
        {
            compClass = typeof(CompShuttleWeapon);
        }
    }

    public class CompShuttleWeapon : ThingComp
    {
        public CompProperties_ShuttleWeapon Props => (CompProperties_ShuttleWeapon)props;
        
        internal int cooldownTicksRemaining = 0;
        private int burstShotsRemaining = 0;
        private Thing target = null;
        private int ticksUntilNextShot = 0;

        public bool installed = false;
        private WeaponType s_weaponType = WeaponType.DualMachineGun;
        private int s_damage = 12;
        private float s_range = 28f;
        private int s_burstShotCount = 2;
        private int s_ticksBetweenBurstShots = 8;
        private float s_cooldownTime = 4.8f;
        private string s_projectileDefName = "";
        private string s_soundCastName = "";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref installed, "weaponInstalled", false);
            Scribe_Values.Look(ref s_weaponType, "weaponType", WeaponType.DualMachineGun);
            Scribe_Values.Look(ref s_damage, "damage", 12);
            Scribe_Values.Look(ref s_range, "range", 28f);
            Scribe_Values.Look(ref s_burstShotCount, "burstShotCount", 2);
            Scribe_Values.Look(ref s_ticksBetweenBurstShots, "ticksBetweenBurstShots", 8);
            Scribe_Values.Look(ref s_cooldownTime, "cooldownTime", 4.8f);
            Scribe_Values.Look(ref s_projectileDefName, "projectileDefName", "");
            Scribe_Values.Look(ref s_soundCastName, "soundCastName", "");
            Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining", 0);
            Scribe_Values.Look(ref burstShotsRemaining, "burstShotsRemaining", 0);
            Scribe_Values.Look(ref ticksUntilNextShot, "ticksUntilNextShot", 0);
            Scribe_References.Look(ref target, "target");

            if (!installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs && !string.IsNullOrEmpty(s_projectileDefName))
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
            var newProps = new CompProperties_ShuttleWeapon();
            newProps.weaponType = s_weaponType;
            newProps.damage = s_damage;
            newProps.range = s_range;
            newProps.burstShotCount = s_burstShotCount;
            newProps.ticksBetweenBurstShots = s_ticksBetweenBurstShots;
            newProps.cooldownTime = s_cooldownTime;
            if (!string.IsNullOrEmpty(s_projectileDefName))
                newProps.projectileDef = DefDatabase<ThingDef>.GetNamed(s_projectileDefName, false);
            if (!string.IsNullOrEmpty(s_soundCastName))
                newProps.soundCast = SoundDef.Named(s_soundCastName);
            props = newProps;
        }

        internal void SavePropsToFields()
        {
            installed = true;
            s_weaponType = Props.weaponType;
            s_damage = Props.damage;
            s_range = Props.range;
            s_burstShotCount = Props.burstShotCount;
            s_ticksBetweenBurstShots = Props.ticksBetweenBurstShots;
            s_cooldownTime = Props.cooldownTime;
            s_projectileDefName = Props.projectileDef?.defName ?? "";
            s_soundCastName = Props.soundCast?.defName ?? "";
        }

        public bool IsReady => cooldownTicksRemaining <= 0 && burstShotsRemaining <= 0 && parent != null && parent.Spawned;
        public float CurrentCooldownPercent => cooldownTicksRemaining > 0 ? (float)cooldownTicksRemaining / (Props.cooldownTime * 60f) : 0f;
        public bool IsFlying => parent == null || !parent.Spawned;

        public override void CompTick()
        {
            base.CompTick();

            if (IsFlying || !installed)
                return;

            if (cooldownTicksRemaining > 0)
                cooldownTicksRemaining--;

            if (burstShotsRemaining > 0)
            {
                if (ticksUntilNextShot > 0)
                {
                    ticksUntilNextShot--;
                    return;
                }

                TryFireShot();
                burstShotsRemaining--;
                if (burstShotsRemaining > 0)
                {
                    ticksUntilNextShot = Props.ticksBetweenBurstShots;
                }
                else
                {
                    cooldownTicksRemaining = (int)(Props.cooldownTime * 60f);
                    target = null;
                }
            }
            else if (IsReady)
            {
                TryFindTargetAndFire();
            }
        }

        private void TryFindTargetAndFire()
        {
            if (parent == null || !parent.Spawned)
                return;

            var shuttle = parent;
            var map = shuttle.Map;
            if (map == null)
                return;

            float closestDist = Props.range;
            Thing closestEnemy = null;

            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.Downed)
                    continue;

                if (pawn.IsPrisoner)
                    continue;

                if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    float dist = (pawn.Position - shuttle.Position).LengthHorizontalSquared;
                    float rangeSq = Props.range * Props.range;
                    if (dist <= rangeSq && (closestEnemy == null || dist < closestDist))
                    {
                        closestDist = dist;
                        closestEnemy = pawn;
                    }
                }
            }

            if (closestEnemy != null)
            {
                target = closestEnemy;
                burstShotsRemaining = Props.burstShotCount;
                ticksUntilNextShot = 0;
            }
        }

        private void TryFireShot()
        {
            if (parent == null || target == null || !parent.Spawned)
                return;

            if (target.Destroyed || !target.Spawned)
            {
                target = null;
                burstShotsRemaining = 0;
                return;
            }

            float dist = (target.Position - parent.Position).LengthHorizontal;
            if (dist > Props.range)
            {
                target = null;
                burstShotsRemaining = 0;
                return;
            }

            var projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, parent.Position, parent.Map);
            projectile.Launch(parent, target, target, ProjectileHitFlags.IntendedTarget);

            if (Props.soundCast != null)
            {
                Props.soundCast.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!installed) return null;
            string weaponTypeName = "MaiyaAeroBay_Weapon_" + Props.weaponType.ToString();
            string baseStr = "MaiyaAeroBay_WeaponStatus".Translate(weaponTypeName.Translate());
            if (IsFlying)
            {
                baseStr += " " + "MaiyaAeroBay_WeaponFlying".Translate();
            }
            else if (cooldownTicksRemaining > 0)
            {
                baseStr += " " + "MaiyaAeroBay_Cooldown".Translate(cooldownTicksRemaining.ToStringTicksToPeriod());
            }
            else
            {
                baseStr += " " + "MaiyaAeroBay_WeaponReady".Translate();
            }
            return baseStr;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (IsFlying || !installed)
                yield break;

            yield return new Gizmo_ShuttleWeaponStatus
            {
                weapon = this
            };

            if (Props.weaponType != WeaponType.DualMachineGun)
            {
                yield return new Command_Action
                {
                    defaultLabel = "MaiyaAeroBay_ForceFire".Translate(),
                    defaultDesc = "MaiyaAeroBay_ForceFireDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                    action = delegate
                    {
                        Find.Targeter.BeginTargeting(TargetingParameters.ForAttackAny(), (LocalTargetInfo t) =>
                        {
                            if (t.HasThing && IsReady)
                            {
                                target = t.Thing;
                                burstShotsRemaining = Props.burstShotCount;
                                ticksUntilNextShot = 0;
                            }
                        });
                    }
                };
            }
        }
    }

    public class Gizmo_ShuttleWeaponStatus : Gizmo
    {
        public CompShuttleWeapon weapon;

        private static readonly Texture2D FullBarTex = SolidColorMaterials.NewSolidColorTexture(Color.white);
        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));

        public override float GetWidth(float maxWidth)
        {
            return 140f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);

            string weaponTypeName = "MaiyaAeroBay_Weapon_" + weapon.Props.weaponType.ToString();
            string label = "MaiyaAeroBay_WeaponLabel".Translate(weaponTypeName.Translate());
            Widgets.Label(rect.ContractedBy(7f), label);

            Rect barRect = new Rect(rect.x + 7f, rect.y + 30f, rect.width - 14f, 20f);
            
            float fillPercent;
            Color barColor;
            string statusText;

            if (weapon.IsFlying)
            {
                fillPercent = 0f;
                barColor = Color.gray;
                statusText = "MaiyaAeroBay_WeaponFlying".Translate();
            }
            else if (weapon.cooldownTicksRemaining > 0)
            {
                fillPercent = 1f - weapon.CurrentCooldownPercent;
                barColor = Color.yellow;
                statusText = weapon.cooldownTicksRemaining.ToStringTicksToPeriod();
            }
            else
            {
                fillPercent = 1f;
                barColor = new Color(0.1f, 0.2f, 0.6f);
                statusText = "MaiyaAeroBay_WeaponReady".Translate();
            }

            Texture2D coloredBarTex = SolidColorMaterials.NewSolidColorTexture(barColor);
            Widgets.FillableBar(barRect, fillPercent, coloredBarTex, EmptyBarTex, false);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, statusText);
            Text.Anchor = TextAnchor.UpperLeft;

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
