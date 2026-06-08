using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace MaiyaAeroBay
{
    [StaticConstructorOnStartup]
    public static class RimTalkCompat
    {
        public static bool IsAvailable { get; private set; } = false;

        static RimTalkCompat()
        {
            try
            {
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod.PackageIdPlayerFacing?.IndexOf("rimtalk", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        InitAPI();
                        return;
                    }
                }
            }
            catch { }
        }

        private static void InitAPI()
        {
            try
            {
                var promptApiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "RimTalk.API.RimTalkPromptAPI");

                if (promptApiType == null) return;

                MethodInfo method = promptApiType.GetMethod("RegisterEnvironmentVariable",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(Func<Map, string>), typeof(string), typeof(int) },
                    null);

                Func<Map, string> provider = map => BuildAeroBayStatus();

                if (method != null)
                {
                    method.Invoke(null, new object[] { "MaiyaAeroBay", "aerobay_status", provider, "Ride-hailing business status", 100 });
                }
                else
                {
                    method = promptApiType.GetMethod("RegisterEnvironmentVariable",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string), typeof(Func<Map, string>) },
                        null);

                    if (method == null) return;

                    method.Invoke(null, new object[] { "MaiyaAeroBay", "aerobay_status", provider });
                }

                IsAvailable = true;
                Log.Message("[MaiyaAeroBay] RimTalk compat initialized");
            }
            catch (Exception ex)
            {
                Log.Warning("[MaiyaAeroBay] RimTalk compat init failed: " + ex.Message);
            }
        }

        private static string BuildAeroBayStatus()
        {
            try
            {
                var manager = WorldComponent_RideHailingManager.Instance;
                if (manager == null) return "";

                var shuttles = new List<CompShuttleRideHailing>();
                foreach (Map map in Find.Maps)
                {
                    foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle))
                    {
                        var comp = thing.TryGetComp<CompShuttleRideHailing>();
                        if (comp != null && comp.installed) shuttles.Add(comp);
                    }
                }

                if (shuttles.Count == 0) return "";

                var parts = new List<string>();
                bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");

                foreach (var s in shuttles)
                {
                    var sb = new List<string>();
                    if (isCN)
                    {
                        sb.Add($"信誉{s.StarRating:F1}星");
                        if (s.OrdersCompleted > 0) sb.Add($"完成{s.OrdersCompleted}单");
                        if (s.TotalIncome > 0) sb.Add($"收入{s.TotalIncome}银");
                        if (s.OrdersFailed > 0) sb.Add($"失败{s.OrdersFailed}单");
                        var order = s.ActiveOrder;
                        if (order != null) sb.Add($"当前: {order.pickupLabel}→{order.dropoffLabel}");
                    }
                    else
                    {
                        sb.Add($"{s.StarRating:F1}star");
                        if (s.OrdersCompleted > 0) sb.Add($"{s.OrdersCompleted} completed");
                        if (s.TotalIncome > 0) sb.Add($"{s.TotalIncome}s earned");
                        if (s.OrdersFailed > 0) sb.Add($"{s.OrdersFailed} failed");
                        var order = s.ActiveOrder;
                        if (order != null) sb.Add($"Active: {order.pickupLabel}->{order.dropoffLabel}");
                    }
                    parts.Add(string.Join(isCN ? "，" : ", ", sb));
                }

                string prefix = isCN ? "嘀嘀: " : "R-ber: ";
                return prefix + string.Join(isCN ? "；" : "; ", parts);
            }
            catch { return ""; }
        }
    }
}
