using Verse;
using Verse.AI;

namespace MaiyaAeroBay
{
    public class CompTargetEffect_InstallAeroBayKit : RimWorld.CompTargetEffect
    {
        public CompProperties_TargetEffectInstallAeroBayKit Props => (CompProperties_TargetEffectInstallAeroBayKit)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (user.IsColonistPlayerControlled)
            {
                JobDef installJob = DefDatabase<JobDef>.GetNamed("MaiyaAeroBay_InstallKit", false);
                if (installJob != null)
                {
                    Job job = JobMaker.MakeJob(installJob, target, parent);
                    job.count = 1;
                    job.playerForced = true;
                    user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }
        }
    }

    public class CompProperties_TargetEffectInstallAeroBayKit : CompProperties
    {
        public CompProperties_TargetEffectInstallAeroBayKit()
        {
            compClass = typeof(CompTargetEffect_InstallAeroBayKit);
        }
    }
}
