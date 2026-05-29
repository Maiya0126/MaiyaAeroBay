using System.Collections.Generic;
using Verse;
using RimWorld;

namespace MaiyaAeroBay
{
    public class CompProperties_TargetableSingleShuttle : CompProperties_Targetable
    {
        public CompProperties_TargetableSingleShuttle()
        {
            compClass = typeof(CompTargetable_SingleShuttle);
        }
    }

    public class CompTargetable_SingleShuttle : RimWorld.CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;

        protected override TargetingParameters GetTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = false,
                canTargetBuildings = true,
                canTargetItems = false,
                validator = (TargetInfo targ) =>
                {
                    if (!targ.HasThing)
                        return false;
                    
                    Thing thing = targ.Thing;
                    if (thing == null)
                        return false;
                    
                    if (thing.TryGetComp<RimWorld.CompShuttle>() == null)
                        return false;
                    
                    return true;
                }
            };
        }

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            yield return targetChosenByPlayer;
        }

        public override bool ValidateTarget(Verse.LocalTargetInfo target, bool showMessages = true)
        {
            if (!target.HasThing)
                return false;

            Thing thing = target.Thing;
            
            if (thing.TryGetComp<RimWorld.CompShuttle>() == null)
            {
                if (showMessages)
                {
                    Messages.Message("MaiyaAeroBay_NotAShuttle".Translate(), MessageTypeDefOf.RejectInput);
                }
                return false;
            }

            CompAeroBayKit kitComp = parent.TryGetComp<CompAeroBayKit>();
            if (kitComp != null)
            {
                switch (kitComp.Props.kitType)
                {
                    case ShuttleKitType.Interior:
                        if (thing.TryGetComp<Comp_ShuttleInterior>() != null)
                        {
                            if (showMessages)
                            {
                                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasInterior".Translate(), MessageTypeDefOf.RejectInput);
                            }
                            return false;
                        }
                        break;
                    case ShuttleKitType.Weapon:
                        if (thing.TryGetComp<CompShuttleWeapon>() is CompShuttleWeapon w && w.installed)
                        {
                            if (showMessages)
                            {
                                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasWeapon".Translate(), MessageTypeDefOf.RejectInput);
                            }
                            return false;
                        }
                        break;
                    case ShuttleKitType.Shield:
                        if (thing.TryGetComp<CompShuttleShield>() is CompShuttleShield s && s.installed)
                        {
                            if (showMessages)
                            {
                                Messages.Message("MaiyaAeroBay_ShuttleAlreadyHasShield".Translate(), MessageTypeDefOf.RejectInput);
                            }
                            return false;
                        }
                        break;
                }
            }

            return base.ValidateTarget(target, showMessages);
        }
    }
}
