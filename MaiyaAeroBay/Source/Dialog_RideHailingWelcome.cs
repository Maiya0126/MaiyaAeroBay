using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class Dialog_RideHailingWelcome : Window
    {
        public override Vector2 InitialSize => new Vector2(480f, 400f);

        public Dialog_RideHailingWelcome()
        {
            forcePause = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f),
                "MaiyaAeroBay_RideWelcomeTitle".Translate());
            Text.Font = GameFont.Small;
            y += 40f;

            string desc = "MaiyaAeroBay_RideWelcomeDesc".Translate();
            float descHeight = Text.CalcHeight(desc, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, descHeight), desc);
            y += descHeight + 10f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 10f;

            string features = "MaiyaAeroBay_RideWelcomeFeatures".Translate();
            float featuresHeight = Text.CalcHeight(features, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, featuresHeight), features);
            y += featuresHeight + 10f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 10f;

            string note = "MaiyaAeroBay_RideWelcomeNote".Translate();
            float noteHeight = Text.CalcHeight(note, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, noteHeight), note);
            y += noteHeight + 20f;

            float btnWidth = 140f;
            float btnHeight = 36f;
            float gap = 20f;
            float totalWidth = btnWidth * 2 + gap;
            float startX = inRect.x + (inRect.width - totalWidth) / 2f;

            Rect enableBtn = new Rect(startX, y, btnWidth, btnHeight);
            if (Widgets.ButtonText(enableBtn, "MaiyaAeroBay_RideWelcomeEnable".Translate()))
            {
                MaiyaAeroBayMod.settings.rideHailingEnabled = true;
                MaiyaAeroBayMod.settings.Write();
                Find.WindowStack.TryRemove(this);
            }

            Rect disableBtn = new Rect(startX + btnWidth + gap, y, btnWidth, btnHeight);
            if (Widgets.ButtonText(disableBtn, "MaiyaAeroBay_RideWelcomeDisable".Translate()))
            {
                MaiyaAeroBayMod.settings.rideHailingEnabled = false;
                MaiyaAeroBayMod.settings.Write();
                Find.WindowStack.TryRemove(this);
            }
        }
    }
}
