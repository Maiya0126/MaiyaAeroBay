using RimWorld;
using Verse;
using UnityEngine;

namespace MaiyaAeroBay
{
    public class Dialog_LeftBehindItem : Window
    {
        private Thing item;
        private CompShuttleRideHailing rideHailing;
        private string factionName;

        public override Vector2 InitialSize => new Vector2(420f, 260f);

        public Dialog_LeftBehindItem(Thing item, CompShuttleRideHailing rideHailing, string factionName)
        {
            this.item = item;
            this.rideHailing = rideHailing;
            this.factionName = factionName;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "MaiyaAeroBay_LeftBehindTitle".Translate());
            Text.Font = GameFont.Small;

            string desc = "MaiyaAeroBay_LeftBehindDesc".Translate(
                item.LabelCap, factionName);
            Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 60f), desc);

            float btnY = inRect.y + 120f;
            float btnW = (inRect.width - 20f) / 2f;

            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnW, 38f),
                "MaiyaAeroBay_LeftBehindKeep".Translate()))
            {
                rideHailing.HandleLeftBehindKeep(item);
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.x + btnW + 20f, btnY, btnW, 38f),
                "MaiyaAeroBay_LeftBehindReturn".Translate()))
            {
                rideHailing.HandleLeftBehindReturn(item);
                Close();
            }
        }
    }
}
