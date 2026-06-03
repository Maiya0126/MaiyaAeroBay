using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class Dialog_RideOrders : Window
    {
        private CompShuttleRideHailing rideHailing;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(520f, 620f);

        public Dialog_RideOrders(CompShuttleRideHailing rideHailing)
        {
            this.rideHailing = rideHailing;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (rideHailing == null || rideHailing.parent == null)
            {
                Close();
                return;
            }

            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f),
                "MaiyaAeroBay_RideOrdersTitle".Translate(rideHailing.parent.Label));
            Text.Font = GameFont.Small;
            y += 35f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f),
                "MaiyaAeroBay_RideStarInfo".Translate(
                    CompShuttleRideHailing.StarIconString(rideHailing.StarRating),
                    rideHailing.StarRating.ToString("F1"),
                    rideHailing.OrdersCompleted,
                    rideHailing.TotalIncome));
            y += 26f;

            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += 5f;

            var activeOrder = rideHailing.ActiveOrder;
            if (activeOrder != null)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f),
                    "MaiyaAeroBay_RideActiveOrderHeader".Translate());
                y += 24f;
                DrawOrderCard(activeOrder, inRect.x, ref y, inRect.width, true);
                y += 5f;
            }

            var pendingOrders = rideHailing.PendingOrders;
            if (pendingOrders.Count > 0 && activeOrder == null)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f),
                    "MaiyaAeroBay_RidePendingOrdersHeader".Translate(pendingOrders.Count));
                y += 24f;

                float listHeight = inRect.yMax - y - 40f;
                if (listHeight < 60f) listHeight = 60f;
                Rect listRect = new Rect(inRect.x, y, inRect.width, listHeight);
                Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, pendingOrders.Count * 95f + 10f);

                Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
                {
                    float cy = 0f;
                    foreach (var order in pendingOrders)
                    {
                        DrawOrderCardScrollable(order, 0f, ref cy, viewRect.width, false);
                    }
                }
                Widgets.EndScrollView();
            }
            else if (activeOrder == null)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 40f),
                    "MaiyaAeroBay_RideNoOrders".Translate());
            }
        }

        private void DrawOrderCard(RideOrder order, float x, ref float y, float width, bool isActive)
        {
            Color bgColor = isActive ? new Color(0.2f, 0.4f, 0.2f, 0.3f) : new Color(0.3f, 0.3f, 0.3f, 0.2f);
            if (order.dispatchMode == RideDispatchMode.Mandatory)
                bgColor = new Color(0.5f, 0.2f, 0.2f, 0.3f);

            Rect cardRect = new Rect(x, y, width, 90f);
            Widgets.DrawRectFast(cardRect, bgColor);

            string typeLabel = order.GetOrderTypeLabel();
            string route = order.pickupLabel + " \u2192 " + order.dropoffLabel;
            string reward = "MaiyaAeroBay_RideReward".Translate(order.rewardSilver);
            string dispatch = order.GetDispatchModeLabel();

            Widgets.Label(new Rect(x + 5f, y + 2f, width - 10f, 20f),
                typeLabel + "  [" + dispatch + "]");

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x + 5f, y + 22f, width - 10f, 20f), route);
            Widgets.Label(new Rect(x + 5f, y + 42f, width / 2f, 20f), reward);

            if (order.TicksRemaining < int.MaxValue)
            {
                string timer = "MaiyaAeroBay_RideDeadline".Translate(order.TicksRemaining.ToStringTicksToPeriod());
                Widgets.Label(new Rect(x + width / 2f, y + 42f, width / 2f - 5f, 20f), timer);
            }

            if (order.orderType == RideOrderType.TransportPerson)
            {
                Widgets.Label(new Rect(x + 5f, y + 62f, width - 10f, 20f),
                    "MaiyaAeroBay_RidePassengerName".Translate(order.passengerName));
            }
            else
            {
                Widgets.Label(new Rect(x + 5f, y + 62f, width - 10f, 20f),
                    "MaiyaAeroBay_RideCargoInfo".Translate(order.cargoCount, order.cargoDef?.label ?? "cargo"));
            }

            y += 95f;
        }

        private void DrawOrderCardScrollable(RideOrder order, float x, ref float y, float width, bool isActive)
        {
            float cardH = 90f;
            Color bgColor = order.dispatchMode == RideDispatchMode.Mandatory
                ? new Color(0.5f, 0.2f, 0.2f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f, 0.2f);

            Rect cardRect = new Rect(x, y, width, cardH);
            Widgets.DrawRectFast(cardRect, bgColor);

            string typeLabel = order.GetOrderTypeLabel();
            string dispatch = order.GetDispatchModeLabel();

            Widgets.Label(new Rect(x + 5f, y + 2f, width - 80f, 20f),
                typeLabel + "  [" + dispatch + "]");

            Rect acceptBtn = new Rect(x + width - 75f, y + 2f, 70f, 24f);
            if (Widgets.ButtonText(acceptBtn, "MaiyaAeroBay_RideAcceptBtn".Translate()))
            {
                rideHailing.AcceptOrder(order);
                Close();
                return;
            }

            string route = order.pickupLabel + " \u2192 " + order.dropoffLabel;
            Widgets.Label(new Rect(x + 5f, y + 22f, width - 10f, 20f), route);

            string reward = "MaiyaAeroBay_RideReward".Translate(order.rewardSilver);
            Widgets.Label(new Rect(x + 5f, y + 42f, width / 2f, 20f), reward);

            if (order.TicksRemaining < int.MaxValue)
            {
                string timer = "MaiyaAeroBay_RideDeadline".Translate(order.TicksRemaining.ToStringTicksToPeriod());
                Widgets.Label(new Rect(x + width / 2f, y + 42f, width / 2f - 5f, 20f), timer);
            }

            if (order.orderType == RideOrderType.TransportPerson)
            {
                Widgets.Label(new Rect(x + 5f, y + 62f, width - 10f, 20f),
                    "MaiyaAeroBay_RidePassengerName".Translate(order.passengerName));
            }
            else
            {
                Widgets.Label(new Rect(x + 5f, y + 62f, width - 10f, 20f),
                    "MaiyaAeroBay_RideCargoInfo".Translate(order.cargoCount, order.cargoDef?.label ?? "cargo"));
            }

            y += cardH + 5f;
        }
    }
}
