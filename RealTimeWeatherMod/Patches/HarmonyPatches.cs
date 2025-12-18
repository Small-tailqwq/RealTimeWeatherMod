using System;
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

    [HarmonyPatch(typeof(EnvironmentController), "Setup")]
    internal static class EnvControllerPatch
    {
        static void Postfix(EnvironmentController __instance)
        {
            EnvRegistry.Register(__instance.EnvironmentType, __instance);
        }
    }

    [HarmonyPatch(typeof(FacilityEnvironment), "Setup")]
    internal static class FacilityEnvPatch
    {
        static void Postfix(FacilityEnvironment __instance)
        {
            try
            {
                FieldInfo field = typeof(FacilityEnvironment).GetField("_windowViewService", BindingFlags.Instance | BindingFlags.NonPublic);
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
            catch (Exception ex) { ChillEnvPlugin.Log?.LogError($"捕获 Service 失败: {ex}"); }
        }
    }

    // 日期栏追加天气信息 Hook
    [HarmonyPatch(typeof(CurrentDateAndTimeUI), "UpdateDateAndTime")]
    internal static class DateUIPatch
    {
        static void Postfix(CurrentDateAndTimeUI __instance)
        {
            // 1. 处理天气显示 (原功能)
            if (ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value && !string.IsNullOrEmpty(ChillEnvPlugin.UIWeatherString))
            {
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

            // 2. 处理详细时间段显示 (新功能 - v5.1.1)
            // 仅在用户开启了该功能时执行
            if (ChillEnvPlugin.Cfg_DetailedTimeSegments.Value)
            {
                try
                {
                    // 检查游戏设置是否为 12小时制 (AMPM)
                    // 如果不是 AMPM 模式，游戏代码会将 _amPmText 置空，我们也就不需要处理了
                    // 注意：如果编译报错提示找不到 R3 程序集，请在项目中添加 R3.dll 的引用
                    var timeFormat = SaveDataManager.Instance.SettingData.TimeFormat.Value;
                    if (timeFormat.ToString() != "AMPM") return;

                    // 计算时间段文本 (已修改为 C# 7.3 兼容写法)
                    int hour = DateTime.Now.Hour;
                    string timeSegment = "晚上"; // 默认

                    if (hour >= 0 && hour < 5) timeSegment = "凌晨";
                    else if (hour >= 5 && hour < 7) timeSegment = "清晨";
                    else if (hour >= 7 && hour < 11) timeSegment = "上午";
                    else if (hour >= 11 && hour < 13) timeSegment = "中午";
                    else if (hour >= 13 && hour < 18) timeSegment = "下午";
                    else if (hour >= 18 && hour < 19) timeSegment = "傍晚";
                    else if (hour >= 19 && hour <= 23) timeSegment = "晚上";

                    // 获取 _amPmText 组件 (TextLocalizationBehaviour)
                    var amPmField = typeof(CurrentDateAndTimeUI).GetField("_amPmText", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (amPmField != null)
                    {
                        var localizationBehaviour = amPmField.GetValue(__instance);
                        if (localizationBehaviour != null)
                        {
                            // 获取 TextLocalizationBehaviour 下的 TextMeshProUGUI 组件 (属性名为 Text)
                            var textProp = localizationBehaviour.GetType().GetProperty("Text");
                            if (textProp != null)
                            {
                                var tmpro = textProp.GetValue(localizationBehaviour) as TextMeshProUGUI;
                                if (tmpro != null)
                                {
                                    // 强制覆盖游戏原有的 "AM"/"PM" 本地化文本
                                    tmpro.text = timeSegment;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // 避免每帧报错刷屏，这里静默处理
                }
            }
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "OnClickButtonMainIcon")]
    internal static class UserInteractionPatch
    {
        public static bool IsSimulatingClick = false;
        static void Prefix(EnvironmentController __instance)
        {
            if (!IsSimulatingClick)
            {
                EnvironmentType type = __instance.EnvironmentType;
                if (!SceneryAutomationSystem.UserInteractedMods.Contains(type))
                {
                    SceneryAutomationSystem.UserInteractedMods.Add(type);
                    ChillEnvPlugin.Log?.LogInfo($"[用户交互] 用户接管了 {type}，停止自动托管。");
                }
                
                // 立即从自动托管列表中移除
                if (SceneryAutomationSystem._autoEnabledMods.Contains(type))
                {
                    SceneryAutomationSystem._autoEnabledMods.Remove(type);
                    ChillEnvPlugin.Log?.LogDebug($"[用户交互] 已从托管列表移除 {type}");
                }
                
                // 特殊处理：用户关闭系统抽中的鲸鱼时,清除标志（不恢复天气）
                if (type == EnvironmentType.Whale && SceneryAutomationSystem.IsWhaleSystemTriggered)
                {
                    SceneryAutomationSystem.IsWhaleSystemTriggered = false;
                    ChillEnvPlugin.Log?.LogInfo("[鲸鱼彩蛋] 用户手动关闭了系统抽中的鲸鱼，标志已清除");
                }
            }
        }
    }

    // 监控解锁状态是否被重新锁定
    [HarmonyPatch]
    internal static class UnlockStatusMonitorPatch
    {
        // 尝试监控 _isLocked 的 Value setter
        static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            var results = new System.Collections.Generic.List<MethodBase>();
            try
            {
                // 查找所有可能修改 _isLocked 的地方
                var assembly = typeof(UnlockItemService).Assembly;
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name.Contains("UnlockEnvironmentData") || type.Name.Contains("UnlockDecorationData"))
                    {
                        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            if (method.Name.Contains("Lock") || method.Name.Contains("Unlock"))
                            {
                                results.Add(method);
                                ChillEnvPlugin.Log?.LogInfo($"[监控] 找到解锁相关方法: {type.Name}.{method.Name}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"[监控] 查找目标方法失败: {ex.Message}");
            }
            return results;
        }

        static void Prefix(MethodBase __originalMethod)
        {
            if (ChillEnvPlugin.Cfg_DebugMode.Value && ChillEnvPlugin.Initialized)
            {
                ChillEnvPlugin.Log?.LogWarning($"[监控] 检测到解锁状态变更调用: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
            }
        }
    }
}