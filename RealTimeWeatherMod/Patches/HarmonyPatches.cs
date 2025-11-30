using System.Reflection;
using HarmonyLib;
using Bulbul;
using TMPro;
using ChillWithYou.EnvSync.Utils;
using ChillWithYou.EnvSync.Core;

namespace ChillWithYou.EnvSync.Patches
{
    [HarmonyPatch(typeof(UnlockItemService), "Setup")]
    internal static class UnlockServicePatch
    {
        static void Postfix(UnlockItemService __instance)
        {
            ChillEnvPlugin.UnlockItemServiceInstance = __instance;
            ChillEnvPlugin.TryInitializeOnce(__instance);
        }
    }

    [HarmonyPatch(typeof(EnviromentController), "Setup")]
    internal static class EnvControllerPatch
    {
        static void Postfix(EnviromentController __instance)
        {
            EnvRegistry.Register(__instance.EnvironmentType, __instance);
        }
    }

    [HarmonyPatch(typeof(FacilityEnviroment), "Setup")]
    internal static class FacilityEnvPatch
    {
        static void Postfix(FacilityEnviroment __instance)
        {
            try
            {
                FieldInfo field = typeof(FacilityEnviroment).GetField("_windowViewService", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object service = field.GetValue(__instance);
                    if (service != null)
                    {
                        ChillEnvPlugin.WindowViewServiceInstance = service;
                        ChillEnvPlugin.ChangeWeatherMethod = service.GetType().GetMethod("ChangeWeatherAndTime", BindingFlags.Instance | BindingFlags.Public);
                        if (ChillEnvPlugin.ChangeWeatherMethod != null)
                            ChillEnvPlugin.Log?.LogInfo("✅ 成功捕获 WindowViewService.ChangeWeatherAndTime");
                    }
                }
            }
            catch (System.Exception ex) { ChillEnvPlugin.Log?.LogError($"捕获 Service 失败: {ex}"); }
        }
    }

    [HarmonyPatch(typeof(CurrentDateAndTimeUI), "UpdateDateAndTime")]
    internal static class DateUIPatch
    {
        static void Postfix(CurrentDateAndTimeUI __instance)
        {
            if (!ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value || string.IsNullOrEmpty(ChillEnvPlugin.UIWeatherString)) return;
            try
            {
                var field = typeof(CurrentDateAndTimeUI).GetField("_dateText", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var textMesh = field.GetValue(__instance) as TextMeshProUGUI;
                    if (textMesh != null)
                        textMesh.text += $" | {ChillEnvPlugin.UIWeatherString}";
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(EnviromentController), "OnClickButtonMainIcon")]
    internal static class UserInteractionPatch
    {
        public static bool IsSimulatingClick = false;
        static void Prefix(EnviromentController __instance)
        {
            if (!IsSimulatingClick)
            {
                EnvironmentType type = __instance.EnvironmentType;
                if (!SceneryAutomationSystem.UserInteractedMods.Contains(type))
                {
                    SceneryAutomationSystem.UserInteractedMods.Add(type);
                    ChillEnvPlugin.Log?.LogInfo($"[用户交互] 用户接管了 {type}，停止自动托管。");
                }
            }
        }
    }
}