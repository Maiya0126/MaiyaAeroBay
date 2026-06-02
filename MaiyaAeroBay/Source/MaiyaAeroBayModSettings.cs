using Verse;

namespace MaiyaAeroBay
{
    public class MaiyaAeroBayModSettings : ModSettings
    {
        public bool interiorSpaceEnabled = true;
        public bool interiorMassEnabled = true;
        public bool powerFuelEnabled = true;
        public bool powerCooldownEnabled = true;
        public float wallPowerPerCell = 50f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref interiorSpaceEnabled, "interiorSpaceEnabled", true);
            Scribe_Values.Look(ref interiorMassEnabled, "interiorMassEnabled", true);
            Scribe_Values.Look(ref powerFuelEnabled, "powerFuelEnabled", true);
            Scribe_Values.Look(ref powerCooldownEnabled, "powerCooldownEnabled", true);
            Scribe_Values.Look(ref wallPowerPerCell, "wallPowerPerCell", 50f);
        }
    }
}
