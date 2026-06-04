using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    public class RideOrderMarker : WorldObject
    {
        private string orderID = "";
        private bool isPickup = true;
        private string targetLabel = "";
        private string otherEndLabel = "";
        private string orderTypeLabel = "";

        private static Material pickupMat;
        private static Material dropoffMat;
        private static Texture2D pickupIcon;
        private static Texture2D dropoffIcon;

        public bool IsPickup => isPickup;

        public static Material PickupMat
        {
            get
            {
                if (pickupMat == null)
                {
                    pickupMat = MaterialPool.MatFrom("UI/Commands/car", ShaderDatabase.Cutout, new Color(0.2f, 0.85f, 0.3f));
                }
                return pickupMat;
            }
        }

        public static Material DropoffMat
        {
            get
            {
                if (dropoffMat == null)
                {
                    dropoffMat = MaterialPool.MatFrom("UI/Commands/car", ShaderDatabase.Cutout, new Color(0.9f, 0.5f, 0.1f));
                }
                return dropoffMat;
            }
        }

        public static Texture2D PickupIcon
        {
            get
            {
                if (pickupIcon == null)
                    pickupIcon = ContentFinder<Texture2D>.Get("UI/Commands/car", false);
                return pickupIcon;
            }
        }

        public static Texture2D DropoffIcon
        {
            get
            {
                if (dropoffIcon == null)
                    dropoffIcon = ContentFinder<Texture2D>.Get("UI/Commands/car", false);
                return dropoffIcon;
            }
        }

        public override string Label => isPickup
            ? "MaiyaAeroBay_RidePickupMarker".Translate(targetLabel)
            : "MaiyaAeroBay_RideDropoffMarker".Translate(targetLabel);

        public override Material Material => isPickup ? PickupMat : DropoffMat;

        public override Texture2D ExpandingIcon => isPickup ? PickupIcon : DropoffIcon;

        public override Color ExpandingIconColor => isPickup
            ? new Color(0.2f, 0.85f, 0.3f)
            : new Color(0.9f, 0.5f, 0.1f);

        public void Initialize(string orderID, bool isPickup, string targetLabel, string otherEndLabel, string orderTypeLabel)
        {
            this.orderID = orderID;
            this.isPickup = isPickup;
            this.targetLabel = targetLabel;
            this.otherEndLabel = otherEndLabel;
            this.orderTypeLabel = orderTypeLabel;
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder();
            if (isPickup)
                sb.AppendLine("MaiyaAeroBay_RidePickupInspect".Translate(orderTypeLabel, targetLabel, otherEndLabel));
            else
                sb.AppendLine("MaiyaAeroBay_RideDropoffInspect".Translate(orderTypeLabel, targetLabel, otherEndLabel));
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref orderID, "orderID", "");
            Scribe_Values.Look(ref isPickup, "isPickup", true);
            Scribe_Values.Look(ref targetLabel, "targetLabel", "");
            Scribe_Values.Look(ref otherEndLabel, "otherEndLabel", "");
            Scribe_Values.Look(ref orderTypeLabel, "orderTypeLabel", "");
        }
    }
}
