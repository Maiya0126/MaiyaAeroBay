using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class CompProperties_ShuttleShield : CompProperties
    {
        public ShieldType shieldType = ShieldType.Simple;
        public int maxHitPoints = 150;
        public float rechargeRate = 1f;
        public int empDisarmTicks = 600;
        public float radius = 3f;

        public CompProperties_ShuttleShield()
        {
            compClass = typeof(CompShuttleShield);
        }
    }

    public class CompShuttleShield : ThingComp
    {
        public CompProperties_ShuttleShield Props => (CompProperties_ShuttleShield)props;
        
        private int currentHitPoints;
        private int empDisarmTicksRemaining = 0;
        private bool isActive = true;

        public bool installed = false;
        private ShieldType s_shieldType = ShieldType.Simple;
        private int s_maxHitPoints = 150;
        private float s_rechargeRate = 1f;
        private int s_empDisarmTicks = 600;

        public int CurrentHitPoints => currentHitPoints;
        public int MaxHitPoints => Props.maxHitPoints;
        public bool IsFlying => parent == null || !parent.Spawned;
        public bool IsActive => isActive && empDisarmTicksRemaining <= 0 && !IsFlying;
        public float ShieldPercent => (float)currentHitPoints / Props.maxHitPoints;
        public bool IsEMPed => empDisarmTicksRemaining > 0;

        public override void PostPostMake()
        {
            base.PostPostMake();
        }

        public void InitHitPoints()
        {
            currentHitPoints = Props.maxHitPoints;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (IsFlying || !installed)
                return;

            if (empDisarmTicksRemaining > 0)
            {
                empDisarmTicksRemaining--;
                return;
            }

            if (currentHitPoints < Props.maxHitPoints)
            {
                currentHitPoints = Mathf.Min(currentHitPoints + (int)Props.rechargeRate, Props.maxHitPoints);
            }
        }

        public void TakeDamage(int damage)
        {
            currentHitPoints = Mathf.Max(0, currentHitPoints - damage);
            if (currentHitPoints <= 0)
            {
                isActive = false;
            }
        }

        public void HitByEMP()
        {
            empDisarmTicksRemaining = Props.empDisarmTicks;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref installed, "shieldInstalled", false);
            Scribe_Values.Look(ref s_shieldType, "shieldType", ShieldType.Simple);
            Scribe_Values.Look(ref s_maxHitPoints, "maxHitPoints", 150);
            Scribe_Values.Look(ref s_rechargeRate, "rechargeRate", 1f);
            Scribe_Values.Look(ref s_empDisarmTicks, "empDisarmTicks", 600);
            Scribe_Values.Look(ref currentHitPoints, "currentHitPoints", s_maxHitPoints);
            Scribe_Values.Look(ref empDisarmTicksRemaining, "empDisarmTicksRemaining", 0);
            Scribe_Values.Look(ref isActive, "isActive", true);

            if (!installed && Scribe.mode == LoadSaveMode.ResolvingCrossRefs
                && (s_shieldType != ShieldType.Simple || s_maxHitPoints != 150 || s_rechargeRate != 1f || s_empDisarmTicks != 600))
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
            var newProps = new CompProperties_ShuttleShield();
            newProps.shieldType = s_shieldType;
            newProps.maxHitPoints = s_maxHitPoints;
            newProps.rechargeRate = s_rechargeRate;
            newProps.empDisarmTicks = s_empDisarmTicks;
            props = newProps;
        }

        internal void SavePropsToFields()
        {
            installed = true;
            s_shieldType = Props.shieldType;
            s_maxHitPoints = Props.maxHitPoints;
            s_rechargeRate = Props.rechargeRate;
            s_empDisarmTicks = Props.empDisarmTicks;
        }

        public override string CompInspectStringExtra()
        {
            if (!installed) return null;
            string shieldTypeName = "MaiyaAeroBay_Shield_" + Props.shieldType.ToString();
            string baseStr = "MaiyaAeroBay_ShieldStatus".Translate(shieldTypeName.Translate());
            if (IsFlying)
            {
                baseStr += " " + "MaiyaAeroBay_ShieldFlying".Translate();
            }
            else if (IsEMPed)
            {
                baseStr += " " + "MaiyaAeroBay_ShieldEMPed".Translate(empDisarmTicksRemaining.ToStringTicksToPeriod());
            }
            else if (IsActive)
            {
                baseStr += " " + "MaiyaAeroBay_ShieldHP".Translate(currentHitPoints, Props.maxHitPoints);
            }
            else
            {
                baseStr += " " + "MaiyaAeroBay_ShieldDown".Translate();
            }
            return baseStr;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!installed || IsFlying)
                yield break;

            yield return new Gizmo_ShuttleShieldStatus
            {
                shield = this
            };
        }
    }

    public class Gizmo_ShuttleShieldStatus : Gizmo
    {
        public CompShuttleShield shield;

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

            string label = "MaiyaAeroBay_ShieldLabel".Translate();
            Widgets.Label(rect.ContractedBy(7f), label);

            float fillPercent;
            Color barColor;
            string statusText;

            if (shield.IsEMPed)
            {
                fillPercent = 0f;
                barColor = Color.red;
                statusText = "MaiyaAeroBay_EMP".Translate();
            }
            else
            {
                fillPercent = shield.ShieldPercent;
                barColor = shield.ShieldPercent > 0.5f ? new Color(0.1f, 0.2f, 0.6f) : 
                           shield.ShieldPercent > 0.25f ? Color.yellow : Color.red;
                statusText = $"{shield.CurrentHitPoints}/{shield.MaxHitPoints}";
            }

            Rect barRect = new Rect(rect.x + 7f, rect.y + 30f, rect.width - 14f, 20f);
            Texture2D coloredBarTex = SolidColorMaterials.NewSolidColorTexture(barColor);
            Widgets.FillableBar(barRect, fillPercent, coloredBarTex, EmptyBarTex, false);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, statusText);
            Text.Anchor = TextAnchor.UpperLeft;

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
