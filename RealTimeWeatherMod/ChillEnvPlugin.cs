using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Bulbul;

namespace ChillWithYou.EnvSync
{
    [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", "5.1.0")]
    public class ChillEnvPlugin : BaseUnityPlugin
    {
        internal static ChillEnvPlugin Instance;
        internal static ManualLogSource Log;
        internal static UnlockItemService UnlockItemServiceInstance;

        internal static object WindowViewServiceInstance;
        internal static MethodInfo ChangeWeatherMethod;
        internal static string UIWeatherString = "";
        internal static bool Initialized;

        // --- 配置项 ---
        internal static ConfigEntry<int> Cfg_WeatherRefreshMinutes;
        internal static ConfigEntry<string> Cfg_SunriseTime;
        internal static ConfigEntry<string> Cfg_SunsetTime;
        internal static ConfigEntry<string> Cfg_SeniverseKey;
        internal static ConfigEntry<string> Cfg_Location;
        internal static ConfigEntry<bool> Cfg_EnableWeatherSync;
        internal static ConfigEntry<bool> Cfg_UnlockEnvironments;
        internal static ConfigEntry<bool> Cfg_UnlockDecorations;
        internal static ConfigEntry<bool> Cfg_ShowWeatherOnUI;
        internal static ConfigEntry<bool> Cfg_EnableEasterEggs;

        // 调试配置
        internal static ConfigEntry<bool> Cfg_DebugMode;
        internal static ConfigEntry<int> Cfg_DebugCode;
        internal static ConfigEntry<int> Cfg_DebugTemp;
        internal static ConfigEntry<string> Cfg_DebugText;

        private static GameObject _runnerGO;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo("【5.1.0】启动 - 代码重构版 (多文件结构)");

            try
            {
                var harmony = new Harmony("ChillWithYou.EnvSync");
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony 失败: {ex}");
            }

            InitConfig();

            try
            {
                _runnerGO = new GameObject("ChillEnvSyncRunner");
                _runnerGO.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(_runnerGO);
                _runnerGO.SetActive(true);

                // 挂载组件
                _runnerGO.AddComponent<Core.AutoEnvRunner>();
                _runnerGO.AddComponent<Core.SceneryAutomationSystem>();
            }
            catch (Exception ex)
            {
                Log.LogError($"Runner 创建失败: {ex}");
            }
        }

        private void InitConfig()
        {
            Cfg_WeatherRefreshMinutes = Config.Bind("WeatherSync", "RefreshMinutes", 30, "天气API刷新间隔(分钟)");
            Cfg_SunriseTime = Config.Bind("TimeConfig", "Sunrise", "06:30", "日出时间");
            Cfg_SunsetTime = Config.Bind("TimeConfig", "Sunset", "18:30", "日落时间");

            Cfg_EnableWeatherSync = Config.Bind("WeatherAPI", "EnableWeatherSync", false, "是否启用天气API同步");
            Cfg_SeniverseKey = Config.Bind("WeatherAPI", "SeniverseKey", "", "心知天气 API Key");
            Cfg_Location = Config.Bind("WeatherAPI", "Location", "beijing", "城市名称");

            Cfg_UnlockEnvironments = Config.Bind("Unlock", "UnlockAllEnvironments", true, "自动解锁环境");
            Cfg_UnlockDecorations = Config.Bind("Unlock", "UnlockAllDecorations", true, "自动解锁装饰");
            Cfg_ShowWeatherOnUI = Config.Bind("UI", "ShowWeatherOnDate", true, "日期栏显示天气");
            Cfg_EnableEasterEggs = Config.Bind("Automation", "EnableSeasonalEasterEggs", true, "启用季节性彩蛋与环境音效自动托管");

            Cfg_DebugMode = Config.Bind("Debug", "EnableDebugMode", false, "调试模式");
            Cfg_DebugCode = Config.Bind("Debug", "SimulatedCode", 1, "模拟天气代码");
            Cfg_DebugTemp = Config.Bind("Debug", "SimulatedTemp", 25, "模拟温度");
            Cfg_DebugText = Config.Bind("Debug", "SimulatedText", "DebugWeather", "模拟描述");
        }

        internal static void TryInitializeOnce(UnlockItemService svc)
        {
            if (Initialized || svc == null) return;

            if (Cfg_UnlockEnvironments.Value) ForceUnlockAllEnvironments(svc);
            if (Cfg_UnlockDecorations.Value) ForceUnlockAllDecorations(svc);

            Initialized = true;
            Log?.LogInfo("初始化完成");
        }

        internal static void CallServiceChangeWeather(EnvironmentType envType)
        {
            if (WindowViewServiceInstance == null || ChangeWeatherMethod == null) return;
            try
            {
                var parameters = ChangeWeatherMethod.GetParameters();
                if (parameters.Length == 0) return;
                Type windowViewEnumType = parameters[0].ParameterType;
                object enumValue = Enum.Parse(windowViewEnumType, envType.ToString());
                ChangeWeatherMethod.Invoke(WindowViewServiceInstance, new object[] { enumValue });
            }
            catch (Exception ex) { Log?.LogError($"Service调用失败: {ex.Message}"); }
        }

        internal static void SimulateClickMainIcon(EnviromentController ctrl)
        {
            if (ctrl == null) return;
            try
            {
                MethodInfo clickMethod = ctrl.GetType().GetMethod("OnClickButtonMainIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (clickMethod != null)
                {
                    Patches.UserInteractionPatch.IsSimulatingClick = true; // 标记开始
                    clickMethod.Invoke(ctrl, null);
                    Patches.UserInteractionPatch.IsSimulatingClick = false; // 标记结束
                }
            }
            catch (Exception ex) { Log?.LogError($"模拟点击失败: {ex.Message}"); }
        }

        private static void ForceUnlockAllEnvironments(UnlockItemService svc)
        {
            try
            {
                var envProp = svc.GetType().GetProperty("Environment");
                var unlockEnvObj = envProp.GetValue(svc);
                var dictField = unlockEnvObj.GetType().GetField("_environmentDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = dictField.GetValue(unlockEnvObj) as System.Collections.IDictionary;
                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var reactive = lockField.GetValue(data);
                    var propValue = reactive.GetType().GetProperty("Value");
                    propValue.SetValue(reactive, false, null);
                    count++;
                }
                Log?.LogInfo($"✅ 已解锁 {count} 个环境");
            }
            catch { }
        }

        private static void ForceUnlockAllDecorations(UnlockItemService svc)
        {
            try
            {
                var decoProp = svc.GetType().GetProperty("Decoration");
                if (decoProp == null) return;
                var unlockDecoObj = decoProp.GetValue(svc);
                if (unlockDecoObj == null) return;
                var dictField = unlockDecoObj.GetType().GetField("_decorationDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (dictField == null) return;
                var dict = dictField.GetValue(unlockDecoObj) as System.Collections.IDictionary;
                if (dict == null) return;
                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (lockField == null) continue;
                    var reactive = lockField.GetValue(data);
                    if (reactive == null) continue;
                    var propValue = reactive.GetType().GetProperty("Value");
                    if (propValue == null) continue;
                    propValue.SetValue(reactive, false, null);
                    count++;
                }
                Log?.LogInfo($"✅ 已解锁 {count} 个装饰品");
            }
            catch { }
        }
    }
}