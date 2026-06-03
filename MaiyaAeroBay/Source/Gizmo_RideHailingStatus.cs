using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class Gizmo_RideHailingStatus : Gizmo
    {
        public CompShuttleRideHailing rideHailing;

        public Gizmo_RideHailingStatus()
        {
            Order = GizmoOrder.Special;
        }

        public override float GetWidth(float maxWidth)
        {
            return 140f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (rideHailing == null) return new GizmoResult(GizmoState.Clear);

            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawRectFast(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Widgets.DrawBox(rect);

            float stars = rideHailing.StarRating;
            string starStr = CompShuttleRideHailing.StarIconString(stars);

            Text.Font = GameFont.Tiny;
            Rect starRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 18f);
            Widgets.Label(starRect, starStr + " " + stars.ToString("F1"));

            Text.Font = GameFont.Tiny;
            Rect incomeRect = new Rect(rect.x + 4f, rect.y + 22f, rect.width - 8f, 16f);
            Widgets.Label(incomeRect, "MaiyaAeroBay_RideHailingIncome".Translate(rideHailing.TotalIncome));

            var activeOrder = rideHailing.ActiveOrder;
            if (activeOrder != null)
            {
                Rect orderRect = new Rect(rect.x + 4f, rect.y + 38f, rect.width - 8f, 16f);
                string stateText = activeOrder.state == RideOrderState.Accepted
                    ? "MaiyaAeroBay_RideStatePickup".Translate()
                    : "MaiyaAeroBay_RideStateDeliver".Translate();
                Widgets.Label(orderRect, stateText);

                if (activeOrder.TicksRemaining < int.MaxValue)
                {
                    Rect timerRect = new Rect(rect.x + 4f, rect.y + 54f, rect.width - 8f, 16f);
                    Widgets.Label(timerRect, activeOrder.TicksRemaining.ToStringTicksToPeriod());
                }
            }
            else
            {
                Rect statusRect = new Rect(rect.x + 4f, rect.y + 38f, rect.width - 8f, 16f);
                string status = rideHailing.IsAcceptingRides
                    ? "MaiyaAeroBay_RideStatusWaiting".Translate()
                    : "MaiyaAeroBay_RideStatusOffline".Translate();
                Widgets.Label(statusRect, status);
            }

            Text.Font = GameFont.Small;
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
