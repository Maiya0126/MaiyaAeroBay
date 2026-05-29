using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace MaiyaAeroBay
{
    public class MaiyaAeroBayMod : Mod
    {
        public static Harmony harmony;
        public static MaiyaAeroBayModSettings settings;

        public MaiyaAeroBayMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<MaiyaAeroBayModSettings>();

            harmony = new Harmony("Maiya.AeroBay");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("MaiyaAeroBay_UpgradeCostPercent".Translate() + ": " + settings.UpgradeCostPercent.ToString("F0") + "%");
            settings.UpgradeCostPercent = listingStandard.Slider(settings.UpgradeCostPercent, 10f, 500f);
            listingStandard.Gap(5f);
            listingStandard.Label("MaiyaAeroBay_UpgradeCostPercentDesc".Translate());
            
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "MaiyaAeroBay_ModSettings".Translate();
        }
    }
}