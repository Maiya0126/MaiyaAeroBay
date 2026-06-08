using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace MaiyaAeroBay
{
    [StaticConstructorOnStartup]
    public static class RimTuberCompat
    {
        private static bool available = false;
        private static Type apiType;
        private static MethodInfo postSystemMessageMethod;
        private static MethodInfo triggerGameEventMethod;
        private static MethodInfo addHeatMethod;
        private static MethodInfo isLiveMethod;
        private static Type gameEventTypeType;
        private static object letterReceivedEvent;

        static RimTuberCompat()
        {
            try
            {
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod.PackageIdPlayerFacing?.IndexOf("rimtuber", StringComparison.OrdinalIgnoreCase) >= 0)
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
                apiType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "RimTuber.RimTuberAPI");

                if (apiType == null) return;

                postSystemMessageMethod = apiType.GetMethod("PostSystemMessage", BindingFlags.Public | BindingFlags.Static);
                triggerGameEventMethod = apiType.GetMethod("TriggerGameEvent", BindingFlags.Public | BindingFlags.Static);
                addHeatMethod = apiType.GetMethod("AddHeat", BindingFlags.Public | BindingFlags.Static);
                isLiveMethod = apiType.GetMethod("IsLive", BindingFlags.Public | BindingFlags.Static);

                gameEventTypeType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "RimTuber.GameEventType");

                if (gameEventTypeType != null)
                {
                    letterReceivedEvent = Enum.Parse(gameEventTypeType, "LetterReceived");
                }

                available = postSystemMessageMethod != null && isLiveMethod != null;
                if (available)
                    Log.Message("[MaiyaAeroBay] RimTuber compat initialized");
            }
            catch (Exception ex)
            {
                Log.Error("[MaiyaAeroBay] RimTuber compat init failed: " + ex.Message);
            }
        }

        public static bool IsAvailable => available;

        public static bool IsLive()
        {
            if (!available) return false;
            try { return (bool)isLiveMethod.Invoke(null, null); }
            catch { return false; }
        }

        public static void PostSystemMessage(string text, Color color)
        {
            if (!available || !IsLive()) return;
            try { postSystemMessageMethod.Invoke(null, new object[] { text, color }); }
            catch { }
        }

        public static void TriggerEvent(string eventType, float impact, Dictionary<string, string> context)
        {
            if (!available || !IsLive()) return;
            try
            {
                if (triggerGameEventMethod != null && gameEventTypeType != null && letterReceivedEvent != null)
                {
                    triggerGameEventMethod.Invoke(null, new object[] { letterReceivedEvent, impact, context });
                }
                if (addHeatMethod != null)
                {
                    addHeatMethod.Invoke(null, new object[] { impact * 0.3f });
                }
            }
            catch { }
        }

        public static void OnFareDodged(int unpaidSilver, float starRating)
        {
            if (!available) return;
            bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");
            string msg = isCN
                ? $"[嘀嘀] 乘客飞单！{unpaidSilver}银未到账，当前{starRating:F1}星"
                : $"[R-ber] Fare dodged! {unpaidSilver}s unpaid, rating {starRating:F1}star";
            PostSystemMessage(msg, new Color(1f, 0.5f, 0.3f));

            var ctx = new Dictionary<string, string>
            {
                ["eventType"] = "fareDodged",
                ["unpaidSilver"] = unpaidSilver.ToString(),
                ["starRating"] = starRating.ToString("F1")
            };
            TriggerEvent("LetterReceived", 0.8f, ctx);
        }

        public static void OnBadReview(int penaltySilver, float starRating)
        {
            if (!available) return;
            bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");
            string msg = isCN
                ? $"[嘀嘀] 恶意差评！扣{penaltySilver}银，降至{starRating:F1}星"
                : $"[R-ber] Bad review! -{penaltySilver}s, rating {starRating:F1}star";
            PostSystemMessage(msg, new Color(1f, 0.3f, 0.3f));

            var ctx = new Dictionary<string, string>
            {
                ["eventType"] = "badReview",
                ["penaltySilver"] = penaltySilver.ToString(),
                ["starRating"] = starRating.ToString("F1")
            };
            TriggerEvent("LetterReceived", 0.6f, ctx);
        }

        public static void OnGoodTip(int tipSilver, float starRating)
        {
            if (!available) return;
            bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");
            string msg = isCN
                ? $"[嘀嘀] 好评打赏！+{tipSilver}银，升至{starRating:F1}星"
                : $"[R-ber] Great tip! +{tipSilver}s, rating {starRating:F1}star";
            PostSystemMessage(msg, new Color(0.3f, 1f, 0.5f));

            var ctx = new Dictionary<string, string>
            {
                ["eventType"] = "goodTip",
                ["tipSilver"] = tipSilver.ToString(),
                ["starRating"] = starRating.ToString("F1")
            };
            TriggerEvent("LetterReceived", 0.5f, ctx);
        }

        public static void OnLeftBehind(string itemName, bool kept)
        {
            if (!available) return;
            bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");
            string msg = isCN
                ? (kept ? $"[嘀嘀] 司机留下了乘客遗落的{itemName}" : $"[嘀嘀] 司机归还了乘客遗落的{itemName}")
                : (kept ? $"[R-ber] Driver kept the left-behind {itemName}" : $"[R-ber] Driver returned the left-behind {itemName}");
            PostSystemMessage(msg, kept ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 0.8f, 1f));

            var ctx = new Dictionary<string, string>
            {
                ["eventType"] = "leftBehind",
                ["itemName"] = itemName,
                ["kept"] = kept.ToString()
            };
            TriggerEvent("SocialInteract", 0.4f, ctx);
        }

        public static void OnOrderCompleted(int rewardSilver, float starRating)
        {
            if (!available) return;
            bool isCN = LanguageDatabase.activeLanguage.FriendlyNameEnglish.Contains("Chinese");
            string msg = isCN
                ? $"[嘀嘀] 订单完成！+{rewardSilver}银，{starRating:F1}星"
                : $"[R-ber] Order complete! +{rewardSilver}s, {starRating:F1}star";
            PostSystemMessage(msg, new Color(0.3f, 0.8f, 0.3f));
        }
    }
}
