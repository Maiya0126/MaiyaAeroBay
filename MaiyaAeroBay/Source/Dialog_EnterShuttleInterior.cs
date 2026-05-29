using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.AI;

namespace MaiyaAeroBay
{
    public class Dialog_EnterShuttleInterior : Window
    {
        private readonly Comp_ShuttleInterior shuttleComp;
        private Vector2 scrollPosition;
        private Pawn selectedPawn;

        public Dialog_EnterShuttleInterior(Comp_ShuttleInterior comp)
        {
            shuttleComp = comp;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(600, 500);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40), "MaiyaAeroBay_SelectPawnsToEnter".Translate());
            
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(inRect.x, inRect.y + 28, inRect.width, 20), "MaiyaAeroBay_SelectOneColonist".Translate());
            GUI.color = Color.white;

            Rect listRect = new Rect(inRect.x, inRect.y + 55, inRect.width, inRect.height - 110);
            DrawPawnList(listRect);

            Rect buttonRect = new Rect(inRect.x + inRect.width - 120, inRect.y + inRect.height - 45, 120, 35);
            if (Widgets.ButtonText(buttonRect, "Enter".Translate(), true, false, true))
            {
                TryEnterPawn();
            }
        }

        private void DrawPawnList(Rect rect)
        {
            List<Pawn> colonists = shuttleComp.ParentMap.mapPawns.FreeColonists.ToList();
            if (colonists.Count == 0)
            {
                Widgets.Label(rect, "NoColonists".Translate());
                return;
            }

            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 20, colonists.Count * 35 + 10));
            
            float y = 0;
            foreach (Pawn pawn in colonists)
            {
                Rect rowRect = new Rect(0, y, rect.width - 20, 35);
                bool isSelected = selectedPawn == pawn;
                
                if (Widgets.ButtonInvisible(rowRect, true))
                {
                    selectedPawn = pawn;
                }

                Rect checkRect = new Rect(rowRect.x + 5, rowRect.y + 5, 25, 25);
                Widgets.Checkbox(checkRect.position, ref isSelected, 24);

                Rect pawnRect = new Rect(checkRect.xMax + 10, rowRect.y, 30, 30);
                Widgets.ThingIcon(pawnRect, pawn);

                Rect labelRect = new Rect(pawnRect.xMax + 10, rowRect.y + 5, 200, 25);
                Widgets.Label(labelRect, pawn.Name.ToStringShort);

                Rect statusRect = new Rect(labelRect.xMax + 10, rowRect.y + 5, 150, 25);
                if (pawn.Downed)
                {
                    Widgets.Label(statusRect, "IsDowned".Translate().ToString().Colorize(Color.red));
                }
                else if (!pawn.CanReach(shuttleComp.parent, PathEndMode.Touch, Danger.Deadly))
                {
                    Widgets.Label(statusRect, "NoPath".Translate().ToString().Colorize(Color.gray));
                }

                y += 35;
            }

            Widgets.EndScrollView();
        }

        private void TryEnterPawn()
        {
            if (selectedPawn == null)
            {
                Messages.Message("MaiyaAeroBay_SelectAtLeastOnePawn".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (!shuttleComp.PocketMapExists)
            {
                Messages.Message("MaiyaAeroBay_ExitNotReady".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Pawn pawn = selectedPawn;

            if (!pawn.CanReach(shuttleComp.parent, PathEndMode.Touch, Danger.Deadly))
            {
                Messages.Message("MaiyaAeroBay_PawnCantReach".Translate(pawn.Name.ToStringShort), MessageTypeDefOf.RejectInput);
                return;
            }

            if (pawn.Downed)
            {
                Messages.Message("MaiyaAeroBay_PawnDowned".Translate(pawn.Name.ToStringShort), MessageTypeDefOf.RejectInput);
                return;
            }

            var enterJobDef = DefDatabase<JobDef>.GetNamed("EnterShuttleInterior", false);
            if (enterJobDef != null)
            {
                Job job = JobMaker.MakeJob(enterJobDef, shuttleComp.parent);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                SoundDefOf.Click.PlayOneShotOnCamera();
                Close();
            }
        }
    }
}