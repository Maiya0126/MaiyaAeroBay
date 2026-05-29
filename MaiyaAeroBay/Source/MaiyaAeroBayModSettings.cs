using Verse;

namespace MaiyaAeroBay
{
    public class MaiyaAeroBayModSettings : ModSettings
    {
        public float UpgradeCostPercent = 100f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref UpgradeCostPercent, "UpgradeCostPercent", 100f);
        }
    }
}