using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MaiyaAeroBay
{
    public class Dialog_SelectPawnsForExit : Window
    {
        private readonly ExitPortal exitPortal;
        private readonly Comp_ShuttleInterior shuttleComp;
        private Vector2 scrollPosition;
        private List<Pawn> selectedPawns = new List<Pawn>();

        public Dialog_SelectPawnsForExit(ExitPortal exitPortal, Comp_ShuttleInterior shuttleComp)
        {
            this.exitPortal = exitPortal;
            this.shuttleComp = shuttleComp;
            forcePause = true;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(600, 500);

        public override void DoWindowContents(Rect inRect)
        {
            bool isEmergency = shuttleComp == null || shuttleComp.parent == null || !shuttleComp.parent.Spawned;

            Text.Font = GameFont.Medium;
            if (isEmergency)
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40), "MaiyaAeroBay_EmergencyExitTitle".Translate());
                GUI.color = Color.white;
            }
            else
            {
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40), "MaiyaAeroBay_SelectPawnsToExit".Translate());
            }
            Text.Font = GameFont.Small;

            if (isEmergency)
            {
                Rect warnRect = new Rect(inRect.x, inRect.y + 42, inRect.width, 30);
                GUI.color = new Color(1f, 0.6f, 0.1f);
                Widgets.Label(warnRect, "MaiyaAeroBay_EmergencyExitWarning".Translate());
                GUI.color = Color.white;
            }

            float topOffset = isEmergency ? 75 : 45;
            Rect listRect = new Rect(inRect.x, inRect.y + topOffset, inRect.width, inRect.height - topOffset - 55);
            DrawPawnList(listRect);

            Rect buttonRect = new Rect(inRect.x + inRect.width - 240, inRect.y + inRect.height - 45, 115, 35);
            if (Widgets.ButtonText(buttonRect, "MaiyaAeroBay_EvacuateAll".Translate(), true, false, true))
            {
                exitPortal.EvacuateAll();
                Close();
            }

            Rect enterRect = new Rect(inRect.x + inRect.width - 120, inRect.y + inRect.height - 45, 120, 35);
            string exitLabel = isEmergency ? "MaiyaAeroBay_EmergencyEvacuateSelected".Translate() : "MaiyaAeroBay_EvacuateSelected".Translate();
            if (Widgets.ButtonText(enterRect, exitLabel, true, false, true))
            {
                TryExitPawns();
            }
        }

        private void DrawPawnList(Rect rect)
        {
            List<Pawn> colonists = exitPortal.Map.mapPawns.FreeColonists.ToList();
            if (colonists.Count == 0)
            {
                Widgets.Label(rect, "MaiyaAeroBay_NoPawnsToExit".Translate());
                return;
            }

            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 20, colonists.Count * 35 + 10));
            
            float y = 0;
            foreach (Pawn pawn in colonists)
            {
                Rect rowRect = new Rect(0, y, rect.width - 20, 35);
                bool isSelected = selectedPawns.Contains(pawn);
                
                if (Widgets.ButtonInvisible(rowRect, true))
                {
                    if (isSelected)
                        selectedPawns.Remove(pawn);
                    else
                        selectedPawns.Add(pawn);
                }

                Rect checkRect = new Rect(rowRect.x + 5, rowRect.y + 5, 25, 25);
                Widgets.Checkbox(checkRect.position, ref isSelected, 24);

                Rect pawnRect = new Rect(checkRect.xMax + 10, rowRect.y, 30, 30);
                Widgets.ThingIcon(pawnRect, pawn);

                Rect labelRect = new Rect(pawnRect.xMax + 10, rowRect.y + 5, 200, 25);
                Widgets.Label(labelRect, pawn.Name.ToStringShort);

                y += 35;
            }

            Widgets.EndScrollView();
        }

        private void TryExitPawns()
        {
            if (selectedPawns.Count == 0)
            {
                Messages.Message("MaiyaAeroBay_SelectAtLeastOne".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            exitPortal.TransportPawnsToShuttle(selectedPawns);
            Close();
        }
    }
}